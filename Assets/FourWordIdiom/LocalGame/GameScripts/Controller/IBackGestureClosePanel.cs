/// <summary>
/// 侧滑（安卓返回键 / 鸿蒙返回手势）命中的界面，若实现了本接口，
/// 就由界面自己执行「关闭按钮」的逻辑，而不是被路由直接 HidePanel 掉。
///
/// 为什么要这样：活动页 / 商店页这类界面的关闭按钮通常不只是隐藏界面，
/// 还会做「回主页、恢复 HeaderSection、恢复入口层」等副作用；
/// 侧滑如果绕过它直接隐藏，界面状态会和玩家点关闭按钮不一致。
/// </summary>
public interface IBackGestureClosePanel
{
    /// <summary>执行本界面关闭按钮的逻辑（必须与玩家点按钮完全等价）。</summary>
    void CloseFromBackGesture();
}
