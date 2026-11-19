using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 侧滑返回（Android 系统返回键 / 全面屏侧滑手势）统一路由。
///
/// 规则（按优先级从上到下，命中即返回）：
///   0. 全屏广告播放中            → 不处理（广告窗口自己负责返回）
///   0.1 输入框编辑中（软键盘开着）→ 只收起键盘，不关界面
///   1. 退出确认面板开着          → 不处理（面板只认按钮，侧滑直接吞掉）
///   2. 暂停界面开着              → 关掉并继续游戏（走 PauseGameScreen 自己的恢复逻辑）
///   3. 复活弹窗 / 断点续玩窗      → 各自界面内的按钮逻辑
///                                  （复活弹窗=CloseBtn；续玩窗=取消 cancelQuitBtn）
///   3.1 排行榜结算（月榜/段位）    → 等价于点「继续 / 领取」，不是关面板
///                                  （领奖、上报服务器、清缓存都挂在按钮流程里）
///   4. 任意弹窗/活动页/商店页/
///      道具弹窗/礼包弹窗/模式选择    → 关掉最上层那个；若它实现了 IBackGestureClosePanel，
///                                  则执行它自己的按钮逻辑（SelectMode = 点当前模式的按钮；
///                                  活动页/商店页等有回主页、恢复顶栏等收尾副作用）
///   5. 拼字关卡（ChessPlayArea） → 弹出 PauseGameScreen
///   6. 六边形 / 消消关卡         → 返回主页
///   7. Start 场景               → QuitPanel（隐藏关闭按钮，主按钮「取消」）
///   8. GameLobby 场景主页/结算   → QuitPanel（显示关闭按钮，主按钮「去闯关」）
/// </summary>
public static class BackGestureRouter
{
    public const string SceneStart = "Start";
    public const string SceneGameLobby = "GameLobby";

    /// <summary>
    /// 这些界面即使在弹窗层，也不参与「侧滑关弹窗」——它们由关卡/场景规则接管，
    /// 或者有自己的关闭流程（复活弹窗 / 断点续玩窗 / 暂停窗）。
    /// 其中 ZenSettlementScreen / OverallSettlementScreen 不关窗，侧滑改为
    /// 「继续 / 领取」，见 TryAdvanceRankSettlement。
    /// </summary>
    private static readonly HashSet<string> PopupCloseBlacklist = new HashSet<string>
    {
        PanelType.PrimaryInterface,
        PanelType.HeaderSection,
        PanelType.ChessPlayArea,
        PanelType.HexGamePlayArea,
        PanelType.GamePlayArea,
        PanelType.ChessFinishView,
        PanelType.StageFinishView,
        PanelType.ZenSettlementScreen,
        PanelType.OverallSettlementScreen,
        PanelType.FailGameScreen,
        PanelType.PauseGameScreen,
        PanelType.ContinueGameWindow,
        PanelType.LearningGuide,
        PanelType.ChessLearningGuide,
        PanelType.DebugMenu,
    };

    /// <summary>
    /// 这些界面侧滑时应当「关闭它」，即使层级不是常规弹窗层也要纳入。
    /// SelectMode 打开时 HeaderSection 被隐藏、PrimaryInterface 折叠，
    /// 必须走它自己的「选模式 → 收弹窗回主页」流程，不能直接 HidePanel。
    /// </summary>
    private static readonly HashSet<string> PopupCloseExtraPages = new HashSet<string>
    {
        PanelType.ZenRankScreen,
        PanelType.ButterflyHome,
        PanelType.ButterflyManual,
        PanelType.ButterflyGardenHelp,
        PanelType.SelectMode,
    };

    /// <summary>弹窗层：这些层级的可见界面都算「可以侧滑关掉」。</summary>
    private static bool IsPopupCategory(string category)
    {
        return category == UIPanelLayer.PopPanel
            || category == UIPanelLayer.UpPopPanel
            || category == UIPanelLayer.UpPopTwoPanel
            || category == UIPanelLayer.RewardPanel;
    }

    /// <summary>
    /// 去抖窗口（秒）。鸿蒙上系统返回手势可能同时经「ArkTS onBackPress → TuanjieSendMessage」
    /// 和「引擎合成的 KeyCode.Escape」两条通道到达，同一手势只能处理一次，否则会连着执行两步
    /// （例如先关闭弹窗、又接着触发关卡的规则）。
    /// </summary>
    private const float HandleInterval = 0.25f;
    private static float _lastHandleTime = -10f;

