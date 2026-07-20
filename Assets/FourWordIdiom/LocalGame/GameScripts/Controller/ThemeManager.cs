using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Middleware;
using Newtonsoft.Json;
using UnityEngine;

public class ThemeDataItem
{
    /// <summary>
    /// 主题id
    /// </summary>
    public int id;
    /// <summary>
    /// 主题图片名称
    /// </summary>
    public string iconName;
    //主题名称
    public string themeName;
}

public class ThemeSaveItem
{
    /// <summary>
    /// 主题id
    /// </summary>
    public int id;
    /// <summary>
    /// 是否获得
    /// </summary>
    public bool isGet;
}

public class ThemeRangeData
{
    /// <summary>
    /// 主题范围
    /// </summary>
    public Vector2Int themeRange;
    /// <summary>
    /// 需要的金箔数量
    /// </summary>
    public int needGoldLeaf;
}



public class ThemeManager : Singleton<ThemeManager>
{
    private List<ThemeDataItem> themeDataItems;
    private List<ThemeRangeData> ThemeRangeDatas=new List<ThemeRangeData>();
    /// <summary>
    /// 金箔首次出现关卡
    /// </summary>
    public int golfFirstLevel;
    /// <summary>
    /// 关卡个位数相同时出现金箔
    /// </summary>
    public Vector2Int levelGeNum;
    /// <summary>
    /// 关卡内出现金箔个数范围
    /// </summary>
    public Vector2Int CountRange;
    public GameObject themeButton;
    
    private bool _isSkinRedPointActive = false;   // 当前红点是否显示
    public bool IsSkinRedPointActive => _isSkinRedPointActive;
    
    // 新增红点状态变更事件（参数 true=显示红点，false=隐藏）
    public event Action<bool> OnSkinRedPointChanged;
    
    public event System.Action OnShowNewThemeBtnUI; 
    

