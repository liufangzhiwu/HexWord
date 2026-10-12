using System;
using UnityEngine;

/// <summary>
/// 商店限购 / 购买跟踪数据（持久化在 GameDataManager.UserData.limitShopItems）。
///
/// 包含两类信息：
///  1) 限时礼包 / 去广告 / 限时权益的生效状态（endtime / isget / isoverdate / adstype）；
///  2) 推荐位决策所需的礼包购买跟踪（buyCount / payTendencyScore / exploreScore /
///     conversionFailScore / lastFailScoreTime / lastBuyTime / limitEndTime / ...）。
///
/// 时间字段统一使用 "yyyy-MM-dd HH:mm:ss" 字符串存储，便于 JsonUtility 序列化；
/// 通过 GetXxxTime / SetXxxTime 辅助方法做 DateTime 转换，避免各处手写 DateTime.TryParse。
/// </summary>
[Serializable]
public class ShopLimitData
{
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    // ========== 基础字段 ==========

    /// <summary>商品 ID（对应 ShopDataItem.id）</summary>
    public int id;

    /// <summary>商品名称 ID（对应 ShopDataItem.produceNameId）</summary>
    public string nameid;

    /// <summary>活动 / 权益结束时间（永久为空字符串）</summary>
    public string endtime;

    /// <summary>是否开启</summary>
    public bool isopen;

    /// <summary>奖励获取时间</summary>
    public string gettime;

    /// <summary>去广告类型（对应 LimitRewordType）</summary>
    public int adstype;

    /// <summary>是否已获得</summary>
    public bool isget;

    /// <summary>是否已失效</summary>
    public bool isoverdate;

    // ========== 推荐位决策 / 限购跟踪 ==========

    /// <summary>已购买次数（限购判断）</summary>
    public int buyCount;

    /// <summary>付费倾向积分</summary>
    public float payTendencyScore;

    /// <summary>探价加分</summary>
    public float exploreScore;

    /// <summary>转化失败分 F</summary>
    public float conversionFailScore;

    /// <summary>F 值最近变化时间（用于 30 秒间隔判断）</summary>
    public string lastFailScoreTime;

    /// <summary>最近购买时间（用于冷却系数）</summary>
    public string lastBuyTime;

    /// <summary>限时活动礼包截至时间（用于冷却系数 48h / 120h 判断）</summary>
    public string limitEndTime;

    /// <summary>当前是否为活动礼包（节日 X 折 / 周末 X 折）</summary>
    public bool isActivityPack;

    /// <summary>推送状态指数（优惠推送 = 0，默认 1）</summary>
    public float pushStateIndex = 1f;

    // ========== 时间字段转换辅助 ==========

    public DateTime GetEndTime() => ParseTime(endtime);
    public void SetEndTime(DateTime value) => endtime = FormatTime(value);

    public DateTime GetGetTime() => ParseTime(gettime);
    public void SetGetTime(DateTime value) => gettime = FormatTime(value);

    public DateTime GetLastBuyTime() => ParseTime(lastBuyTime);
    public void SetLastBuyTime(DateTime value) => lastBuyTime = FormatTime(value);

    public DateTime GetLastFailScoreTime() => ParseTime(lastFailScoreTime);
    public void SetLastFailScoreTime(DateTime value) => lastFailScoreTime = FormatTime(value);

    public DateTime GetLimitEndTime() => ParseTime(limitEndTime);
    public void SetLimitEndTime(DateTime value) => limitEndTime = FormatTime(value);

    private static DateTime ParseTime(string s)
    {
        DateTime t;
        return DateTime.TryParse(s, out t) ? t : DateTime.MinValue;
    }

    private static string FormatTime(DateTime value)
    {
        return value == DateTime.MinValue ? "" : value.ToString(TimeFormat);
    }

    // ========== 便捷判断 ==========

    /// <summary>是否处于有效期内（endtime 为空视为永久有效）</summary>
    public bool IsActive()
    {
        if (!isget || isoverdate) return false;
        if (string.IsNullOrEmpty(endtime)) return true;
        return GetEndTime() > DateTime.Now;
    }

    /// <summary>剩余有效期（秒），永久或无效返回 0</summary>
    public double GetRemainSeconds()
    {
        if (string.IsNullOrEmpty(endtime)) return 0;
        DateTime end = GetEndTime();
        if (end == DateTime.MinValue) return 0;
        return Math.Max(0, (end - DateTime.Now).TotalSeconds);
    }

    /// <summary>深拷贝（避免修改共享引用）</summary>
    public ShopLimitData DeepCopy()
    {
        return new ShopLimitData
        {
            id = id,
            nameid = nameid,
            endtime = endtime,
            isopen = isopen,
            gettime = gettime,
            adstype = adstype,
            isget = isget,
            isoverdate = isoverdate,
            buyCount = buyCount,
            payTendencyScore = payTendencyScore,
            exploreScore = exploreScore,
            conversionFailScore = conversionFailScore,
            lastFailScoreTime = lastFailScoreTime,
            lastBuyTime = lastBuyTime,
            limitEndTime = limitEndTime,
            isActivityPack = isActivityPack,
            pushStateIndex = pushStateIndex,
        };
    }
}