using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using Newtonsoft.Json;
using UnityEngine;

public class LimitDataItem
{
    public int id;
    //奖励内容
    public List<List<int>> rewardContent;
    //需要的成语数
    public int num;
}

//对应限时奖励配置表中奖励配置批准中的奖励索引表示的类型
public enum LimitRewordType
{
    Coins,Butterfly,Tipstool,Resettool,Min5Double,Min15Double,RemoveAds,Remove7DayAds,AutoComplete
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


        UnityTimer.Loop(1f, TickTime);
    }
    
    
    private void TickTime()
    {
        // 假设 logoutTime 是用户的登出时间
        DateTime logoutTime = DateTime.Now; // 将字符串转换为 DateTime
        DateTime midnight = logoutTime.Date.AddDays(1); // 获取当天的 00:00

        // 计算剩余时间
        TimeSpan timeRemaining = midnight - logoutTime;
        if (timeRemaining.TotalMinutes > 0)
        {
            if (timeRemaining.Hours == 24)
            {
                GameDataManager.Instance.UserData.CheckResetLimitTime();
            }
            string time = UIUtilities.FormatTimeRemaining(timeRemaining);
            OnLimitTimeUpdated?.Invoke(time); // 触发事件，通知所有订阅者
            OnDailyTimeUpdated?.Invoke(time); // 触发事件，通知所有订阅者
        }
    }
 
    void ParseLimitItems(string data)
    {
        // 将 CSV 数据转换为 JSON 格式
        ConvertCSVToJSON(data);

        // 现在limitItems列表中包含所有商品
        Debug.Log("Limit items loaded: " + limitItems.Count);
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
                    return GameDataManager.Instance.UserData.timePuzzlecount - needword;
                }
                needword += TemplimitData.num;
            }         
        }

        return 0;
    }

    void ConvertCSVToJSON(string data)
    {
        // 用于构建 JSON 字符串
        List<LimitDataItem> items = new List<LimitDataItem>();
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
              
                int count = int.Parse(fields[2].Trim());

                LimitDataItem item = new LimitDataItem
                {
                    id = id,
                    rewardContent = productContent,
                    num = count
                };
                items.Add(item);
            }
            else
            {
                Debug.LogWarning($"Skipping line {i + 1}: Not enough fields.");
            }
        }
      
        limitItems = items;
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
        
        return GameDataManager.Instance.UserData.timerePuzzleid>=limitItems.Count;
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