    public override void Init()
    {
        TextAsset data = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "themes");
        TextAsset setdata = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "themeSetConfig");
        if (data != null)
        {
            ParseLimitItems(data.text);
        }
        else
        {
            Debug.LogError("Failed to load CSV data.");
        }
        
        if (setdata != null)
        {
            ConvertSetCSVToJSON(setdata.text);
        }
        else
        {
            Debug.LogError("Failed to load CSV data.");
        }
    }
   
 
    void ParseLimitItems(string data)
    {
        // 将 CSV 数据转换为 JSON 格式
        ConvertCSVToJSON(data);

        // 现在limitItems列表中包含所有商品
        // Debug.Log("Limit items loaded: " + limitItems.Count);
    }
  

    void ConvertCSVToJSON(string data)
    {
        // 用于构建 JSON 字符串
        List<ThemeDataItem> items = new List<ThemeDataItem>();
        string[] lines = data.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 2; i < lines.Length; i++) // 从第一行开始，跳过标题行
        {
            string[] fields = lines[i].Split(',');

            if (fields.Length >= 3) // 确保有足够的字段
            {
                int id = int.Parse(fields[0].Trim());
                
                string iconName = fields[1].Trim();
                string themeName = fields[2].Trim();

                ThemeDataItem item = new ThemeDataItem
                {
                    id = id,
                    iconName = iconName,
                    themeName = themeName
                };
                items.Add(item);
            }
            else
            {
                Debug.LogWarning($"Skipping line {i + 1}: Not enough fields.");
            }
        }
      
        themeDataItems = items;
    }
    
    void ConvertSetCSVToJSON(string setdata)
    {
        // 用于构建 JSON 字符串
        string[] lines = setdata.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 2; i < lines.Length; i++) // 从第一行开始，跳过标题行
        {
            string[] fields = lines[i].Split(',');

            if (fields.Length >= 3) // 确保有足够的字段
            {
                int levelid = int.Parse(fields[0].Trim());
                
                string[] levelGeNums = fields[1].Split('_');
                string[] countRanges = fields[2].Split('_');
                
                // 第一阶段：fields[3] 格式 "OneRange 4;10"  → 主题 1~4，金箔 10
                ParseSingleRange(fields[3], isFirstStage: true);

                // 第二阶段：fields[4] 格式 "TwoRange 5_8;20" → 主题 5~8，金箔 20
                ParseSingleRange(fields[4], isFirstStage: false);

                // 第三阶段：fields[5] 格式 "ThreeRange 8;30" → 主题 9~无穷，金箔 30
                ParseSingleRange(fields[5], isFirstStage: false);

                golfFirstLevel = levelid;
                levelGeNum = new Vector2Int(int.Parse(levelGeNums[0]), int.Parse(levelGeNums[1]));
                CountRange =  new Vector2Int(int.Parse(countRanges[0]), int.Parse(countRanges[1]));
                
            }
            else
            {
                Debug.LogWarning($"Skipping line {i + 1}: Not enough fields.");
            }
        }
    }
    
    private void ParseSingleRange(string rawData, bool isFirstStage)
    {
        if (string.IsNullOrWhiteSpace(rawData))
            return;

       
        string[] rangeAndGold = rawData.Split(';');
        if (rangeAndGold.Length != 2)
        {
            Debug.LogWarning($"范围与金箔数量格式错误: {rawData}");
            return;
        }

        string rangeStr = rangeAndGold[0]; // "4" 或 "5_8" 或 "8"
        string goldStr = rangeAndGold[1];  // "10" 或 "20" 或 "30"

        if (!int.TryParse(goldStr, out int needGoldLeaf))
        {
            Debug.LogWarning($"金箔数量解析失败: {goldStr}");
            return;
        }

        Vector2Int themeRange;

        // 根据是否包含 '_' 判断是连续范围还是单一数字
        if (rangeStr.Contains('_'))
        {
            // 第二阶段：明确起止，例如 "5_8"
            string[] bound = rangeStr.Split('_');
            if (bound.Length == 2 &&
                int.TryParse(bound[0], out int start) &&
                int.TryParse(bound[1], out int end))
            {
                themeRange = new Vector2Int(start+1, end+1);
            }
            else
            {
                Debug.LogWarning($"范围格式错误: {rangeStr}");
                return;
            }
        }
        else
        {
            // 单一数字：根据阶段区分含义
            if (!int.TryParse(rangeStr, out int num))
            {
                Debug.LogWarning($"数字解析失败: {rangeStr}");
                return;
            }

            if (isFirstStage)
            {
                // 第一阶段：前 num 个主题 → 0 ~ num
                themeRange = new Vector2Int(2, num+1);
            }
            else
            {
                // 第三阶段：第 num 个主题之后 → num+1 到无穷
                // 用 int.MaxValue 表示无穷大，实际使用时判断主题编号 > start 即可
                themeRange = new Vector2Int(num+1, themeDataItems.Count);
            }
        }

        ThemeRangeDatas.Add(new ThemeRangeData
        {
            themeRange = themeRange,
            needGoldLeaf = needGoldLeaf
        });
    }
    
    
    /// <summary>
    /// 检查并更新皮肤入口红点状态
    /// </summary>
    public void CheckAndUpdateSkinRedPoint()
    {
        bool canDraw = CanDrawTheme();
        if (canDraw != _isSkinRedPointActive)
        {
            _isSkinRedPointActive = canDraw;
            OnSkinRedPointChanged?.Invoke(_isSkinRedPointActive);
        }
    }
    
    /// <summary>
    /// 判断当前是否可以进行主题抽取
    /// </summary>
    public bool CanDrawTheme()
    {
        int unlockedCount = GameDataManager.Instance.UserData.ThemeSaveItems.Count;
        if (unlockedCount >= themeDataItems.Count)
            return false;   // 所有主题已解锁
        
        ThemeRangeData rule = GetThemeRangeDataByThemeCount(unlockedCount);
        if (rule == null)
            return false;
        
        return GameDataManager.Instance.UserData.GoldLeaf >= rule.needGoldLeaf;
    }
    
    /// <summary>
    /// 皮肤入口被点击时调用（由UI按钮触发）
    /// </summary>
    public void OnSkinEntryClicked()
    {
        if (_isSkinRedPointActive)
        {
            _isSkinRedPointActive = false;
            OnSkinRedPointChanged?.Invoke(false);
        }
    }


    /// <summary>
    /// 是否能够获取主题
    /// </summary>
    /// <returns></returns>
    public bool IsCanGetThemes()
    {
        // 解锁主题数量
        int currentUnlocked = GameDataManager.Instance.UserData.ThemeSaveItems.Count;
        int totalThemes =themeDataItems.Count;
        
        return currentUnlocked < totalThemes;
    }
    
    
    /// <summary>
    /// 计算解锁所有主题需要的总金箔数量
    /// </summary>
    /// <param name="totalThemes">主题总数（即 themeDataItems.Count）</param>
    /// <returns>所需金箔总数</returns>
    public int CalculateTotalGoldLeafNeeded()
    {
        // 解锁主题数量
        int currentUnlocked = GameDataManager.Instance.UserData.ThemeSaveItems.Count;
        int totalThemes =themeDataItems.Count;
            
        // 如果主题总数 <= 2，无需抽奖
        if (totalThemes <= currentUnlocked)
            return 0;

        int totalGoldLeaf = 0;

        // 循环抽奖，每次抽奖获得一个新主题，直到所有主题解锁
        while (currentUnlocked < totalThemes)
        {
            // 获取当前已解锁主题数量对应的抽奖规则（一次抽奖需要的金箔数）
            ThemeRangeData rule = GetThemeRangeDataByThemeCount(currentUnlocked);
            if (rule == null)
            {
                Debug.LogError($"无法获取主题数量 {currentUnlocked} 对应的抽奖规则");
                return totalGoldLeaf; // 或抛出异常
            }

            // 累加本次抽奖消耗
            totalGoldLeaf += rule.needGoldLeaf;

            // 抽中新主题，解锁数量 +1
            currentUnlocked++;
        }

        return totalGoldLeaf;
    }
    
    
    /// <summary>
    /// 根据当前已有的主题数量，获取对应的金箔抽取规则
    /// </summary>
    /// <param name="themeCount">当前已解锁的主题总数（即 ThemeSaveItems.Count）</param>
    /// <returns>匹配的 ThemeRangeData，若未匹配到则返回 null</returns>
    public ThemeRangeData GetThemeRangeDataByThemeCount(int themeCount)
    {
        if (ThemeRangeDatas == null || ThemeRangeDatas.Count == 0)
        {
            Debug.LogError("ThemeRangeDatas 尚未初始化或为空，请先调用解析方法。");
            return null;
        }

        // 按顺序查找第一个满足主题编号范围的规则
        foreach (var data in ThemeRangeDatas)
        {
            if (themeCount >= data.themeRange.x && themeCount <= data.themeRange.y)
            {
                return data;
            }
        }

        // 理论上第三阶段的范围结束于 int.MaxValue，一定会匹配到；但防御性返回最后一个
        Debug.LogWarning($"未找到匹配主题数量 {themeCount} 的范围，返回最后一个规则。");
        return ThemeRangeDatas.LastOrDefault();
    }

    public List<ThemeDataItem> GetThemeDataItems()
    {
        return themeDataItems;
    }
    
    /// <summary>
    /// 触发显示新主题事件
    /// </summary>
    public void TriggerOnShowNewThemeBtnUI()
    {
        OnShowNewThemeBtnUI?.Invoke();
    }
    
    /// <summary>
    /// 返回指定主题id的数据
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public ThemeDataItem GetThemeDataItem(int id)
    {
        return themeDataItems.Find(x => x.id == id);
    }

    public bool CanShowThemeBtn()
    {
        return true;
    }
    
    /// <summary>
    /// 关卡内是否出现金箔
    /// </summary>
    /// <returns></returns>
    public bool CanShowGoldLeaf()
    {
        bool isRightlevel=false;
        
        int curlevel=GameDataManager.Instance.UserData.CurrentChessStage;
        int digit = curlevel % 10;
        if ((digit == levelGeNum.x || digit == levelGeNum.y)&&GameDataManager.Instance.UserData.GoldLeaf<CalculateTotalGoldLeafNeeded())
        {
            isRightlevel = true;
        }
        
        return GameDataManager.Instance.UserData.CurrentChessStage >=golfFirstLevel&&isRightlevel;
    }
    
    
}