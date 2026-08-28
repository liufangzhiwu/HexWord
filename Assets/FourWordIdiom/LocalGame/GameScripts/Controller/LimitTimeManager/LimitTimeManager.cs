using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Middleware;
using Newtonsoft.Json;
using UnityEngine;

public class LimitDataItem
{
    public int id;
    //奖励内容
    public List<List<int>> rewardContent;
    // 蝶蛹收集完后的替换奖励内容（与 rewardContent 一一对应，可能为 null）
    public List<List<int>> alternativeRewardContent;
    //需要的成语数
    public int num;
}


//对应限时奖励配置表中奖励配置批准中的奖励索引表示的类型
public enum LimitRewordType
{
    Coins,Butterfly,Tipstool,AutoComplete,Min5Double,Min15Double,RemoveAds,Remove7DayAds,Resettool,Pupas=11,
    ZenScore=12,GoldLeaf,Energy,HeadIcon,MonthlyGold,MonthlySilver,MonthlyBronze,
}

public class LimitTimeManager : Singleton<LimitTimeManager>
{
    private List<LimitDataItem> limitItems;
    public event Action<string> OnLimitTimeUpdated; // 定义事件
    public event Action<string> OnDailyTimeUpdated; // 定义事件
    public event Action OnLimitTimeBtnUI; // 定义事件
    public LimitDataItem CurlimitData;
    

