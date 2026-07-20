using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Middleware;
using Newtonsoft.Json;
using UnityEngine;

public partial class ChessStageController
{
  

    #region 数据配置
    private readonly Dictionary<int, ComboConfig> _comboConfigDict = new Dictionary<int, ComboConfig>();
    private readonly Dictionary<int, ComboConfig> _reduceConfigDict = new Dictionary<int, ComboConfig>();
    public IceConfig IceConfig { get; private set; }          // 冰块
    public LeafConfig LeafConfig { get; private set; }        // 叶子
    public FlowerConfig FlowerConfig { get; private set; }    // 花朵
    public readonly List<StimulateRuleConfig> StimulateRules = new List<StimulateRuleConfig>();   // 关卡鼓励词规则配置
    
    /// <summary>
    /// 局内正向反馈配置数据
    /// </summary>
    public readonly Dictionary<int, PraiseConfig> PraiseConfigDict = new Dictionary<int, PraiseConfig>();
    
    #endregion
    // 🌟 新增：记录本关卡总共生成过几片叶子（用于皮肤循环切换）
    public int LeafGenCounter
    {
        get
        {
            // 防御性编程：若底层数据尚未完成初始化，自动返回0安全兜底
            if (GameDataManager.Instance == null || GameDataManager.Instance.ButterflyData == null)
                return 0;

            return GameDataManager.Instance.ButterflyData.leafSkinCounter;
        }
        set
        {
            if (GameDataManager.Instance != null && GameDataManager.Instance.ButterflyData != null)
            {
                GameDataManager.Instance.ButterflyData.leafSkinCounter = value;

                // 每次发生数据变动（如通关时累加），立刻批准调用物理SaveData落盘，打扫干净战场！
                GameDataManager.Instance.ButterflyData.SaveData();
            }
        }
    }

    
    #region 属性封装
    private ChessPackInfo StagePackInfo;           // 关卡配置
    public ChessPackInfo PackInfos => StagePackInfo;
    public int CurrentStage
    {
        get => GameDataManager.Instance.UserData.CurrentChessStage;
        set => GameDataManager.Instance.UserData.CurrentChessStage = value;
    }
    #endregion
    
    #region 配置初始化方法

    public void Initialized()
    {
        CoroutineRunner.StartCoroutine(LoadDynamicConfig());
    }

    /// <summary>
    /// 加载当前语言的关卡配置
    /// </summary>
    private IEnumerator LoadDynamicConfig()
    {
        string levelConfigName =
            GameDataManager.Instance.UserData.ABName == "1" ? "ChessPackInfo_B" : "ChessPackInfo_A";
        // string levelConfigName = "ChessPackInfo_A";   

        StagePackInfo =
            AdvancedBundleLoader.SharedInstance.LoadScriptableObject(ToolUtil.GetLanguageBundle(), levelConfigName) as
                ChessPackInfo;
        if (StagePackInfo == null)
        {
            StagePackInfo =
                AdvancedBundleLoader.SharedInstance.LoadScriptableObject("chinesesimplified", levelConfigName) as
                    ChessPackInfo;
        }

        Debug.LogWarning("当前初始化的关卡包是 " + levelConfigName);

        TextAsset praiseCsvObj = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "cypz_Praise");
        if (praiseCsvObj != null)
        {
            LoadPraiseConfig(praiseCsvObj.text);
        }

        // 🌟 1. 各个在线配置的异步状态与账本数据准备
        bool isComboDone = false;
        bool isMechanicsDone = false;
        bool isStimulateDone = false;

