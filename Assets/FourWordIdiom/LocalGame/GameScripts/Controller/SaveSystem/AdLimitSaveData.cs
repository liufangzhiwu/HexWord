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
///   - 提示道具 / 复活道具「视频点」已用次数（PropVideoTimes 的 A / B 位）：
///     按"关卡"统计，换关自动刷新（每关 X=a，看完一次激励视频 X-1）；
///   - T 秒冷却：跨所有视频入口共享，用 Ticks 存绝对值。
///
/// ★ 两套计数各管一件事：
///   - *Used（已看完次数）：与分层表 AdFailed 的 A/B/C 比较，决定"从第几次之后开始弹假失败"；
///   - *ClickUsed（视频点已用次数）：与 cypz_Mechanics 的 PropVideoTimes 比较，决定"本关还能点几次"。
///   两者不等价：假失败那一次点击不占用视频点次数，只有真实广告播完才 ClickUsed+1。
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

    // ======================= 视频点次数（PropVideoTimes 上限）：按关卡（商店金币按自然日） =======================

    /// <summary>本关灯泡（提示道具）视频点已用次数（= 本关已看完的激励视频数）</summary>
    public int bulbClickUsed;

    /// <summary>本关复活道具视频点已用次数（= 本关已看完的激励视频数）</summary>
    public int reviveClickUsed;

    /// <summary>当日商店金币视频点已用次数（含当日首次免费领取那一次）</summary>
    public int shopGoldClickUsed;

    // ======================= 假广告失败弹窗标记（每个入口每次只弹 1 次） =======================
    //
    // 口径：
    //   达到分层表 A/B/C 阈值后，该入口只弹一次「假的广告获取失败弹窗」，同时开启 T 秒冷却；
    //   冷却结束后恢复正常请求广告，不再假失败；假失败那次点击不占用视频点次数。
    //   - 灯泡 / 复活：随"关卡"清零（与 A/B 阈值同口径）；
    //   - 商店金币：随"自然日"清零（与 C 阈值同口径）。

    /// <summary>本关灯泡（提示道具）是否已弹过假失败弹窗</summary>
    public bool bulbFakeFailDone;

    /// <summary>本关复活道具是否已弹过假失败弹窗</summary>
    public bool reviveFakeFailDone;

    /// <summary>当日商店金币视频是否已弹过假失败弹窗</summary>
    public bool shopGoldFakeFailDone;

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
            bulbClickUsed = bulbClickUsed,
            reviveClickUsed = reviveClickUsed,
            shopGoldClickUsed = shopGoldClickUsed,
            bulbFakeFailDone = bulbFakeFailDone,
            reviveFakeFailDone = reviveFakeFailDone,
            shopGoldFakeFailDone = shopGoldFakeFailDone,
            adFailBlockedUntilTicks = adFailBlockedUntilTicks,
        };
    }
}
