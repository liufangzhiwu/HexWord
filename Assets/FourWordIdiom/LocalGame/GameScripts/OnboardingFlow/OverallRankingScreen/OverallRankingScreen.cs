using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public enum RankTabType
{
    World,      // 世界总榜
    Monthly,    // 本月榜
    HallOfFame  // 禅贤阁
}

public class OverallRankingScreen : UIWindow
{
    [Header("Top Bar UI")]
    [SerializeField] private Button backHome;
    [SerializeField] private Button helpButton;
    
    [Header("Banner Component")]
    [SerializeField] private OverallRankingBannerUI bannerUI;
    [Header("Tabs & Content UI")]
    [SerializeField] private Toggle worldTab;
    [SerializeField] private Toggle monthlyTab;
    [SerializeField] private Toggle hallOfFameTab;
    
    [SerializeField] private GameObject worldAndMonthlyPanel;
    [SerializeField] private OverallRankItem myRankItem;   // 世界和月榜都有,世界不显示奖励,月榜显示奖励
    [SerializeField] private GameObject hallOfFamePanel;
    [Header("世界榜")]
    [SerializeField] private Transform worldAndMonthlyListContent;
    [SerializeField] private MainRankingList mainRankingList;
    [Header("名人堂")]
    [SerializeField] private Transform hallOfFameContent;
    [SerializeField] private HallOfFameList hallOfFameList;

    [Header("更新时间提示文本")] 
    [SerializeField] private Text footerTipsText; // 底部提示文本："20分钟刷新一次" 等
    
    [Header("未进榜展位图")]
    [SerializeField] private GameObject emptyStatePanel;  // 未进榜展示图
    [SerializeField] private GameObject networkErrorIcon;  // 没有网络展示的图, 在未进榜站位图中
    [SerializeField] private GameObject lockedLotusIcon;       // 莲花图标, 与未进榜文本一起显示
    [SerializeField] private Text emptyStateText;          // 图中的提示

    private RankTabType _currentTab = RankTabType.HallOfFame;


    protected override void Awake()
    {
        base.Awake();
        
        ClearEditorTemplates(worldAndMonthlyListContent);
        ClearEditorTemplates(hallOfFameContent);
    }
    
