using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using System.Linq;

public class FishaiInfoItem
{
    public int id;
    //轮次
    public int round;
    //时间系数
    public float timeFactor;
    public int wordCount;
    public float minTimer;
}

public class FishAwardItem
{
    public int roundid;
    //第一名奖励内容
    public List<List<int>> rewardOne;
    //第二名奖励内容
    public List<List<int>> rewardTwo;
    //第三名奖励内容
    public List<List<int>> rewardThree;
}

/// <summary>
/// 竞速回合比赛中获取排名
/// </summary>
public class FishRankInfo
{
    public bool IsPlayer { get; set; }
    public string name { get; set; }
    public int Rank { get; set; }
}

public class FishInfoController : MonoBehaviour
{
    #region 单例模式
    public static FishInfoController Instance;
    #endregion

    #region 核心字段
    private List<FishaiInfoItem> _fishaiInfoItems = new List<FishaiInfoItem>();
    private List<FishAwardItem> fishAwardItems = new List<FishAwardItem>();
    private readonly HashSet<int> _usedNumbers = new HashSet<int>();
    private readonly Random _rng = new Random();
    private readonly object _syncLock = new object();
    private int _globalPlayerCount;
    public event System.Action<string> OnFishTimeUpdated; // 定义事件
    public event System.Action<bool> OnFishTimeBtnCanHide; // 定义事件
    [HideInInspector] public DashCompetition dashparent;

