/// <summary>
/// 结算相关的动效衔接节奏（单位：秒）。
///
/// 需求来源：《成语消2609改进（一）》A 部分
///   「结算页，弹窗后，所有活动的交互优化，加快动效间的衔接」
///   「拼字关卡完成时，退出关卡界面到显示结算界面这个流程快一倍」
///
/// 设计原则：**只压缩活动与活动之间的空等，不改动动效本身的时长**。
/// 之前散落在 ChessFinishView / StageFinishView / TaskTable / ChessStageController 里的魔法数字统一收敛到这里，
/// 后续调整节奏只改这一处，不用在多个协程里翻找。
/// </summary>
public static class FinishFlowTiming
{
    // ======================= 关卡完成 → 结算转场 =======================

    /// <summary>通关音效后，到开始收关卡界面的停顿（原 0.2）</summary>
    public const float LevelOutLead = 0.1f;

    /// <summary>埋点上报后，到收起关卡界面的停顿（原 0.1）</summary>
    public const float LevelOutBeforeHide = 0.05f;

    /// <summary>关卡界面收起后，到结算界面出现的等待（原 0.8，已按隐藏动画 0.42s 对齐）</summary>
    public const float LevelOutToFinish = 0.4f;

    // ======================= 通用衔接 =======================

    /// <summary>活动段落之间的通用衔接间隔（原 0.3~0.5）</summary>
    public const float StepGap = 0.2f;

    /// <summary>限时活动按钮动效播完后，等面板弹出的间隔（原 0.5）</summary>
    public const float LimitPanelGap = 0.2f;

    /// <summary>禅意总分滚动结束后，进入下一段的缓冲（原 0.3）</summary>
    public const float AfterZenRoll = 0.12f;

    /// <summary>金币起飞后进入下一段的缓冲（原 0.5）</summary>
    public const float CoinFlyGap = 0.2f;

    // ======================= 莲花飞榜 =======================

    /// <summary>中心荷花点亮（淡入）时长（原 1.5）</summary>
    public const float LotusLightFade = 0.6f;

    /// <summary>荷花点亮后到起飞之间的停顿（原 1.75 的盲等）</summary>
    public const float LotusLightHold = 0.2f;

    /// <summary>莲花落位后的缓冲（原 0.3）</summary>
    public const float AfterLotusLand = 0.12f;

    // ======================= 任务 / 蝶蛹 =======================

    /// <summary>任务条目飞出动画启动前的等待（原 1.0）</summary>
    public const float TaskAnimLead = 0.2f;

    /// <summary>任务动效启动后，进入下一段的衔接（原 1.5）</summary>
    public const float TaskPanelGap = 0.45f;

    /// <summary>多枚蝶蛹依次起飞的间隔（原 0.2）</summary>
    public const float PupaStagger = 0.12f;
}
