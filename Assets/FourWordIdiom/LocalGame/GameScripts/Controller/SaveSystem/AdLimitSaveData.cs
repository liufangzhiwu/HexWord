using System;

/// <summary>
/// 广告获取失败策略（S_F_T_A_B_C）的限次持久化数据。
///
/// 背景：原实现把"已看次数"放在 ShopManager 的内存私有字段里，导致
///   1) 游戏进程关闭后计数丢失（重新打开又恢复满次数）；
///   2) 商店金币视频累计时没有归一化日期，剩余次数永远不减。
///
/// 该结构体随 <see cref="UserData"/> 一起序列化落盘，保证"关掉游戏再进"数据可继承。
///
/// 计数口径：
///   - 商店金币视频（C）：按自然日统计，跨天自动刷新；
///   - 分享奖励（F）：按自然日统计，跨天自动刷新；
///   - 提示道具 / 复活道具视频（A / B）：按"关卡"统计，换关自动刷新；
///   - T 秒冷却：跨所有视频入口共享，用 Ticks 存绝对值。
/// </summary>
[Serializable]
public class AdLimitSaveData
{
    // ======================= 商店金币视频（C）：按自然日 =======================

    /// <summary>商店金币视频计数所属日期（"yyyy-MM-dd"）</summary>
    public string shopGoldDate = "";

    /// <summary>当日已正常观看的商店金币视频次数</summary>
    public int shopGoldUsed;

    // ======================= 分享奖励（F）：按自然日 =======================

    /// <summary>分享计数所属日期（"yyyy-MM-dd"）</summary>
    public string shareDate = "";

    /// <summary>当日已成功分享次数</summary>
    public int shareUsed;

    // ======================= 提示 / 复活视频（A / B）：按关卡 =======================

    /// <summary>
    /// 当前计数所属关卡标识（形如 "H:36" 六边形/方块、"C:12" 拼字）。
    /// 关卡标识不同即视为换关，计数清零。
    /// </summary>
    public string toolLevelKey = "";

    /// <summary>当前关卡已正常观看的灯泡（提示道具）视频次数</summary>
    public int bulbUsed;

    /// <summary>当前关卡已正常观看的复活道具视频次数</summary>
    public int reviveUsed;

    // ======================= 广告失败弹窗冷却（T） =======================

    /// <summary>广告失败弹窗冷却截止时间（DateTime.Ticks；0 表示无冷却）</summary>
    public long adFailBlockedUntilTicks;

    /// <summary>深拷贝，避免 InitData 时多个存档共享同一引用。</summary>
    public AdLimitSaveData Clone()
    {
        return new AdLimitSaveData
        {
            shopGoldDate = shopGoldDate,
            shopGoldUsed = shopGoldUsed,
            shareDate = shareDate,
            shareUsed = shareUsed,
            toolLevelKey = toolLevelKey,
            bulbUsed = bulbUsed,
            reviveUsed = reviveUsed,
            adFailBlockedUntilTicks = adFailBlockedUntilTicks,
        };
    }
}
