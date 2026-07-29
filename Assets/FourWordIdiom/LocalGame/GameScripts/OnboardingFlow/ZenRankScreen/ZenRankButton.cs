using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class ZenRankButton : MonoBehaviour
{
    public Button rankBtn;
    public Image rankImage;
    public Text zenLevelText;
    public GameObject wifi;
    public GameObject rank;
    public Text rankText;
    
    // public GameObject effect;
    
    // Start is called before the first frame update
    private bool isUnranked = true; // 记录是否“未上榜”
    private bool isHandlingClick = false;
    
    private void Awake()
    {
        if (rankBtn == null)
            rankBtn = GetComponent<Button>();
        
        if(rankImage == null)
            rankImage = GetComponentInChildren<Image>();
        
        if(zenLevelText == null)
            zenLevelText = GetComponentInChildren<Text>();
        
    }

    private void OnEnable()
    {
        isHandlingClick = false;
        // if (rankText != null) rankText.text = "...";
        SyncTextFromCache();
        CheckRankProgress();
        // FetchMyCurrentRank();
        // 🌟 修复：注册倒计时监听
        if (ZenRankManager.Instance != null)
        {
            ZenRankManager.Instance.OnRankTimerTick += OnTimerTick;
        }
    }

    void Start()
    {
        rankBtn.AddClickAction(OnRankButtonClick);
    }
    
    // 👇=== 监听倒计时状态 ===👇
    private void OnTimerTick(int seconds, string timeStr)
    {
        if (!GameDataManager.Instance.UserData.isJoinedZenRank) return; // 没加入不关心结算

        if (seconds <= 0 && rankText != null)
        {
            rank.SetActive(true);
            rankText.text = MultilingualManager.Instance.GetString("LotusRankingEnd") ?? "结算中";
        }
    }

    private void OnRankButtonClick()
    {
        if (!Game.IsNetworkActive)
        {
            MessageSystem.Instance.ShowTip(MultilingualManager.Instance.GetString("RestorePurchasesTips01"), false);
            return;
        }

        // 未起名时，先检查弹窗条件
        if (string.IsNullOrEmpty(GameDataManager.Instance.UserData.UserName))
        {
            var userData = GameDataManager.Instance.UserData;
            if (userData.CanShowCharInfoPopup())
            {
                // 标记已弹出（立即记录，避免多次触发）
                userData.MarkCharInfoPopupShown();
                // 弹出起名/奖励面板
                SystemManager.Instance.ShowPanel(PanelType.RewardNamePanel);
            }
            else
            {
                // 弹窗次数已用完，提示用户手动前往设置
                MessageSystem.Instance.ShowTip("请前往设置昵称后再查看排行榜");
            }
            return; // 未起名时不再继续后面的排行榜跳转
        }
        if (isHandlingClick) return;
        
        // 1. 记录来源页面 (捕获你要返回的 PanelType)
        string sourcePanel = PanelType.PrimaryInterface; // 默认大厅
        if (GameCoreManager.Instance.PanelState == PanelState.MainMenuPanel)
            sourcePanel = PanelType.PrimaryInterface;
        else if (GameCoreManager.Instance.PanelState == PanelState.FinishXiaoPanel)
            sourcePanel = PanelType.StageFinishView;
        else if (GameCoreManager.Instance.PanelState == PanelState.FinishPingPanel)
            sourcePanel = PanelType.ChessFinishView;
        else if (GameCoreManager.Instance.PanelState == PanelState.GameXiaoPanel)
            sourcePanel = PanelType.GamePlayArea;
        else if (GameCoreManager.Instance.PanelState == PanelState.GamePingPanel)
            sourcePanel = PanelType.ChessPlayArea;
        
        StartCoroutine(HandleRankButtonClick(sourcePanel));
    }
    private IEnumerator HandleRankButtonClick(string sourcePanel)
    {
        isHandlingClick = true;
        
        bool hasTriggeredSettlement = false;
        
        // 🌟 核心修复1：无条件先去服务器查是否有结算！不要依赖本地的剩余时间！
        yield return ZenRankManager.Instance.CheckAndShowSettlementRoutine(sourcePanel,(res) =>
        {
            hasTriggeredSettlement = res;
        });
        if (hasTriggeredSettlement)
        {
            SystemManager.Instance.HidePanel(sourcePanel);
            SystemManager.Instance.HidePanel(PanelType.HeaderSection);

            // 如果触发了结算，Manager内部已自动打开了雷达页，只需把来源页传给它
            var radarWindow = SystemManager.Instance.GetPanel(PanelType.ZenRankStartScreen);
            if (radarWindow != null)
            {
                radarWindow.GetComponent<ZenRankStartScreen>().SetSourcePanel(sourcePanel);
            }
        }
        else
        {
            // 如果确实没结算，再拉取当前榜单数据并打开榜单
            if (GameDataManager.Instance.UserData.isJoinedZenRank)
            {
                yield return ZenRankManager.Instance.FetchLeaderboardDataRoutine(GameDataManager.Instance.UserData.Zenlevel);
            }
            
            SystemManager.Instance.HidePanel(sourcePanel);
            SystemManager.Instance.HidePanel(PanelType.HeaderSection);
            
            OpenRankUI(sourcePanel);
        }
        
        isHandlingClick = false;
    }
    // 提炼出打开界面的逻辑
    private void OpenRankUI(string sourcePanel)
    {
        UIWindow window = null;
        if (!GameDataManager.Instance.UserData.isJoinedZenRank)
        {
            window = SystemManager.Instance.ShowPanel(PanelType.ZenRankStartScreen);
            var startScreen = window.GetComponent<ZenRankStartScreen>();
            if (startScreen != null) startScreen.SetSourcePanel(sourcePanel);
        }
        else
        {
            window = SystemManager.Instance.ShowPanel(PanelType.ZenRankScreen);
            // 假设你的排行榜脚本叫 ZenRankScreen
            var rankScreen = window.GetComponent<ZenRankScreen>();
            if (rankScreen != null) rankScreen.SetSourcePanel(sourcePanel);
        }
    }
    
    public void CheckRankProgress()
    {
        bool isShow =
            GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.ZenOpenLevel ||
            GameDataManager.Instance.UserData.CurrentChessStage >= AppGameSettings.UnlockRequirements.ZenOpenLevel;
        
        //rankBtn.transform.parent.gameObject.SetActive(isShow);
        rankBtn.gameObject.SetActive(isShow);

        string zenName = MultilingualManager.Instance.GetString(GameDataManager.Instance.UserData.Zenlevel);
        zenLevelText.text = zenName;

        string zenLevel = UIUtilities.ExtractNumber(GameDataManager.Instance.UserData.Zenlevel);
        Sprite zenIcon = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("zenicon_"+zenLevel);
        if(zenIcon != null)
            rankImage.sprite = zenIcon;
        
        bool isJoined = GameDataManager.Instance.UserData.isJoinedZenRank;
        
        if (Game.IsNetworkActive)
        {
            wifi.SetActive(false);
            if (!isJoined)
            {
                rank.SetActive(false);
                if (rankText != null) rankText.text = "";
            }
            else
            {
                // 加入了榜单，才根据是否未上榜来显示角标
                rank.SetActive(!isUnranked);
            }
        }
        else
        {
            wifi.SetActive(true);
            rank.SetActive(false);
        }
    }
    // 🌟 新增：从 Manager 缓存直接恢复文字，避免显示 "..."
    public void SyncTextFromCache()
    {
        if (!Game.IsNetworkActive) return;

        // 状态 1：没加入
        if (!GameDataManager.Instance.UserData.isJoinedZenRank)
        {
            isUnranked = false;
            if (rank != null) rank.SetActive(false); // 🌟 直接隐藏整个角标
            if (rankText != null) rankText.text = "";
            return;
        }

        if (ZenRankManager.Instance != null)
        {
            // 状态 2：结算中
            if (ZenRankManager.Instance.RemainingSeconds == 0 && GameDataManager.Instance.UserData.isJoinedZenRank)
            {
                isUnranked = false;
                if (rank != null)
                {
                    rank.SetActive(true);
                    wifi.SetActive(false);
                }
                if (rankText != null) rankText.text = MultilingualManager.Instance.GetString("LotusRankingEnd") ?? "结算中";
                return;
            }

            // 状态 3：读取本地刚刚缓存的最新排名
            var myData = ZenRankManager.Instance.MyCurrentRankData;
            if (myData != null && myData.rank > 0)
            {
                isUnranked = false;
                if (rank != null)
                {
                    rank.SetActive(true);
                    wifi.SetActive(false);
                }
                if (rankText != null) rankText.text = "#" + myData.rank;
            }
            else if (myData != null && myData.rank <= 0) // 未上榜（如名次超过 1000）
            {
                isUnranked = true;
                if (rank != null) rank.SetActive(false);
            }
            else
            {
                isUnranked = true;
                if (rank != null) rank.SetActive(false);
                if (rankText != null) rankText.text = "";
            }
        }
    }
    /// <summary>
    /// 异步拉取一次我的最新排名，用于按钮展示
    /// </summary>
    public void FetchMyCurrentRank()
    {
        // if (!GameCoreManager.Instance.IsNetworkActive) return;
        // 状态 1：如果没加入，直接显示“未加入”
        if (!GameDataManager.Instance.UserData.isJoinedZenRank)
        {
            isUnranked = false;
            if (rank != null) rank.SetActive(false); // 🌟 直接隐藏
            if (rankText != null) rankText.text = "";
            // string notR = MultilingualManager.Instance.GetString("NotJoined");
            // if (notR == "NotJoined") notR = "未加入";
            // rankText.text = notR;
            return;
        }
        string boardId = GameDataManager.Instance.UserData.Zenlevel;
        if (string.IsNullOrEmpty(boardId)) return;
        
        StartCoroutine(APIGateway.Instance.LeaderboardApi.GetLeaderboard(boardId, (response) =>
        {
            Debug.Log("来自按钮的获取榜单 " + boardId);
            if (response != null)
            {
                ZenRankManager.Instance.StartGlobalTimer(response.remaining_seconds);
                // 👇=== 只有真实拿到服务器倒计时为 0 时，才判定为结算中 ===👇
                if (response.remaining_seconds <= 0)
                {
                    isUnranked = false;
                    rank.SetActive(true);
                    wifi.SetActive(false);
                    rankText.text = MultilingualManager.Instance.GetString("LotusRankingEnd") ?? "结算中";
                }
                else if (response.my != null && response.my.rank > 0)
                {
                    isUnranked = false;
                    rank.SetActive(true);
                    wifi.SetActive(false);
                    rankText.text = "#" + response.my.rank;
                }
                else
                {
                    isUnranked = true;
                    rank.SetActive(false);
                }
            }
            else
            {
                isUnranked = true;
                if (rankText != null) rankText.text = "";
                rank.SetActive(false);
            }
        }));
    }
    
    // ==========================================
    // 🌟 外部调用此方法，告诉按钮“你接收到了新的禅意”
    // ==========================================
    public void PlayAbsorbEffect(int addZenCount)
    {
        // 1. 播放按钮的震动反馈（被莲花砸中的物理感）
        // DOPunchScale(震动强度, 震动时间)
        transform.DOPunchScale(Vector3.one * 0.2f, 0.4f, 10, 1f).OnComplete(() =>
        {
            // 确保震动结束后比例恢复正常
            transform.localScale = Vector3.one;
        });

        // 2. 可以在这里顺便做个数字跳动的特效，或者直接刷新 UI
        // （这里假设你的 CheckRankProgress 能拉取到最新的本地数据来刷新界面）
        CheckRankProgress();
        
        // 可选：播放一声清脆的“叮”音效
        // AudioManager.Instance.PlaySoundEffect("ZenAbsorb");
    }
    private void OnDisable()
    {
        // 🌟 修复：注销倒计时监听，防止内存泄漏
        if (ZenRankManager.Instance != null)
        {
            ZenRankManager.Instance.OnRankTimerTick -= OnTimerTick;
        }
    }
}