    // 销毁 Content 下的所有原生子节点（非对象池生成的模板）
    private void ClearEditorTemplates(Transform contentParent)
    {
        if (contentParent == null) return;
        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Destroy(contentParent.GetChild(i).gameObject);
        }
    }
    protected override void InitializeUIComponents()
    {
        base.InitializeUIComponents();
        backHome.AddClickAction(OnClickBack);
        helpButton.AddClickAction(()=> SystemManager.Instance.ShowPanel(PanelType.OverallRankingHelp));
        worldTab.onValueChanged.AddListener((isOn) => { if(isOn) SwitchTab(RankTabType.World); });
        monthlyTab.onValueChanged.AddListener((isOn) => { if(isOn) SwitchTab(RankTabType.Monthly); });
        hallOfFameTab.onValueChanged.AddListener((isOn) => { if(isOn) SwitchTab(RankTabType.HallOfFame); });
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        
        bannerUI.RefreshBannerInfo();
        // 1. 获取玩家分数与各榜单的解锁条件
        int myScore = GameDataManager.Instance.UserData.overallZenScore;
        int worldUnlockScore = OverallRankingManager.Instance.RankConfig.TotalRankUnlockScore;
        int monthlyUnlockScore = OverallRankingManager.Instance.RankConfig.MonthlyRankUnlockScore;
        RankTabType targetTab = RankTabType.World;
        if (myScore < worldUnlockScore)
        {
            if (myScore >= monthlyUnlockScore)
            {
                targetTab = RankTabType.Monthly; // 月榜已达标，降级看月榜
            }
        }
        worldTab.isOn = false;
        monthlyTab.isOn = false;
        hallOfFameTab.isOn = false;
        if (targetTab == RankTabType.World) 
        {
            worldTab.isOn = true;
        }
        else if (targetTab == RankTabType.Monthly) 
        {
            monthlyTab.isOn = true;
        }
        else 
        {
            hallOfFameTab.isOn = true;
        }
        // 默认打开世界榜
        // SwitchTab(_currentTab);
        AnalyticMgr.ZenRankEnter("禅意榜");
    }

    private IEnumerator CheckOpenHelp()
    {
        yield return new WaitForSeconds(0.5f);
        if (!GameDataManager.Instance.OverallRank.HasShowZenHelp)
        {
            SystemManager.Instance.ShowPanel(PanelType.OverallRankingHelp);
            GameDataManager.Instance.OverallRank.HasShowZenHelp = true;
        }

        yield return new WaitUntil(() =>
            GameDataManager.Instance.OverallRank.HasShowZenHelp &&
            !SystemManager.Instance.PanelIsShowing(PanelType.OverallRankingHelp));
        
        if (string.IsNullOrEmpty(GameDataManager.Instance.UserData.UserName))
        {
         
            if (!GameDataManager.Instance.OverallRank.HasShowName)
            {
                // 弹出起名/奖励面板
                SystemManager.Instance.ShowPanel(PanelType.RewardNamePanel);
                GameDataManager.Instance.OverallRank.HasShowName = true;
            }
            else
            {
                // 弹窗次数已用完，提示用户手动前往设置
                MessageSystem.Instance.ShowTip("请前往设置昵称后再查看排行榜");
            }
        }
    }
    // ==========================================
    // 🌟 Tab 切换与数据加载调度
    // ==========================================
    private void SwitchTab(RankTabType tabType)
    {
        _currentTab = tabType;
        int myScore = GameDataManager.Instance.UserData.overallZenScore;
        bool isUnlocked = true;
        
        Debug.Log($"[RankingDebug] ==== 切换到页签: {tabType} ==== 我的当前禅意分: {myScore}");
        // 1. 优先判断当前请求的页签是否在 Manager 中存在有效的缓存
        bool hasCache = false;
        if (tabType == RankTabType.World) hasCache = OverallRankingManager.Instance.IsTotalRankCached();
        else if (tabType == RankTabType.Monthly) hasCache = OverallRankingManager.Instance.IsMonthlyRankCached();
        else if (tabType == RankTabType.HallOfFame) hasCache = OverallRankingManager.Instance.IsHallOfFameCached();
        
        // 先检查是否有网络, 再检查是否达到开启条件
        if (!GameCoreManager.Instance.IsNetworkActive && !hasCache)
        {
            Debug.Log("[RankingDebug] 拦截：没有网络");
            ShowNoneState(true, true, MultilingualManager.Instance.GetString("PoorNetwork","hudie"));
            isUnlocked = false;
        }
        else if (tabType == RankTabType.World)
        {
            int unlockScore = OverallRankingManager.Instance.RankConfig.TotalRankUnlockScore;
            // int unlockScore = 10;
            Debug.Log($"[RankingDebug] 世界榜所需分数: {unlockScore}");
            if (myScore < unlockScore)
            {
                ShowNoneState(true, false, string.Format(MultilingualManager.Instance.GetString("OpenBoard","hudie"), unlockScore));
                isUnlocked = false;
            }
            else
            {
                ShowNoneState(false);
                footerTipsText.text = MultilingualManager.Instance.GetString("Refresh","hudie");
            }
        }
        else if (tabType == RankTabType.Monthly)
        {
            StartCoroutine(CheckOpenHelp());
            int unlockScore = OverallRankingManager.Instance.RankConfig.MonthlyRankUnlockScore;
            // int unlockScore = 10;
            Debug.Log($"[RankingDebug] 月榜所需分数: {unlockScore}");
            if (myScore < unlockScore)
            {
                ShowNoneState(true, false, string.Format(MultilingualManager.Instance.GetString("OpenBoard","hudie"), unlockScore));
                isUnlocked = false;
            }
            else
            {
                ShowNoneState(false);
                footerTipsText.text = MultilingualManager.Instance.GetString("Reset", "hudie");
            }
        }
        else if (tabType == RankTabType.HallOfFame)
        {
            StartCoroutine(CheckOpenHelp());
            ShowNoneState(false);
            footerTipsText.text = MultilingualManager.Instance.GetString("HallOfFameDesc", "hudie") ?? "历代宗师云集之地";
        }
        // 2. 核心优化：严格的互斥显隐控制
        if (isUnlocked)
        {
            // 面板显隐控制
            worldAndMonthlyPanel.SetActive(tabType is RankTabType.World or RankTabType.Monthly);
            hallOfFamePanel.SetActive(tabType == RankTabType.HallOfFame);
            
            bannerUI.SetTimer(false); // 默认关闭倒计时
            StartCoroutine(LoadTabDataRoutine(tabType));
        }
        else
        {
            worldAndMonthlyPanel.SetActive(false);
            hallOfFamePanel.SetActive(false);
            if (mainRankingList != null) mainRankingList.ClearAllUiItem();
            if (hallOfFameList != null) hallOfFameList.ClearAllUiItem();
        }
    }
    
    private void ShowNoneState(bool show, bool isWifiError = false, string text = "")
    {
        emptyStatePanel.SetActive(show);
        if (show)
        {
            networkErrorIcon.SetActive(isWifiError);
            lockedLotusIcon.SetActive(!isWifiError);
            emptyStateText.text = text;
        }
    }
    // ==========================================
    // 🌟 列表渲染逻辑
    // ==========================================
    private IEnumerator LoadTabDataRoutine(RankTabType tabType)
    {
        bool isCached = false; // 记录当前页签是否使用了缓存
        switch (tabType)
        {
            case RankTabType.World:
                isCached = OverallRankingManager.Instance.IsTotalRankCached();
                if (!isCached) MessageSystem.Instance.ShowLoadingAnimation(); // 仅在无缓存时显示
                yield return OverallRankingManager.Instance.FetchTotalRankRoutine();
                if (!isCached) MessageSystem.Instance.HideLoadingAnimation(); // 数据请求完毕后隐藏
                // 确保无论是新拉取的数据，还是20分钟内的缓存，都在渲染前将最新分数同步进去
                OverallRankingManager.Instance.SyncLocalScoreToCache();
                // 1. 将底层网络数据转换为 UI 展示数据
                var worldStates = new List<OverallRankState>();
                foreach (var entry in OverallRankingManager.Instance.TotalRanks)
                {
                    worldStates.Add(ConvertEntryToState(entry, false));
                }
                    
                // 2. 传给列表渲染
                mainRankingList.IsMonthly = false;
                mainRankingList.Initlize(worldStates, true); 
                // 3. 单独刷新底部的"我自己"的常驻UI
                UpdateMyRankItem(OverallRankingManager.Instance.MyTotalRankData, false);
                break;
            case RankTabType.Monthly:
                isCached = OverallRankingManager.Instance.IsMonthlyRankCached();
                if (!isCached) MessageSystem.Instance.ShowLoadingAnimation();
                yield return OverallRankingManager.Instance.FetchMonthlyRankRoutine();
                if (!isCached) MessageSystem.Instance.HideLoadingAnimation();
                
                // 假设后端返回了倒计时字段 (由于没有直接引用 Response，这里使用你 Manager 里的属性，如果没有请自行补充)
                bannerUI.SetTimer(true, OverallRankingManager.Instance.GetActualMonthlyRemainingSeconds()); 
                // 确保无论是新拉取的数据，还是20分钟内的缓存，都在渲染前将最新分数同步进去
                OverallRankingManager.Instance.SyncLocalScoreToCache();
                
                //  1. 转换数据
                var monthlyStates = new List<OverallRankState>();
                foreach (var entry in OverallRankingManager.Instance.MonthlyRanks)
                {
                    monthlyStates.Add(ConvertEntryToState(entry, true));
                }
                    
                //  2. 传给列表渲染
                mainRankingList.IsMonthly = true;
                mainRankingList.Initlize(monthlyStates, true);
                // 3. 单独刷新底部的"我自己"的常驻UI
                UpdateMyRankItem(OverallRankingManager.Instance.MyMonthlyRankData, true);
                break;

            case RankTabType.HallOfFame:
                isCached = OverallRankingManager.Instance.IsHallOfFameCached();
                if (!isCached) MessageSystem.Instance.ShowLoadingAnimation();
                yield return OverallRankingManager.Instance.FetchHallOfFameRoutine();
                if (!isCached) MessageSystem.Instance.HideLoadingAnimation();
                
                // 1. 按月份分组，并转换为循环列表需要的 HallOfFameGroupData 结构
                var list = OverallRankingManager.Instance.HallOfFameRanks;
                var groupedData = list.GroupBy(x => x.period_date).ToList();
                var hallOfFameStates = new List<HallOfFameGroupData>();
                foreach (var group in groupedData)
                {
                    hallOfFameStates.Add(new HallOfFameGroupData
                    {
                        Date = group.Key,
                        TopPlayers = group.OrderBy(x => x.rank).Take(3).Select(entry => new MonthlyTopPlayer
                        {
                            Rank = entry.rank,
                            Avatar = entry.avatar,
                            Name = entry.nickname,
                            Score = entry.score
                        }).ToList()
                    });
                }
                // 2. 注入名人堂循环列表并刷新
                hallOfFameList.Initlize(hallOfFameStates, true);
                break;
        }
    }
    // 辅助方法：控制底部自己排名的显示与隐藏
    private void UpdateMyRankItem(LeaderboardEntry myData, bool isMonthly)
    {
        if (myData != null && myData.rank > 0) 
        {
            myRankItem.gameObject.SetActive(true);
            myRankItem.SetRankInfo(ConvertEntryToState(myData, isMonthly), isMonthly, true);
        }
        else
        {
            myRankItem.gameObject.SetActive(false);
        }
    }
    // ==========================================
    // 🌟 数据转换与辅助工具
    // ==========================================
    private OverallRankState ConvertEntryToState(LeaderboardEntry entry, bool isMonthly)
    {
        var state = new OverallRankState
        {
            PlayerId = entry.user_id,
            Rank = entry.rank,
            Avatar = entry.avatar,
            Frame = entry.avatar_frame,
            Name = entry.nickname,
            Score = entry.score,
            Reward = 0
        };

        // 如果是月榜，根据排名附带金币奖励信息（前三名是宝箱，不需要金币数值）
        if (isMonthly && entry.rank >= 4)
        {
            // 这里为了演示写死 200，实际应从 OverallRankingManager.Instance.MonthlyRewards 查表获取
            state.Reward = 200; 
        }

        return state;
    }
    private void ClearPool(ObjectPool pool, Transform parent)
    {
        // 遍历 Content 下的所有活跃子节点并回收
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var poolObj = parent.GetChild(i).GetComponent<PoolObject>();
            if (poolObj != null)
            {
                pool.ReturnObjectToPool(poolObj);
            }
        }
    }
    private void OnClickBack()
    {
        if (GameCoreManager.Instance.PanelState == PanelState.MainMenuPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
        }
        else if (GameCoreManager.Instance.PanelState == PanelState.FinishXiaoPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.StageFinishView);
        }else if (GameCoreManager.Instance.PanelState == PanelState.FinishPingPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.ChessFinishView);
        }
        else if (GameCoreManager.Instance.PanelState == PanelState.GameXiaoPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.GamePlayArea);
        }
        else if (GameCoreManager.Instance.PanelState == PanelState.GameHexPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.HexGamePlayArea);
        }
        else if (GameCoreManager.Instance.PanelState == PanelState.GamePingPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.ChessPlayArea);
        }
        if (SystemManager.Instance.PanelIsShowing(PanelType.ButterflyHome))
        {
            SystemManager.Instance.HidePanel(PanelType.ButterflyHome);
        }
        SystemManager.Instance.HidePanel(PanelType.OverallRankingScreen, true, () =>
        {
            SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
            SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        });
   
    }
}