    public override void Init()
    {
        TextAsset data = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "limittime");
        if (data != null)
        {
            ParseLimitItems(data.text);
        }
        else
        {
            Debug.LogError("Failed to load CSV data.");
        }
    }

    public void StartTickTimer()
    {
        CheckLimtEvent();
        GameCoreManager.Instance.StartCoroutine(TickTime());
    }
    
    
    IEnumerator TickTime()
    {

        while (true)
        {
            yield return new WaitForSeconds(1f);
            
            // 假设 logoutTime 是用户的登出时间
            DateTime nDateTime = DateTime.Now;
            DateTime midnight = nDateTime.Date.AddDays(1); // 获取当天的 00:00

            // 计算剩余时间
            TimeSpan timeRemaining = midnight - nDateTime;
        
            // Debug.Log("判断时间："+midnight+" "+timeRemaining);
        
            if (timeRemaining.TotalMinutes >= 0)
            {
                // 如果上次打开时间为空，则初始化为当前时间
                if (string.IsNullOrEmpty(GameDataManager.Instance.UserData.limitOpenTime))
                {
                    GameDataManager.Instance.UserData.limitOpenTime = DateTime.Now.ToString();
                }
                else
                {
                    // 解析上次打开时间
                    DateTime lastTime = DateTime.Parse(GameDataManager.Instance.UserData.limitOpenTime);
    
                    // Debug.Log("判断时间： 上次开启时间"+lastTime+"  今天： "+DateTime.Today);
                    
                    // 判断上次打开日期与今天是否为同一天
                    if (lastTime.Date != DateTime.Today)
                    {
                        // 不是同一天，执行每日重置逻辑
                        GameDataManager.Instance.UserData.CheckResetDailyTime();
                        // 重置后，将上次打开时间更新为当前时间
                        CheckLimtEvent();
                    }
                }
                string time = UIUtilities.FormatTimeRemaining(timeRemaining);
                OnLimitTimeUpdated?.Invoke(time); // 触发事件，通知所有订阅者
                OnDailyTimeUpdated?.Invoke(time); // 触发事件，通知所有订阅者
            }
        }
    }
 
    
    /// <summary>
    /// 领取当前阶段奖励，处理溢出词数结转到下一阶段
    /// </summary>
    public void ClaimCurrentReward()
    {
        if (CurlimitData == null) return;
        var userData = GameDataManager.Instance.UserData;
        int currentStage = userData.timerePuzzleid;
        int totalBefore = 0;
        // 计算之前所有阶段的总需求
        for (int i = 0; i < currentStage; i++)
        {
            var item = GetLimitItem(i);
            if (item != null) totalBefore += item.num;
        }
        // 当前阶段的需求
        int currentNeed = CurlimitData.num;
        // 当前已完成的词数（基于累计值）
        int totalDone = userData.timePuzzlecount;
        int completed = totalDone - totalBefore;
        if (completed >= currentNeed)
        {
            int overflow = completed - currentNeed;
            // 将完成数截断到本阶段满额
            userData.timePuzzlecount = totalBefore + currentNeed;
            // 保存溢出词数，在下一阶段生效
            userData.timePuzzlecount += overflow;
            // 递增阶段
            userData.UpdateLImitid();
            // 更新当前数据引用
            CurlimitData = GetLimitItem(userData.timerePuzzleid);
        }
        // 刷新UI
        UpdateLimitTimeBtnUI();
    }
    
    // <summary>
    /// 根据蝶蛹是否收集满，返回应使用的奖励列表
    /// </summary>
    public List<List<int>> GetEffectiveRewards(LimitDataItem item)
    {
        if (item == null) return null;
        bool pupaFull = ButterfliesManager.Instance.IsPupaSufficientForAllRemaining(); // 蛹足够时就替换
        if (!pupaFull || item.alternativeRewardContent == null)
            return item.rewardContent;

        // 蝶蛹已满且有替代配置：逐项替换，没有替代的保留原奖励
        var result = new List<List<int>>();
        for (int i = 0; i < item.rewardContent.Count; i++)
        {
            result.Add(item.alternativeRewardContent[i] ?? item.rewardContent[i]);
        }
        return result;
    }
    
    private void CheckLimtEvent()
    {
        if (GameDataManager.Instance.UserData.isDayEnterLimint)
        {      
            AnalyticMgr.ActivityBegin("限时活动");
            GameDataManager.Instance.UserData.EveryDayOpenLimit();
        }
    }
    
    void ParseLimitItems(string data)
    {
        // 将 CSV 数据转换为 JSON 格式
        ConvertCSVToJSON(data);

        // 现在limitItems列表中包含所有商品
        // Debug.Log("Limit items loaded: " + limitItems.Count);
    }
    
    /// <summary>
    /// 获取当前连词数量
    /// </summary>
    /// <returns></returns>
    public int GetCurWordCount()
    {
        int needword = 0;
        for (int i = 0; i <= GameDataManager.Instance.UserData.timerePuzzleid; i++)
        {
            LimitDataItem TemplimitData=GetLimitItem(i);
            if (TemplimitData != null)
            {
                if (i == GameDataManager.Instance.UserData.timerePuzzleid)
                {
                    CurlimitData = TemplimitData;
                    if (GameDataManager.Instance.UserData.timePuzzlecount >= needword + CurlimitData.num)
                        return CurlimitData.num;
                    if (GameDataManager.Instance.UserData.timePuzzlecount - needword < 0)
                    {
                        int count=needword - GameDataManager.Instance.UserData.timePuzzlecount;
                        int value = CurlimitData.num - count;
                        
                        GameDataManager.Instance.UserData.timePuzzlecount=needword+value;
                        return value;
                    }
                    else
                    {
                        return GameDataManager.Instance.UserData.timePuzzlecount - needword;
                    }
                }
                needword += TemplimitData.num;
            }         
        }

        return 0;
    }

    private void ConvertCSVToJSON(string data)
    {
        List<LimitDataItem> items = new List<LimitDataItem>();
        string[] lines = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 2; i < lines.Length; i++) // 跳过标题行
        {
            string[] fields = lines[i].Split(',');
            if (fields.Length >= 3)
            {
                int id = int.Parse(fields[0].Trim());

                List<List<int>> normalRewards = new List<List<int>>();
                List<List<int>> alternativeRewards = new List<List<int>>();

                string[] groups = fields[1].Split('#');
                foreach (string group in groups)
                {
                    // 按 '_' 分割原始奖励和替换奖励
                    string[] parts = group.Split('_');
                    string normalPart = parts[0];
                    string alternativePart = parts.Length > 1 ? parts[1] : null;

                    // 解析正常奖励
                    normalRewards.Add(ParseNumbers(normalPart));
                    // 解析替换奖励（如果有）
                    alternativeRewards.Add(alternativePart != null ? ParseNumbers(alternativePart) : null);
                }

                int count = int.Parse(fields[2].Trim());

                LimitDataItem item = new LimitDataItem
                {
                    id = id,
                    rewardContent = normalRewards,
                    alternativeRewardContent = alternativeRewards.Any(r => r != null) ? alternativeRewards : null, // 只有存在替换时才赋值
                    num = count
                };
                items.Add(item);
            }
        }
        limitItems = items;
    }
    
    // 辅助方法：解析 "1;2;3" 为 List<int>
    private List<int> ParseNumbers(string str)
    {
        List<int> numbers = new List<int>();
        string[] parts = str.Split(';');
        foreach (string part in parts)
        {
            if (int.TryParse(part, out int num))
                numbers.Add(num);
        }
        return numbers;
    }


    public List<LimitDataItem> GetLimitItems()
    {
        return limitItems;
    }

    /// <summary>
    /// 限时奖励是否领取完成
    /// </summary>
    /// <returns></returns>
    public bool IsComplete()
    {
        if(GameDataManager.Instance==null) return false;
        
        return GameDataManager.Instance.UserData.timerePuzzleid>limitItems.Count;
    }
    
    /// <summary>
    /// 限时奖励是否可以领取
    /// </summary>
    /// <returns></returns>
    public bool IsClaim()
    {
        int wordCount = GetCurWordCount();
        if (CurlimitData == null) return false;
        return wordCount>=CurlimitData.num;
    }

    /// <summary>
    /// 限时活动翻倍时间是否可以显示
    /// </summary>
    /// <param name="num"></param>
    /// <returns></returns>
    public bool LimitTimeCanShow()
    {
        if (!string.IsNullOrEmpty(GameDataManager.Instance.UserData.limitEndTime))
        {
            DateTime endTime = DateTime.Parse(GameDataManager.Instance.UserData.limitEndTime);
            if (endTime > DateTime.Now)
            {
                return true;
            }
        }
        return false;
    }

    public void UpdateLimitTimeBtnUI()
    {
        OnLimitTimeBtnUI?.Invoke();
    }

    public void UpdateLimitProgress(int value)
    {
        GameDataManager.Instance.UserData.timePuzzlecount += value;
    }
    
    /// <summary>
    /// 获取限时活动翻倍时间剩余分钟
    /// </summary>
    /// <param name="num"></param>
    /// <returns></returns>
    public int GetLimitWordMinTime()
    {
        DateTime endtime = DateTime.Parse(GameDataManager.Instance.UserData.limitEndTime);
        if (endtime > DateTime.Now)
        {
            TimeSpan timeSpan = endtime - DateTime.Now;
            return (int)Math.Ceiling(timeSpan.TotalMinutes);
        }
        
        return 0;
    }

    public LimitDataItem GetLimitItem(int limitItemID)
    {
        foreach (var limitItem in limitItems)
        {
            if (limitItem.id == limitItemID)
            {
                return limitItem;
            }
        }
        return null;
    }
}