        string comboCsvData = null;
        string mechanicsCsvData = null;
        string stimulateCsvData = null;
        // 🌟 2. 并发拉取机制：每个配置独立分配 Key，同时向服务器发出请求，不互相阻塞
        // A. 拉取连击配置
        CoroutineRunner.StartCoroutine(APIGateway.Instance.GameConfigApi.GetGameConfig("ComboConfig",
            onSuccess: (response) =>
            {
                comboCsvData = response.CsvString;
                isComboDone = true;
            },
            onError: (error) =>
            {
                isComboDone = true;
                Debug.Log("⚠️ 服务器拉取 ComboConfig 失败，准备使用本地资源兜底: " + error);
            }
        ));
        // B. 拉取核心机制配置 (冰块、花朵、树叶)
        CoroutineRunner.StartCoroutine(APIGateway.Instance.GameConfigApi.GetGameConfig("Mechanics",
            onSuccess: (response) =>
            {
                mechanicsCsvData = response.CsvString;
                isMechanicsDone = true;
            },
            onError: (error) =>
            {
                isMechanicsDone = true;
                Debug.Log("⚠️ 服务器拉取 Mechanics 失败，准备使用本地资源兜底: " + error);
            }
        ));
        // C. 拉取关卡完结横幅与鼓励词配置
        CoroutineRunner.StartCoroutine(APIGateway.Instance.GameConfigApi.GetGameConfig("Stimulate",
            onSuccess: (response) =>
            {
                stimulateCsvData = response.CsvString;
                isStimulateDone = true;
            },
            onError: (error) =>
            {
                isStimulateDone = true;
                Debug.Log("⚠️ 服务器拉取 Stimulate 失败，准备使用本地资源兜底: " + error);
            }
        ));
        // 🌟 3. 统一收网守候：用最大时间窗口安全等待所有线上的数据结账
        float timeout = 5f;
        while ((!isComboDone || !isMechanicsDone || !isStimulateDone) && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        // 🌟 4. 数据落地清算与安全退路（Fallbacks）
        // ======= A. 结算连击配置 =======
        if (string.IsNullOrEmpty(comboCsvData))
        {
            TextAsset comboCsvObj = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "cypz_ComboConfig");
            comboCsvData = comboCsvObj?.text;
        }

        if (!string.IsNullOrEmpty(comboCsvData))
        {
            LoadComboConfig(comboCsvData);
        }
        else
        {
            Debug.LogError("严重错误：连击配置表（在线/本地）全部加载失败，请核对资源名！");
        }