    public event System.Action OnFishMatchOver; // 定义事件
    #endregion

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 保持广告管理器在场景切换时不销毁
        }
        else
        {
            Destroy(gameObject);
        }       
    }
  
    private void Start()
    {
        InitializeData();

        UpdateFishTime();
    }
    
    public void UpdateFishTime()
    {
        StartCoroutine(WaitUpdateFishTime());
    }

    public IEnumerator WaitUpdateFishTime()
    {
        yield return new WaitForSeconds(0.8f);
        if(!string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.cloestime) )
            StartCoroutine(UpdateTime());
    }

    private void InitializeData()
    {
        // 从资源管理器获取数据（需替换实际数据源）
        TextAsset csvData = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo","MatchRobatTable");
        TextAsset awardcsvData = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo","MatchConfig");
       
        if (csvData != null)
        {
            ParseFishaiItems(csvData.text);
        }
        else
        {
            Debug.LogError("Failed to load CSV data.");
        }
        
         
        if (awardcsvData != null)
        {
            ParseFishAwardItems(awardcsvData.text);
        }
        else
        {
            Debug.LogError("Failed to load CSV data.");
        }
    } 

    #region 公开接口

    public List<List<int>> GetAwardItems()
    {
        FishAwardItem awardItem = fishAwardItems.Find((item)=>item.roundid==GameDataManager.Instance.FishUserSave.curround);
        switch (GameDataManager.Instance.FishUserSave.rank)
        {
            case 1:
                return awardItem.rewardOne;
            case 2:
                return awardItem.rewardTwo;
            case 3:
                return awardItem.rewardThree;
        }
        
        return null;
    }

    public List<FishRankInfo> RoundResultFishRank()
    {
        int playerWordProgress = GameDataManager.Instance.FishUserSave.Puzzleprogress;
        int userUseTime = GameDataManager.Instance.FishUserSave.updatePuzzleusetime;
        int round= GameDataManager.Instance.FishUserSave.curround;
        FishaiInfoItem infoItem = GetCurrFishItem(round);
        AppGameSettings.FishTargetWordCount=infoItem.wordCount;
        int fishTargetWordCount =  AppGameSettings.FishTargetWordCount;
        // 合并符合条件的AI和玩家数据，并包含usetime
        var query = GameDataManager.Instance.FishUserSave.aiSaveDatas
            .Where(ai => ai.Puzzleprogress >= fishTargetWordCount)
            .Select(ai => new { IsPlayer = false, UseTime = ai.updatePuzzleusetime, Data = ai })
            .ToList();

        // 添加玩家数据（如果符合条件）
        if (playerWordProgress >= fishTargetWordCount)
        {
            query.Add(new { IsPlayer = true, UseTime = userUseTime, Data = (FishAISaveData)null });
        }

        int rank = 0;
        var result = query
            .OrderBy(x => x.UseTime)                  // 按usetime升序排列（用时短者靠前）
            .GroupBy(x => x.UseTime)                  // 按usetime分组，共享同一排名
            .SelectMany(g => g.Select((x, i) =>
            {
                rank +=1; // 处理并列排名

                // 更新AI数据的排名
                if (!x.IsPlayer)
                {
                    x.Data.rank = rank;
                }
                // 玩家排名存储在FishUserSave中
                else
                {
                    GameDataManager.Instance.FishUserSave.rank = rank;
                }

                return new FishRankInfo
                {
                    IsPlayer = x.IsPlayer,
                    name = x.IsPlayer ? "" : x.Data.ainame,
                    Rank = rank
                };
            }))
            .ToList();

        var leftquery = GameDataManager.Instance.FishUserSave.aiSaveDatas
           .Where(ai => ai.Puzzleprogress < fishTargetWordCount&&ai.Puzzleprogress>0)
            .Select(ai => new { IsPlayer = false, leftword = fishTargetWordCount - ai.Puzzleprogress,usetime=ai.updatePuzzleusetime, Data = ai })
           .ToList();

        if (playerWordProgress < fishTargetWordCount&&playerWordProgress>0)
        {
            leftquery.Add(new { IsPlayer = true, leftword = fishTargetWordCount- playerWordProgress,usetime= userUseTime, Data = (FishAISaveData)null });
        }


        leftquery.OrderBy(x => x.usetime).ToList();

        leftquery.OrderBy(x => x.leftword)
             .GroupBy(x => x.leftword)                  // 按wordprogress分组，共享同一排名
            .SelectMany(g => g.Select((x, i) =>           
            {
                rank += 1; // 处理并列排名

                // 更新AI数据的排名
                if (!x.IsPlayer)
                {
                    x.Data.rank = rank;
                }
                // 玩家排名存储在FishUserSave中
                else
                {
                    GameDataManager.Instance.FishUserSave.rank = rank;
                }

                return new FishRankInfo
                {
                    IsPlayer = x.IsPlayer,
                    name = x.IsPlayer ? "" : x.Data.ainame,
                    Rank = rank
                };
            }))
            .ToList();

        return result;
    }
    
    /// <summary>
    /// 结算页面是否限时竞速进度动画
    /// </summary>
    /// <returns></returns>
    public bool IsShowFishProgressAnim()
    {
        if (GameDataManager.Instance.FishUserSave.Puzzleprogress >= AppGameSettings.FishTargetWordCount)
        {
            return false;
        }
        
        int targetOver=GameDataManager.Instance.FishUserSave.aiSaveDatas.FindAll((item)=>item.Puzzleprogress >= AppGameSettings.FishTargetWordCount).Count;
        
        if(targetOver>=3)
            return false;
        
        if(string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.roundstarttime))
            return false;
        
        return true;
    }


    /// <summary>
    /// 竞速回合比赛是否结束
    /// </summary>
    /// <returns></returns>
    public bool RoundFishIsOver()
    {
        if (GameDataManager.Instance.FishUserSave.Puzzleprogress >= AppGameSettings.FishTargetWordCount)
        {
            return true;
        }
        
        int targetOver=GameDataManager.Instance.FishUserSave.aiSaveDatas.FindAll((item)=>item.Puzzleprogress >= AppGameSettings.FishTargetWordCount).Count;
        
        if(targetOver>=3)
           return true;
        
        return false;
    }

    /// <summary>
    /// 获取竞速功能是否开启
    /// </summary>
    /// <returns></returns>
    public bool GetOpenFishFunction()
    {
        bool isOpen = false;
        if (!string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.cloestime))
        {
            DateTime closetime = DateTime.Parse(GameDataManager.Instance.FishUserSave.cloestime);
            DateTime opentime = DateTime.Parse(GameDataManager.Instance.FishUserSave.opentime);
            DateTime today = DateTime.Today;
            int daysToSubtract = 0;
            //是否在活动期间
            if (closetime.Subtract(DateTime.Now).TotalMinutes > 0&&DateTime.Now.Subtract(opentime).TotalMinutes > 0)
            {
                isOpen = true;
            }
            else
            {
                //是否超过关闭时间
                if (closetime.Subtract(DateTime.Now).TotalMinutes <= 0)
                {
                    if(!string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.opentime))
                    {
                        TimeSpan ts = closetime.Subtract(opentime);// 将字符串转换为 DateTime
                        //FirebaseManager.Instance.ActivityComplete("竞速活动", today.ToString(),0);
                    }
                    
                    //if (closetime.DayOfWeek == DayOfWeek.Monday)
                    //{
                    //    GameDataManager.MainInstance.FishUserSave.opentime = closetime.AddDays(1).ToString();
                    //}
                    //else
                    //{
                        // 计算本周一的日期
                        daysToSubtract = (today.DayOfWeek == DayOfWeek.Sunday) ? 6 : (int)today.DayOfWeek - (int)DayOfWeek.Monday;
               
                        GameDataManager.Instance.FishUserSave.opentime =today.AddDays(-daysToSubtract).ToString();
                    //}
                    // 计算本周五的日期（如果今天已经过了周五，则计算下周五）
                    DateTime closeTime = today.AddDays((DayOfWeek.Saturday - DateTime.Now.DayOfWeek + 7) % 7);
                    if (closeTime.Subtract(today).TotalMinutes > 0)
                    {
                        GameDataManager.Instance.FishUserSave.cloestime = closeTime.ToString();
                        GameDataManager.Instance.FishUserSave.roundstarttime = "";
                        GameDataManager.Instance.FishUserSave.matchCount = 0;
                        isOpen = true;
                    }                    
                }
                //是否结束回合
                else if (GameDataManager.Instance.FishUserSave.isRoundOver)
                {
                    if(!string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.opentime))
                    {
                        TimeSpan ts = closetime.Subtract(opentime);// 将字符串转换为 DateTime
                        //FirebaseManager.Instance.ActivityComplete("竞速活动", today.ToString(),0);
                    }
                    
                    //关闭时间为周一时，开启时间为周二（顺延一天）
                    // if (closetime.DayOfWeek == DayOfWeek.Monday)
                    // {
                    //     GameDataManager.MainInstance.FishUserSave.opentime = closetime.AddDays(1).ToString();
                    // }
                    // else
                    // {
                        // 计算本周一的日期
                        daysToSubtract = (today.DayOfWeek == DayOfWeek.Sunday) ? 6 : (int)today.DayOfWeek - (int)DayOfWeek.Monday;
               
                        GameDataManager.Instance.FishUserSave.opentime =today.AddDays(-daysToSubtract).ToString();
                    //}
                    // 计算本周五的日期（如果今天已经过了周五，则计算下周五）
                    DateTime closeTime = today.AddDays((DayOfWeek.Friday - DateTime.Now.DayOfWeek + 7) % 7);
                    GameDataManager.Instance.FishUserSave.cloestime =closeTime.ToString();
                    GameDataManager.Instance.FishUserSave.matchCount = 0;
                    isOpen = true;
                }
                else if (!string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.roundstarttime))
                {                   
                    isOpen = true;
                }
            }
        }
        
        return isOpen;
    }
    
    /// <summary>
    /// 获取指定轮次的竞速AI保存数据
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public List<FishAISaveData> GetRoundFishaiSaveItems(int roundindex)
    {
        List<FishAISaveData> fishAISaves = GameDataManager.Instance.FishUserSave.aiSaveDatas;
        if (fishAISaves.Count <= 0)
        {
            // 获取当前玩家真实关卡数
            int playerLevel = GameDataManager.Instance.UserData.CurrentHexStage;
            int maxLevel = playerLevel+100; // 从配置获取最大关卡
            
            List<FishaiInfoItem>  fishaiInfos = GetRoundFishaiItems(roundindex);
            foreach (var aInfo in fishaiInfos)
            {
                // 智能关卡生成系统
                int aiLevel = CalculateAILevel(
                    playerLevel: playerLevel,
                    maxLevel: maxLevel,
                    aiIndex: fishaiInfos.IndexOf(aInfo),
                    totalAI: fishaiInfos.Count,
                    roundIndex: roundindex
                );
                
                fishAISaves.Add(new FishAISaveData()
                {
                    aiid = aInfo.id,
                    iscliam = false,
                    rank = 0,
                    ainame = GeneratePlayerName(),
                    updatePuzzleusetime = 0,
                    ailevel = aiLevel,
                    Puzzleprogress = 0,
                });
            }
            Shuffle(fishAISaves);
        }
       
        return fishAISaves;
    }
    
    /// <summary>
    /// 随机获取名称
    /// </summary>
    /// <returns></returns>
    public string GeneratePlayerName()
    {
        // 使用锁确保线程安全（多线程环境下避免竞争条件）
        lock (_syncLock)
        {
            // 获取当前语言配置中名字的最大索引值
            int maxValue = MultilingualManager.Instance.GetNameLength();
    
            // 检查是否所有名字都已被使用（+1是因为Random.Next的上界是排他性的）
            if (_usedNumbers.Count >= maxValue + 1)
            {
                throw new InvalidOperationException("所有名字已用完");
            }

            int number;
            int attempts = 0;
            const int maxAttempts = 10000; // 最大尝试次数限制（防止死循环）
            string currentPlayerName = GameDataManager.Instance.UserData.UserName; // 缓存当前用户名

            // 生成不重复且不等于当前用户名的随机名字
            while (true)
            {
                number = _rng.Next(0, maxValue + 1);
                if (++attempts > maxAttempts)
                {
                    throw new InvalidOperationException($"无法在{maxAttempts}次尝试内生成有效名称");
                }

                if (!_usedNumbers.Contains(number)&&
                    MultilingualManager.Instance.GetName(number) != currentPlayerName)
                {
                    break;
                }
            }

            // 记录已使用的索引
            _usedNumbers.Add(number);
        
            // 直接返回缓存结果（避免二次查询）
            return MultilingualManager.Instance.GetName(number);
        }
    }

    /// <summary>
    /// 检查竞速AI更新关卡及词语数量
    /// </summary>
    /// <param name="aiid"></param>
    public void CheckAIPassLevel(int aiid,Action callback)
    {
        FishAISaveData aiSaveData = GameDataManager.Instance.FishUserSave.aiSaveDatas
            .Find(item => item.aiid == aiid);
        
        int round= GameDataManager.Instance.FishUserSave.curround;
        FishaiInfoItem infoItem = GetCurrFishItem(round);
        int fishTargetWordCount = infoItem?.wordCount ?? AppGameSettings.FishTargetWordCount;
        
        while (!RoundFishIsOver()&& aiSaveData!=null)
        {
            StageInfo aiLevelInfo= StageHexController.Instance.CreateStageInfo(aiSaveData.ailevel,true);
            float needtime= aiLevelInfo.Puzzles.Count/(float)fishTargetWordCount*GetAiTargetTime(aiid);
            needtime += aiSaveData.updatePuzzleusetime;
            
            DateTime rtargetTime = DateTime.Parse(GameDataManager.Instance.FishUserSave.roundstarttime).AddSeconds(needtime);

            if (aiSaveData.Puzzleprogress<fishTargetWordCount)
            {
                callback?.Invoke();
            }
            
            if (rtargetTime <= DateTime.Now&&aiSaveData.Puzzleprogress<fishTargetWordCount)
            {
                aiSaveData.UpdateFishProgress(aiLevelInfo.Puzzles.Count);
                aiSaveData.UpdatePassLvTime((int)needtime);
                if (aiSaveData.Puzzleprogress >= fishTargetWordCount)
                {
                    RoundResultFishRank();
                    break;
                }
            }
            else 
            {
                break;
            }
            
        }
       
    }

    /// <summary>
    /// 获取AI到达终点的时间
    /// </summary>
    public float GetAiTargetTime(int aiid)
    {
        float time = 0;
        FishaiInfoItem aItem = _fishaiInfoItems.Find((item)=>item.id==aiid);
        time=GetLevelUseAverageTime()*aItem.timeFactor*10;
        return time;
    }

    /// <summary>
    /// 获得关卡用时平均值
    /// </summary>
    public float GetLevelUseAverageTime()
    {
        int sum = 0, levelcount = 0;
        float averageTime = 0;
        foreach (var time in GameDataManager.Instance.UserData.passLevelUseTime.Values)
        {
            if(time<=60*60)
            {
                levelcount++;
                sum += time;
            }
            if(levelcount>=9) break;
        }
        
        if(levelcount>0)
            averageTime= sum /(float) levelcount+40;
        
        if (averageTime <= 0) averageTime = 3 * 60;
        
        return averageTime;
    }


    public void FishMatchOver()
    {
        OnFishMatchOver?.Invoke();
    }
    
    private string Gettime()
    {
        if(string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.cloestime)) return "";
        DateTime close = DateTime.Parse(GameDataManager.Instance.FishUserSave.cloestime); // 获取当天的 00:00

        // 计算剩余时间
        TimeSpan timeRemaining = close - DateTime.Now;
        if (timeRemaining.TotalMinutes > 0)
        {
            string time = UIUtilities.GetDateDayStyle(timeRemaining);
            OnFishTimeUpdated?.Invoke(time); // 触发事件，通知所有订阅者
        }
        else
        {
            OnFishTimeBtnCanHide?.Invoke(false);
            return "";
        }
        // 输出倒计时
        return timeRemaining.TotalMinutes.ToString();
    }
    
    private IEnumerator UpdateTime()
    {
        yield return new WaitForSeconds(0.2f); // 等待 10 秒
        string time = Gettime();
       
        while (true)
        {
            time = Gettime();
            if (string.IsNullOrEmpty(time))
            {
                break; // 如果时间为空，退出循环
            }
            
            yield return new WaitForSeconds(1f); // 等待 10 秒
        }
    }

    #endregion

    #region 核心逻辑
    
    
    // 智能关卡生成算法
    private int CalculateAILevel(int playerLevel, int maxLevel, int aiIndex, int totalAI, int roundIndex)
    {
        // 参数配置（可通过ScriptableObject调整）
        float baseRange = Mathf.Clamp(playerLevel * 0.3f, 3, 8); // 动态波动范围
        float progressionBias = 0.4f * (roundIndex + 1);        // 轮次难度加成
        float positionWeight = 0.25f;                           // 列表位置影响系数
    
        // 正态分布随机（μ=0, σ=baseRange/2）
        float gaussian = Mathf.Clamp(
            GaussianRandom(0, baseRange/2), 
            -baseRange, 
            baseRange
        );
    
        // 基于位置的线性增长
        float positionFactor = (float)aiIndex / totalAI * positionWeight;
    
        // 最终关卡计算
        float calculatedLevel = playerLevel 
                                + gaussian 
                                + positionFactor * playerLevel 
                                + progressionBias;
        // 生态限制
        return Mathf.Clamp(
            Mathf.RoundToInt(calculatedLevel),
            1,
            Mathf.Min(maxLevel, playerLevel + 8) // 最高不超过玩家+8关
        );
    }

    // 高斯随机生成器
    private float GaussianRandom(float mean, float stdDev)
    {
        float u1 = 1.0f -  UnityEngine.Random.value;;
        float u2 = 1.0f -  UnityEngine.Random.value;;
        float randStdNormal = Mathf.Sqrt(-2 * Mathf.Log(u1)) * 
                              Mathf.Sin(2 * Mathf.PI * u2);
        return mean + stdDev * randStdNormal;
    }
    
    /// <summary>
    /// 获取指定轮次的竞速AI数据
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    private List<FishaiInfoItem> GetRoundFishaiItems(int roundindex)
    {
        List<FishaiInfoItem>  fishaiInfos= _fishaiInfoItems.FindAll((item)=>roundindex==item.round);
        Shuffle(fishaiInfos);
        return fishaiInfos;
    }
    
    /// <summary>
    /// 获取指定轮次的竞速AI数据
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public FishaiInfoItem GetCurrFishItem(int index)
    {
        return _fishaiInfoItems?.FirstOrDefault((item) => item.id == index);
    }
    
    private void Shuffle<T>(IList<T> list)
    {
        lock (_syncLock)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = _rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
    
    #endregion

    #region 数据解析
    private void ParseFishaiItems(string data)
    {
        var lines = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 2; i < lines.Length; i++)
        {
            var fields = lines[i].Split(',');
            if (fields.Length < 2) continue;
            
            int id = int.Parse(fields[0].Trim());
            int round = int.Parse(fields[1].Trim());
            float timeFactor = float.Parse(fields[2].Trim());
            int wordCount = int.Parse(fields[3].Trim());
            float minTimer = float.Parse(fields[4].Trim());

            _fishaiInfoItems.Add(new FishaiInfoItem
            {
                id = id,
                round = round,
                timeFactor = timeFactor,
                wordCount = wordCount,
                minTimer = minTimer
            });
        }
    }
    
    private void ParseFishAwardItems(string data)
    {
        string[] lines = data.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 2; i < lines.Length; i++) // 从第一行开始，跳过标题行
        {
            string[] fields = lines[i].Split(',');

            if (fields.Length >= 3) // 确保有足够的字段
            {
                int id = int.Parse(fields[0].Trim());
        
                // 解析 productContent
                List<List<int>> productContent = new List<List<int>>();
                // 先用 # 分隔
                string[] groups = fields[1].Split('#');
                foreach (string group in groups)
                {
                    // 用 ; 分隔并转换为 List<int>
                    List<int> numbers = new List<int>();
                    string[] sinitems = group.Split(';');
                    foreach (string temp in sinitems)
                    {
                        if (int.TryParse(temp, out int number)) // 解析为整数
                        {
                            numbers.Add(number);
                        }
                    }
                    productContent.Add(numbers); // 添加到主列表
                }
          
                // 解析 productContent
                List<List<int>> productContent2 = new List<List<int>>();
                // 先用 # 分隔
                string[] groups2 = fields[2].Split('#');
                foreach (string group in groups2)
                {
                    // 用 ; 分隔并转换为 List<int>
                    List<int> numbers = new List<int>();
                    string[] sinitems = group.Split(';');
                    foreach (string temp in sinitems)
                    {
                        if (int.TryParse(temp, out int number)) // 解析为整数
                        {
                            numbers.Add(number);
                        }
                    }
                    productContent2.Add(numbers); // 添加到主列表
                }
                
                // 解析 productContent
                List<List<int>> productContent3 = new List<List<int>>();
                // 先用 # 分隔
                string[] groups3 = fields[2].Split('#');
                foreach (string group in groups3)
                {
                    // 用 ; 分隔并转换为 List<int>
                    List<int> numbers = new List<int>();
                    string[] sinitems = group.Split(';');
                    foreach (string temp in sinitems)
                    {
                        if (int.TryParse(temp, out int number)) // 解析为整数
                        {
                            numbers.Add(number);
                        }
                    }
                    productContent3.Add(numbers); // 添加到主列表
                }

                fishAwardItems.Add(new FishAwardItem
                {
                    roundid = id,
                    rewardOne = productContent,
                    rewardTwo = productContent2,
                    rewardThree = productContent3,
                });
            }
            else
            {
                Debug.LogWarning($"Skipping line {i + 1}: Not enough fields.");
            }
        }
    }

    public void Dispose()
    {
        //_updateTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
    #endregion
}