    /// <summary>侧滑入口。由 Middleware.Game.Update 在收到返回键/侧滑手势时调用。</summary>
    public static void HandleBackGesture()
    {
        if (SystemManager.Instance == null) return;

        float now = Time.unscaledTime;
        if (now - _lastHandleTime < HandleInterval)
        {
            Debug.Log("[BackGesture] 同一手势的重复信号，忽略");
            return;
        }
        _lastHandleTime = now;

        // 0. 全屏广告播放中：侧滑不参与游戏内路由。
        //    鸿蒙的激励/插屏是原生 UIExtension 全屏窗口，返回手势通常被广告窗口自己吃掉；
        //    但引擎侧若也合成了一次返回，就会在广告还开着的时候把底下的界面关掉/暂停，
        //    等广告回调回来时状态已经错位。这里兜一层。
        if (IsFullScreenAdPlaying())
        {
            Debug.Log("[BackGesture] 全屏广告播放中，侧滑忽略");
            return;
        }

        // 0.1 输入框正在编辑：侧滑只收键盘，不关界面。
        //     改名面板（RewardNamePanel）这类界面，鸿蒙上软键盘是系统 UI，
        //     返回手势可能被系统先消费掉、也可能直接传到引擎，两条路都可能出现
        //     「键盘还在、面板被关掉」的半状态，统一在这里兜住。
        if (TryExitInputEditing()) return;

        // 1. 退出确认面板开着时，侧滑不处理（既不叠加新面板，也不代用户取消）
        if (QuitPanel.IsShowing)
        {
            Debug.Log("[BackGesture] 退出确认面板开着，侧滑忽略");
            return;
        }

        // 2. 暂停界面：侧滑 = 继续游戏
        if (TryResumeFromPause()) return;

        // 3. 复活弹窗 / 断点续玩窗：侧滑 = 点击它们自己的关闭按钮
        if (TryCloseReviveOrContinue()) return;

        // 3.1 排行榜结算界面：侧滑 = 「继续 / 领取」，不是关面板
        if (TryAdvanceRankSettlement()) return;

        // 4. 各类弹窗 / 活动页 / 商店页 / 道具 / 礼包弹窗
        if (TryCloseTopPopup()) return;

        // 5. 拼字关卡 → 暂停界面
        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
        {
            if (ChessPlayArea.Instance != null)
                ChessPlayArea.Instance.RequestPauseFromBackGesture();
            else
                SystemManager.Instance.ShowPanel(PanelType.PauseGameScreen);
            return;
        }

        // 6. 六边形 / 消消关卡 → 返回主页
        if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea)
            || SystemManager.Instance.PanelIsShowing(PanelType.GamePlayArea))
        {
            BackToHome();
            return;
        }

        // 7/8. 场景级兜底
        if (IsStartScene())
            QuitPanel.Show(QuitPanel.Mode.ExitApp);
        else
            QuitPanel.Show(QuitPanel.Mode.GoLevel);
    }

    #region 内部实现

    /// <summary>全屏广告（激励/插屏）播放中返回 true。</summary>
    private static bool IsFullScreenAdPlaying()
    {
        try
        {
            Middleware.Game game = Middleware.Game.self;
            return game != null && game.Ads != null && game.Ads.IsPlaying;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 软键盘开着（正在输入）时，侧滑只做「收起键盘」并返回 true。
    /// 鸿蒙/安卓的软键盘都是独立窗口，返回手势第一下通常被系统用来收键盘；
    /// 但如果这一下也传到了引擎（鸿蒙走 ArkTS 通道时就会），就会把整个面板关掉，
    /// 玩家刚输的名字直接丢。这里统一兜住：先收键盘，再滑一次才关界面。
    /// </summary>
    private static bool TryExitInputEditing()
    {
        if (!TouchScreenKeyboard.visible) return false;

        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem != null)
        {
            GameObject selected = eventSystem.currentSelectedGameObject;
            UnityEngine.UI.InputField field = selected != null
                ? selected.GetComponent<UnityEngine.UI.InputField>()
                : null;

            if (field != null && field.isFocused)
            {
                Debug.Log("[BackGesture] 输入框编辑中，侧滑先收起键盘");
                field.DeactivateInputField();
                eventSystem.SetSelectedGameObject(null);
                return true;
            }
        }

        // 兜底：键盘开着但没定位到具体 InputField（例如刚失焦），这一下滑掉不做事
        Debug.Log("[BackGesture] 软键盘开着，侧滑先收起键盘");
        return true;
    }

    private static bool IsStartScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return false;
        if (scene.name == SceneStart) return true;
        // 兜底：Start 是 0 号场景
        return scene.buildIndex == 0 && string.IsNullOrEmpty(scene.name);
    }

    /// <summary>暂停界面开着时，侧滑等价于点「继续游戏」。</summary>
    private static bool TryResumeFromPause()
    {
        if (!SystemManager.Instance.PanelIsShowing(PanelType.PauseGameScreen)) return false;

        PauseGameScreen pause = SystemManager.Instance.GetPanel(PanelType.PauseGameScreen) as PauseGameScreen;
        if (pause != null)
            pause.ResumeFromBackGesture();
        else
            SystemManager.Instance.HidePanel(PanelType.PauseGameScreen);

        return true;
    }

    /// <summary>
    /// 复活弹窗 → 等价于它的 CloseBtn；断点续玩窗 → 等价于它的「取消」按钮（cancelQuitBtn）。
    /// 两者都走界面自己的既有流程，避免侧滑产生与按钮不一致的状态。
    /// </summary>
    private static bool TryCloseReviveOrContinue()
    {
        if (SystemManager.Instance.PanelIsShowing(PanelType.FailGameScreen))
        {
            FailGameScreen fail = SystemManager.Instance.GetPanel(PanelType.FailGameScreen) as FailGameScreen;
            if (fail != null)
            {
                Debug.Log("[BackGesture] 侧滑 = 复活弹窗的关闭按钮");
                fail.CloseFromBackGesture();
                return true;
            }
        }

        if (SystemManager.Instance.PanelIsShowing(PanelType.ContinueGameWindow))
        {
            ContinueGameWindow cont = SystemManager.Instance.GetPanel(PanelType.ContinueGameWindow) as ContinueGameWindow;
            if (cont != null)
            {
                Debug.Log("[BackGesture] 侧滑 = 断点续玩窗的「取消」按钮");
                cont.CloseFromBackGesture();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 排行榜结算（月榜 OverallSettlementScreen / 段位 ZenSettlementScreen）显示时，
    /// 侧滑等价于点它自己的「继续 / 领取」主按钮，而不是关面板。
    ///
    /// 这两个界面在 PopupCloseBlacklist 里（不能被 TryCloseTopPopup 直接 HidePanel），
    /// 领取奖励、上报服务器、清排行榜缓存、存档都挂在按钮流程上；
    /// 直接关面板 = 玩家看到奖励却一分钱没拿到，月榜还会让
    /// CheckMonthlySettlementRoutine 误判「已结算」而不再补发。
    /// </summary>
    private static bool TryAdvanceRankSettlement()
    {
        // 月榜优先：它的关闭会让下层段位结算继续走，两者同时可见时先推进上面那个。
        if (SystemManager.Instance.PanelIsShowing(PanelType.OverallSettlementScreen))
        {
            OverallSettlementScreen monthly =
                SystemManager.Instance.GetPanel(PanelType.OverallSettlementScreen) as OverallSettlementScreen;
            if (monthly != null)
            {
                Debug.Log("[BackGesture] 侧滑 = 月榜结算的「继续 / 领取」");
                monthly.AdvanceFromBackGesture();
                return true;
            }
        }

        if (SystemManager.Instance.PanelIsShowing(PanelType.ZenSettlementScreen))
        {
            ZenSettlementScreen zen =
                SystemManager.Instance.GetPanel(PanelType.ZenSettlementScreen) as ZenSettlementScreen;
            if (zen != null)
            {
                Debug.Log("[BackGesture] 侧滑 = 段位结算的「继续 / 领取」");
                zen.AdvanceFromBackGesture();
                return true;
            }
        }

        return false;
    }

    /// <summary>关掉最上层的那个弹窗；没有任何可关的弹窗时返回 false。</summary>
    private static bool TryCloseTopPopup()
    {
        UIWindow top = null;
        int topSibling = int.MinValue;

        List<UIWindow> visible = SystemManager.Instance.GetVisiblePanels();
        for (int i = 0; i < visible.Count; i++)
        {
            UIWindow panel = visible[i];
            if (panel == null || string.IsNullOrEmpty(panel.WindowName)) continue;
            if (!IsClosablePopup(panel)) continue;

            int sibling = panel.transform.GetSiblingIndex();
            if (sibling >= topSibling)
            {
                topSibling = sibling;
                top = panel;
            }
        }

        if (top == null) return false;

        // 界面自己实现了「关闭按钮」语义（活动页 / 商店页等）→ 走它自己的关闭逻辑，
        // 这样侧滑和玩家点关闭按钮的副作用（回主页、恢复 HeaderSection 等）完全一致。
        IBackGestureClosePanel closable = top as IBackGestureClosePanel;
        if (closable != null)
        {
            Debug.Log($"[BackGesture] 侧滑触发界面关闭按钮：{top.WindowName}");
            closable.CloseFromBackGesture();
            return true;
        }

        Debug.Log($"[BackGesture] 侧滑关闭弹窗：{top.WindowName}");
        SystemManager.Instance.HidePanel(top.WindowName);
        return true;
    }

    private static bool IsClosablePopup(UIWindow panel)
    {
        string name = panel.WindowName;
        if (PopupCloseBlacklist.Contains(name)) return false;
        if (PopupCloseExtraPages.Contains(name)) return true;
        return IsPopupCategory(panel.WindowCategory);
    }

    /// <summary>六边形 / 消消关卡侧滑 = 返回主页（对齐 HeaderSection 返回按钮的行为）。</summary>
    private static void BackToHome()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.TriggerVibration(40, 50);

        if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
            SystemManager.Instance.HidePanel(PanelType.HexGamePlayArea);

        if (SystemManager.Instance.PanelIsShowing(PanelType.GamePlayArea))
            SystemManager.Instance.HidePanel(PanelType.GamePlayArea);

        GameDataManager.Instance.UserData.EndStageTime();
        // 主页出现时会自己把 HeaderSection 拉起来（PrimaryInterface.ShowTopPanel）
        SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
    }

    #endregion
}