        // ======= B. 结算核心游戏机制（冰/花/叶） =======
        if (string.IsNullOrEmpty(mechanicsCsvData))
        {
            TextAsset mechainCsvObj = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "cypz_Mechanics");
            mechanicsCsvData = mechainCsvObj?.text;
        }

        if (!string.IsNullOrEmpty(mechanicsCsvData))
        {
            LoadMechainConfig(mechanicsCsvData);
        }
        else
        {
            Debug.LogError("严重错误：游戏机制配置（冰/花/叶）（在线/本地）全部加载失败！");
        }

        // ======= C. 结算结算横幅与鼓励词 =======
        if (string.IsNullOrEmpty(stimulateCsvData))
        {
            TextAsset stimulateCsvObj = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "cypz_Stimulate");
            stimulateCsvData = stimulateCsvObj?.text;
        }

        if (!string.IsNullOrEmpty(stimulateCsvData))
        {
            LoadStimulateRuleConfig(stimulateCsvData);
        }
        else
        {
            Debug.LogError("严重错误：关卡完成鼓励词配置（在线/本地）全部加载失败！");
        }

        yield return null;
        // 5. 组装完毕，拉起当前进度的关卡沙盒数据
        SetStageData(GameDataManager.Instance.UserData.CurrentChessStage);
    }

    #endregion

    /// <summary>
    /// 解析 拼字玩法配置表 - Praise（局内正反馈）_2.csv
    /// </summary>
    private void LoadPraiseConfig(string csvText)
    {
        PraiseConfigDict.Clear();

        if (string.IsNullOrEmpty(csvText))
        {
            Debug.LogError("Praise 配置表内容为空！");
            return;
        }

        // 使用现有的工具类按行分割
        List<string> lines = ToolUtil.SplitCsvLines(csvText);

        // 假设第0行是中文说明，第1行是英文变量名，真实数据从第2行开始
        for (int i = 2; i < lines.Count; i++)
        {
            // 核心：复用现有的双引号剥离与逗号切割方法
            string[] fields = ParseCsvLineAndCleanQuotes(lines[i]);

            // 健壮性防御：跳过彻底的空行或无效行
            if (fields == null || fields.Length < 6 || string.IsNullOrEmpty(fields[0])) continue;

            try
            {
                PraiseConfig config = new PraiseConfig();
                // 1. 解析基础数值
                int.TryParse(fields[0], out config.FeedbackID);
                int.TryParse(fields[1], out config.Priority);
                int.TryParse(fields[2], out config.BannerStyle);
                float.TryParse(fields[3], out config.Probability);
                float.TryParse(fields[4], out config.TimeWindow);
                // 2. 解析轮播文案 (假设策划是用分号 ; 隔开的多个Key)
                if (!string.IsNullOrEmpty(fields[5]))
                {
                    config.TextLoop = fields[5].Split(new char[] { ';', '_' }, StringSplitOptions.RemoveEmptyEntries);
                }
                else
                {
                    config.TextLoop =  Array.Empty<string>(); // 兜底空数组防空指针
                }
                PraiseConfigDict[config.FeedbackID] = config;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"解析 拼字玩法配置表 - Praise（局内正反馈）_2.csv 第 {i} 行时崩溃! 错误: {ex.Message}");
            }
        }

        Debug.Log($"[配置加载] 局内正反馈表加载完毕，共解析 {PraiseConfigDict.Count} 条规则。 " +JsonConvert.SerializeObject(PraiseConfigDict));
    }

    /// <summary>
    /// 加载并解析连击配置表 CSV (需要在游戏初始化时调用)
    /// </summary>
    /// <param name="csvText">CSV 文件的纯文本内容</param>
    private void LoadComboConfig(string csvText)
    {
        _comboConfigDict.Clear();
        _reduceConfigDict.Clear(); // 记得清空旧的扣分字典

        // 按行分割，支持不同操作系统的换行符
        string[] lines = csvText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // 从索引 2 开始遍历 (跳过第0行中文表头 和 第1行英文表头)
        for (int i = 2; i < lines.Length; i++)
        {
            string[] cols = lines[i].Split(',');
            if (cols.Length >= 3) // 至少要有状态、连击数、数值
            {
                string state = cols[0].Trim().ToLower();
                int combo = int.Parse(cols[1]);
                int num = int.Parse(cols[2]);

                // 解析时间窗口 (如果为空或0，默认给个极大值代表无限制)
                int timeLag = 999999;
                if (cols.Length >= 4 && !string.IsNullOrEmpty(cols[3]))
                {
                    int.TryParse(cols[3], out timeLag);
                }

                ComboConfig config = new ComboConfig
                {
                    State = state,
                    Combo = combo,
                    Num = num,
                    TimeLag = timeLag
                };
                // 👇 分门别类存入不同的字典
                if (state == "add") _comboConfigDict[combo] = config;
                else if (state is "reduce" or "sub") _reduceConfigDict[combo] = config;
            }
        }

        Debug.Log($"连击配置解析完成后: {JsonConvert.SerializeObject(_comboConfigDict)}");
    }

    /// <summary>
    /// 新玩法配置获取 (冰块、花朵、树叶)
    /// </summary>
    private void LoadMechainConfig(string text)
    {
        string[] lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line))
            .ToArray();

        string[] cols = lines.Last().Split(',', StringSplitOptions.RemoveEmptyEntries);

        // 定义一个安全的布尔值解析局部方法 (C# 7.0+)
        bool TryParseBool(string[] arr, int index, bool defaultValue = false)
        {
            if (arr == null || index < 0 || index >= arr.Length)
                return defaultValue; // 防止 IndexOutOfRangeException

            string val = arr[index].Trim().ToLower();

            // 兼容配置表常见的 1/0 写法
            if (val == "1" || val == "true") return true;
            if (val == "0" || val == "false") return false;

            // 标准 TryParse 后备
            if (bool.TryParse(val, out bool result)) return result;

            return defaultValue; // 无法解析时返回默认值，防止崩溃
        }

        string[] opens = cols[0].Split("_");
        if (cols.Length > 3)
        {
            IceConfig = new IceConfig
            {
                IsOpen = TryParseBool(opens, 0),
                Fixed = new Dictionary<int, int>(),
                CycleLevels = new List<Interval>(),
                Degree = new Dictionary<int, int>()
            };
            string[] degree = cols[1].Split('_');
            for (int i = 0; i < degree.Length; i++)
            {
                IceConfig.Degree.Add(i, int.Parse(degree[i]));
            }

            string[] levels = cols[2].Split(';');
            string[] first = levels[0].Split('_');
            IceConfig.FirstLevel = int.Parse(first[0]);
            IceConfig.FirstDegree = int.Parse(first[1]);
            for (int i = 1; i < levels.Length; i++)
            {
                degree = levels[i].Split('_');
                IceConfig.Fixed.Add(int.Parse(degree[0]), int.Parse(degree[1]));
            }

            string[] cycles = cols[3].Split(';');
            for (int i = 0; i < cycles.Length; i++)
            {
                string[] mode = cycles[i].Split('#');
                degree = mode[1].Split("*");
                string[] steps = degree[0].Split('_');
                Interval interval = new Interval
                {
                    Mode = int.Parse(mode[0]),
                    Degree = int.Parse(degree[1]),
                    Start = int.Parse(steps[0])
                };
                if (steps.Length >= 2 && int.TryParse(steps[1], out int end))
                    interval.End = end;
                else interval.End = int.MaxValue;
                IceConfig.CycleLevels.Add(interval);
            }

            Debug.Log("解析完成后的冰块配置: " + JsonConvert.SerializeObject(IceConfig));
        }

        if (cols.Length >= 5)
        {
            LeafConfig = new LeafConfig
            {
                IsOpen = TryParseBool(opens, 1),
                FirstLevel = int.Parse(cols[4]),
                CycleLevels = new List<int>(),
                Rewards = new List<LeafReward>()
            };
            string[] cycles = cols[5].Split('_');
            for (int i = 0; i < cycles.Length; i++)
            {
                LeafConfig.CycleLevels.Add(int.Parse(cycles[i]));
            }

            string[] rewardGroups = cols[6].Split('_');
            HashSet<(string NumberRaw, int Type)> seen = new HashSet<(string, int)>();
            foreach (string groupStr in rewardGroups)
            {
                if (string.IsNullOrWhiteSpace(groupStr)) continue;
                string[] rewards = groupStr.Split(';');

                foreach (string r in rewards)
                {
                    string[] parts = r.Split('#');
                    if (parts.Length < 2) continue;

                    string[] rewardValue = parts[1].Split('*');
                    if (rewardValue.Length < 2) continue;

                    int type = int.Parse(rewardValue[1]);
                    LeafReward leafReward = new LeafReward
                    {
                        Type = type,
                        Value = int.Parse(rewardValue[0])
                    };

                    // 解析 Number 字段（支持 n、n-1 等）
                    string numStr = parts[0];
                    if (int.TryParse(numStr, out int num))
                    {
                        leafReward.Number = num;
                    }
                    else if (numStr == "n")
                    {
                        leafReward.Number = -1;
                    }
                    else if (numStr.StartsWith("n-") && int.TryParse(numStr.Substring(2), out int delta))
                    {
                        leafReward.Number = -delta;
                    }
                    else
                    {
                        throw new FormatException($"无法解析的数量: {numStr}");
                    }

                    var key = (numStr, type);
                    if (seen.Contains(key)) continue;
                    seen.Add(key);
                    LeafConfig.Rewards.Add(leafReward);
                }
            }

            Debug.Log("解析完成后的树叶配置: " + JsonConvert.SerializeObject(LeafConfig));
        }

        if (cols.Length >= 9)
        {
            string[] levels = cols[9].Split(';');
            string[] first = levels[0].Split('_');
            string[] removes = cols[8].Split("_");
            FlowerConfig = new FlowerConfig
            {
                IsOpen = TryParseBool(opens, 2),
                FirstLevel = int.Parse(first[0]),
                FirstDegree = int.Parse(first[1]),
                InitNumber = int.Parse(removes[0]),
                MinNumber = int.Parse(removes[1]),
                Degree = new Dictionary<int, int>(),
                Fixed = new Dictionary<int, int>(),
                CycleLevels = new List<Interval>(),
            };
            for (int i = 1; i < levels.Length; i++)
            {
                string[] next = levels[i].Split('_');
                FlowerConfig.Fixed.Add(int.Parse(next[0]), int.Parse(next[1]));
            }

            string[] degrees = cols[7].Split("_");
            for (int i = 0; i < degrees.Length; i++)
            {
                FlowerConfig.Degree.Add(i, int.Parse(degrees[i]));
            }

            string[] cycles = cols[10].Split(';');
            for (int i = 0; i < cycles.Length; i++)
            {
                string[] mode = cycles[i].Split('#');
                string[] degree = mode[1].Split("*");
                string[] steps = degree[0].Split('_');
                Interval interval = new Interval
                {
                    Mode = int.Parse(mode[0]),
                    Degree = int.Parse(degree[1]),
                    Start = int.Parse(steps[0]),
                };
                if (steps.Length >= 2 && int.TryParse(steps[1], out int end))
                    interval.End = end;
                else interval.End = int.MaxValue;

                FlowerConfig.CycleLevels.Add(interval);
            }

            Debug.Log("解析完成后的花朵配置: " + JsonConvert.SerializeObject(FlowerConfig));
        }
    }

    /// <summary>
    ///  StimulateRule 结算横幅激励词规则
    /// </summary>
    private void LoadStimulateRuleConfig(string assetText)
    {
        StimulateRules.Clear();
        List<string> lines = ToolUtil.SplitCsvLines(assetText);
        for (int i = 2; i < lines.Count; i++)
        {
            string[] fields = ParseCsvLineAndCleanQuotes(lines[i]);
            // 健壮性防御：如果当前行是彻底的空行，直接跳过
            if (fields == null || fields.Length == 0 || string.IsNullOrEmpty(fields[0])) continue;
            try
            {
                string[] banners = fields[0].Split(';', StringSplitOptions.RemoveEmptyEntries);
                StimulateRuleConfig stimulateRule = new StimulateRuleConfig
                {
                    BannerTypes = new BannerType[banners.Length], TitleRate = 0, StimulateRate = 0,
                };
                for (int j = 0; j < banners.Length; j++)
                {
                    string[] bType = banners[j].Split('_');
                    stimulateRule.BannerTypes[j] = new BannerType
                    {
                        Number = int.Parse(bType[0]),
                        Rate = int.Parse(bType[1])
                    };
                }

                string[] tRates = fields[1].Split(';', StringSplitOptions.RemoveEmptyEntries);
                if (tRates.Length >= 2)
                {
                    // 🌟 修复 2：优雅地切出 _ 后面的数字，替代危险的 Substring(-1)
                    stimulateRule.TitleRate = int.Parse(tRates[0].Split('_')[1]);
                    stimulateRule.StimulateRate = int.Parse(tRates[1].Split('_')[1]);
                }

                stimulateRule.ScatterFlowers = (fields[2] == "1"); // 如果填1表示开启撒花
                stimulateRule.Priority = string.IsNullOrEmpty(fields[3]) ? 0 : int.Parse(fields[3]);
                stimulateRule.Type = string.IsNullOrEmpty(fields[4]) ? 0 : int.Parse(fields[4]);
                if (!string.IsNullOrEmpty(fields[5]))
                {
                    string[] percents = fields[5].Split('_');
                    if (percents.Length >= 2)
                    {
                        stimulateRule.ZenPercent = new[] { int.Parse(percents[0]), int.Parse(percents[1]) };
                    }
                }

                stimulateRule.TitleKey = fields[6];
                stimulateRule.PhraseKey = fields[7];
                stimulateRule.EmojiKey = fields[8];
                stimulateRule.LongTextKey = fields[9];
                StimulateRules.Add(stimulateRule);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"解析第 {i} 行数据时崩溃! 内容: {lines[i]} | 错误: {ex.Message}\n{ex.StackTrace}");
            }
        }

        Debug.Log("鼓励词配置解析完成! " + JsonConvert.SerializeObject(StimulateRules));
    }

    /// <summary>
    /// 辅助方法：完美切分 CSV 列，并剥离外围双引号，同时保留单元格内容中的换行与逗号
    /// </summary>
    private string[] ParseCsvLineAndCleanQuotes(string line)
    {
        var list = new List<string>();
        var cur = new System.Text.StringBuilder();
        bool inQuote = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    cur.Append('"'); // 转义双引号
                    i++;
                }
                else
                {
                    inQuote = !inQuote; // 只切换状态，外层包裹的引号不录入内容
                }
            }
            else if (c == ',' && !inQuote)
            {
                list.Add(cur.ToString().Trim());
                cur.Clear();
            }
            else
            {
                cur.Append(c);
            }
        }

        list.Add(cur.ToString().Trim());
        return list.ToArray();
    }
}