using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ZenRankScreen : UIWindow
{
    [SerializeField] private Text title;
    [SerializeField] private RectTransform LeaderView;
    [SerializeField] private RectTransform RankView;
    [SerializeField] private Transform LevelParent;
    [SerializeField] private Transform RankParent;
    [Header("UI组件")]
    [SerializeField] private Button BackBtn;
    [SerializeField] private Button HelpBtn;
    [SerializeField] private Button LeftArrowBtn;
    [SerializeField] private Button RightArrowBtn;
    [SerializeField] private GameObject Uptag;
    [SerializeField] private GameObject Downtag;
    [SerializeField] private Text TimeText;
    
    [Header("加载与过渡效果")]
    [Tooltip("转圈圈的加载遮罩层，放在列表最上层")]
    [SerializeField] private GameObject LoadingMask;
    [Tooltip("给 RankParent 或者外层加上 CanvasGroup，用于数据出来的瞬间做淡入")]
    [SerializeField] private CanvasGroup RankListCanvasGroup;
    [Tooltip("加载旋转图片")]
    [SerializeField] private Image loadingImage;
    
    [Header("空榜单提示")]
    [Tooltip("当该段位没有任何人上榜时显示的提示物件")]
    [SerializeField] private GameObject EmptyStateObj;

    [Header("我的排名信息")]
    [SerializeField] private Image MyRankIcon;
    [SerializeField] private Text MyRank;
    [SerializeField] private Image MyAvatar;
    [SerializeField] private Text MyName;
    [SerializeField] private Text MyScore;
    [SerializeField] private Text ZenTitle;
    [SerializeField] private Button MyGoPlay;
    
    // 🌟 缓存的升降级标签实例
    private GameObject cachedUpTag;
    private GameObject cachedDownTag;
    
    private string _returnTargetPanel = PanelType.PrimaryInterface;
    
    private ZenLevelState currrentState;
    private int currentLevelIndex = 0;
    private readonly List<ZenRankLevelItem> createdLevelItems = new List<ZenRankLevelItem>();
    
    private ZenRankLevelItem RanklvProfab;
    private ZenRankItem RankProfab;

    private ObjectPool LevelObjectPool;
    private ObjectPool RankObjectPool;
    
    // Start is called before the first frame update
    
    private ObjectPool hehuaObjectPool;
    private GameObject hehuaPrefab;
    
    // 顶部的滑动组件
    private ScrollRect topScrollRect;
   
    // 判断是否上榜
    private bool isMeUnranked = false;
    private bool isFirstLoad = true;
    
    private HorizontalScrollSnap scrollSnap;
    protected override void Awake()
    {
        base.Awake();
        
        topScrollRect = GetComponentInChildren<ScrollRect>();
        
        // 移除原有的吸附组件监听，改为自由滑动
        scrollSnap = GetComponentInChildren<HorizontalScrollSnap>(true);
        if (scrollSnap != null) scrollSnap.OnEventTriggered += OnScrollRollingListen; 
        
        if (topScrollRect != null)
        {
            // 每次玩家手指滑动，都会触发 OnScrollValueChanged
            topScrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }
        if (Uptag != null)
        {
            cachedUpTag = Instantiate(Uptag, RankParent);
            cachedUpTag.SetActive(false);
            cachedUpTag.GetComponentInChildren<Text>(true).text = MultilingualManager.Instance.GetString("ImproveRanking");
        }
        if (Downtag != null)
        {
            cachedDownTag = Instantiate(Downtag, RankParent);
            cachedDownTag.SetActive(false);
            cachedDownTag.GetComponentInChildren<Text>(true).text = MultilingualManager.Instance.GetString("LevelDecline");
        }
    }
    
    private IEnumerator Start()
    {
        //title.text = MultilingualManager.Instance.GetString("MeditationList");
        // ZenTitle.text = MultilingualManager.Instance.GetString("ZenValue");
        MyGoPlay.AddClickAction(OnGoPlayClicked);
        
        if (RanklvProfab == null)
        {
            RanklvProfab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "ZenRankLvItem").GetComponent<ZenRankLevelItem>();
        }
        LevelObjectPool = new ObjectPool(RanklvProfab.gameObject, ObjectPool.CreatePoolContainer(transform, "ZenRankLvPool"));

        if (RankProfab == null)
        {
            RankProfab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "ZenRankItem").GetComponent<ZenRankItem>();
        }
        RankObjectPool = new ObjectPool(RankProfab.gameObject, ObjectPool.CreatePoolContainer(transform, "ZenRankItemPool"));
        
        if (hehuaPrefab == null)
        {
            hehuaPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("ZenHehua", "UI_hehua");
        }
        hehuaObjectPool = new ObjectPool(hehuaPrefab, ObjectPool.CreatePoolContainer(transform, "ZenHehuaPool"));
        yield return StartCoroutine(UpdateAllLevelItems());
    }
 
    protected override void OnEnable()
    {
        base.OnEnable();
        ResetUIBeforeLoading();
        StartCoroutine(CheckSettlementAndLoadRank());
        // GetComponentInChildren<HorizontalScrollSnap>(true).OnEventTriggered += OnScrollRollingListen;
        
        // 🌟 注册事件
        if (ZenRankManager.Instance != null)
            ZenRankManager.Instance.OnRankTimerTick += UpdateTimerUI;
    }
    // 🌟 新增：根据当前索引，动态更新左右箭头的显示状态
    private void UpdateArrowVisibility()
    {
        if (scrollSnap == null || ZenRankManager.Instance.ZenStates.Count == 0) return;

        int currentIndex = scrollSnap.GetCurrentIndex();
        int maxIndex = ZenRankManager.Instance.ZenStates.Count - 1;

        // 当前索引 > 0 说明左边还有，显示左箭头
        if (LeftArrowBtn != null)
        {
            LeftArrowBtn.gameObject.SetActive(currentIndex > 0);
        }

        // 当前索引 < 最大索引说明右边还有，显示右箭头
        if (RightArrowBtn != null)
        {
            RightArrowBtn.gameObject.SetActive(currentIndex < maxIndex);
        }
    }
    /// <summary>
    /// 在数据回来之前，把界面上的占位符全部清空或重置为加载状态
    /// </summary>
    private void ResetUIBeforeLoading()
    {
        EmptyStateObj.SetActive(false);
        StartLoading();
        // 1. 清空我的个人信息占位符
        MyRank.text = "-";
        MyName.text = "加载中..."; // 或者显示空字符串 ""
        MyScore.text = "-";
        TimeText.text = "--m--s";
        MyRankIcon.gameObject.SetActive(false);

        // 2. 清理掉编辑器里可能残留的列表假预制体
        // 虽然你有 TakeBackRankItem，但在 Awake/Start 的第一帧它还没执行
        for (int i = RankParent.childCount - 1; i >= 0; i--)
        {
            RankParent.GetChild(i).gameObject.SetActive(false);
        }
    }

    // 🌟 核心逻辑：拦截结算弹窗
    private IEnumerator CheckSettlementAndLoadRank()
    {
        // 🌟 1. 直接复用 Manager 里的统一步骤（万一玩家是一直挂机在游戏里跨周的，这里依然能触发弹窗）
        yield return StartCoroutine(ZenRankManager.Instance.CheckAndShowSettlementRoutine(_returnTargetPanel));
        
        // 🌟 2. 此时肯定没有弹窗了，数据也肯定是最新的了。直接刷新 UI！
        string latestLevelCode = GameDataManager.Instance.UserData.Zenlevel;
        currrentState = ZenRankManager.Instance.ZenStates.FirstOrDefault(s => s.Code == latestLevelCode) 
                        ?? ZenRankManager.Instance.ZenStates[0];
        
        int targetIndex = ZenRankManager.Instance.ZenStates.IndexOf(currrentState);
        CenterLevelItemImmediately(currrentState);
        RefreshHehuaDisplay(targetIndex);
        
        // 4. 让顶部的水平滑动列表跳到正确的段位页
        HorizontalScrollSnap snapScript = GetComponentInChildren<HorizontalScrollSnap>(true);
        if (snapScript != null)
        {
            snapScript.SetCurrentIndex(targetIndex);
            RefreshHehuaDisplay(targetIndex);
        }

        // 5. 正式请求排行榜详细数据并刷新条目
        StartCoroutine(UpdateRankItems());
        
    }
    
    // 🌟 自由滑动模式下，玩家点击某个段位触发刷新榜单
    private void OnLevelItemClicked(ZenLevelState state)
    {
        if (currrentState == state) return;

        currrentState = state;
        
        // 点击后只刷新荷花和榜单数据，不再强行把列表吸附过去
        RefreshHehuaDisplay(ZenRankManager.Instance.ZenStates.IndexOf(state));
        // StartCoroutine(UpdateRankItems());
    }
    // 🌟 新增方法：实时计算玩家滑到了哪里，并刷新荷花
    private void OnScrollValueChanged(Vector2 pos)
    {
        if (ZenRankManager.Instance.ZenStates.Count <= 1) return;

        // 把 pos.x 限制在 0~1 之间
        float normalizedX = Mathf.Clamp01(pos.x);
        int maxIndex = ZenRankManager.Instance.ZenStates.Count - 1;
        // 根据比例计算当前屏幕正中央是第几个段位的索引
        int centerIndex = Mathf.RoundToInt(normalizedX * maxIndex);
        
        // 实时刷新荷花的显隐和剪影！
        RefreshHehuaDisplay(centerIndex);
        if (LeftArrowBtn != null)
        {
            LeftArrowBtn.gameObject.SetActive(centerIndex > 0);
        }

        if (RightArrowBtn != null)
        {
            RightArrowBtn.gameObject.SetActive(centerIndex < maxIndex);
        }
    }
    
    // ==========================================
    // 🌟 ScrollView 视口居中控制
    // ==========================================
    private void CenterLevelItemImmediately(ZenLevelState state)
    {
        if (topScrollRect == null) return;
        int index = ZenRankManager.Instance.ZenStates.IndexOf(state);
        if (index < 0) return;

        float normalizedPos = (float)index / Mathf.Max(1, ZenRankManager.Instance.ZenStates.Count - 1);
        topScrollRect.horizontalNormalizedPosition = normalizedPos;
    }
    private IEnumerator ScrollToLevelRoutine(ZenLevelState state)
    {
        if (topScrollRect == null) yield break;

        int index = ZenRankManager.Instance.ZenStates.IndexOf(state);
        float targetPos = (float)index / Mathf.Max(1, ZenRankManager.Instance.ZenStates.Count - 1);
        float startPos = topScrollRect.horizontalNormalizedPosition;
        
        // 平滑滚动 0.8 秒
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.8f;
            float easedT = 1f - Mathf.Pow(1f - t, 3f); // EaseOutCubic
            topScrollRect.horizontalNormalizedPosition = Mathf.Lerp(startPos, targetPos, easedT);
            yield return null;
        }
    }
    private void OnScrollRollingListen(int index)
    {
        if (index >= 0 && index < ZenRankManager.Instance.ZenStates.Count)
        {
            currrentState = ZenRankManager.Instance.ZenStates[index];
            RefreshHehuaDisplay(index);
            UpdateArrowVisibility();
            // StartCoroutine(UpdateRankItems());
        }
    }

    protected override void InitializeUIComponents()
    {
        base.InitializeUIComponents();
        BackBtn.AddVibraClickAction(OnClickBack);
        HelpBtn.AddClickAction(()=> SystemManager.Instance.ShowPanel(PanelType.ZenRankHelpScreen));
        LeftArrowBtn.AddClickAction(OnClickLeftArrow);
        RightArrowBtn.AddClickAction(OnClickRightArrow);
    }
    private void OnClickLeftArrow()
    {
        if (scrollSnap == null) return;
        int currentIndex = scrollSnap.GetCurrentIndex();
        scrollSnap.SetCurrentIndex(currentIndex - 1);
    }
    private void OnClickRightArrow()
    {
        if (scrollSnap == null) return;
        int currentIndex = scrollSnap.GetCurrentIndex();
        scrollSnap.SetCurrentIndex(currentIndex + 1);
    }
    private void OnGoPlayClicked()
    {
        // 点击自己的那条则进入游戏,
        SystemManager.Instance.HidePanel(PanelType.ZenRankScreen, true, () =>
        {
            UIWindow uiWindow = SystemManager.Instance.GetPanel(PanelType.PrimaryInterface);
            uiWindow?.GetComponent<PrimaryInterface>().OnPlayClick();
        });
    }
    // 更新所有关卡项
    private IEnumerator UpdateAllLevelItems()
    {
        yield return new WaitForEndOfFrame(); // 等待一帧，确保UI布局完成
        createdLevelItems.Clear();
        foreach (var state in ZenRankManager.Instance.ZenStates)
        {
            GameObject itemObj = LevelObjectPool.GetObject(LevelParent);
            ZenRankLevelItem item = itemObj.GetComponent<ZenRankLevelItem>();
            item.SetLevelInfo(state);
            createdLevelItems.Add(item);
        }
        // ==========================================
        // 🌟 核心修复：手动撑开 Content 的总宽度，打破空气墙！
        // ==========================================
        if (LevelParent != null && createdLevelItems.Count > 0)
        {
            // 获取单个 Item 的实际宽度 (你在 Item 脚本里设为了屏幕宽度)
            float singleItemWidth = createdLevelItems[0].GetComponent<RectTransform>().rect.width;
            
            // 计算总宽度 (也可以加上你要的水平间距 spacing)
            float totalWidth = singleItemWidth * createdLevelItems.Count;
            
            // 强行把 Content 拉长！
            LevelParent.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, totalWidth);
        }
        
        // 🌟 核心修复：数据全部塞完之后，手动通知滑动组件刷新参数！
        if (scrollSnap != null)
        {
            scrollSnap.RefreshLayout(); // 让它重新计算真正的步长和最大页数
            int targetIndex = ZenRankManager.Instance.ZenStates.IndexOf(currrentState);
            scrollSnap.SetCurrentIndex(targetIndex); // 然后再跳转到目标页
            UpdateArrowVisibility();
        }
        
        // // 🌟 初始刷新荷花的显隐
        // RefreshHehuaDisplay(targetIndex);
    }
    // 🌟 核心：计算并刷新荷花的按需加载
    private void RefreshHehuaDisplay(int centerIndex)
    {
        string myLevelCode = GameDataManager.Instance.UserData.Zenlevel;
        int myUnlockedIndex = ZenRankManager.Instance.ZenStates.FindIndex(s => s.Code == myLevelCode);
        if (myUnlockedIndex < 0) myUnlockedIndex = 0;
        
        for (int i = 0; i < createdLevelItems.Count; i++)
        {
            // 只有当前项，以及左边一个、右边一个 (距离 <= 1)，才显示沉重的 Spine 动画
            bool shouldShowHehua = Mathf.Abs(i - centerIndex) <= 1;
            
            // 🌟 只要该 Item 的索引 <= 玩家当前的索引，就是已解锁状态
            bool isUnlocked = i <= myUnlockedIndex;
            
            createdLevelItems[i].UpdateHehuaVisibility(shouldShowHehua, isUnlocked, i, hehuaObjectPool);
        }
    }
    // 更新排名项 当前code
    public IEnumerator UpdateRankItems()
    {
        TakeBackRankItem();
        
        yield return StartCoroutine(ZenRankManager.Instance.FetchLeaderboardDataRoutine(currrentState.Code));
        // 🌟 直接从 Manager 读取最新数据
        var TopRanks = ZenRankManager.Instance.TopRanks;
        var MiddleRanks = ZenRankManager.Instance.MiddleRanks;
        var BottomRanks = ZenRankManager.Instance.BottomRanks;
        var MyData = ZenRankManager.Instance.MyCurrentRankData;
        HandleMyRank(MyData);
        // ==========================================
        // 🌟 新增：检查是否有人上榜，控制空状态提示的显隐
        // ==========================================
        bool hasAnyPlayer = TopRanks.Count > 0 || MiddleRanks.Count > 0 || BottomRanks.Count > 0;
        bool shouldShowPrompt = isMeUnranked || !hasAnyPlayer;
        if (EmptyStateObj != null)
        {
            // 如果没有人上榜，显示提示；如果有人，隐藏提示
            EmptyStateObj.SetActive(shouldShowPrompt);
            EmptyStateObj.GetComponent<Text>().text = MultilingualManager.Instance.GetString("ZenStateTips01");
        }

        if (!shouldShowPrompt)
        {
            // 1. 生成顶部玩家
            foreach (var state in TopRanks) { SpawnRankItem(state); }
            // 2. 将 Up 标签移动到当前列表的最后面显示！
            if (TopRanks.Count > 0 && cachedUpTag != null)
            {
                cachedUpTag.SetActive(true);
                cachedUpTag.transform.SetAsLastSibling();
            }
            // 3. 生成中部玩家 (它们会自动排在 Up 标签的后面)
            foreach(var state in MiddleRanks) { SpawnRankItem(state); }
            // 4. 将 Down 标签移动到当前列表的最后面显示！
            if(BottomRanks.Count > 0 && cachedDownTag != null)
            {
                cachedDownTag.SetActive(true);
                cachedDownTag.transform.SetAsLastSibling();
            }
            // 5. 生成底部玩家
            foreach(var state in BottomRanks) { SpawnRankItem(state); }
        }
        
        // 🌟 数据拼装完毕，等待一帧让 LayoutGroup 自动排版好高度
        yield return new WaitForEndOfFrame();
        // 🌟 关闭 Loading 转圈，执行丝滑的淡入出现
        StopLoading();
    }
    // 提炼了一个公共的小方法，避免重复写三遍生成逻辑
    private void SpawnRankItem(ZenRankState state)
    {
        GameObject itemObj = RankObjectPool.GetObject(RankParent);
        ZenRankItem item = itemObj.GetComponent<ZenRankItem>();
        state.Level = currrentState.Name ?? "ZenState01";
        
        var rewardConfig = ZenRankManager.Instance.RewardDatas.FirstOrDefault(r => r.State == currrentState.Id && r.Rank == state.Rank);
        if (rewardConfig != null && rewardConfig.rewards.TryGetValue(0, out var reward))
            state.Reward = reward;
        else
            state.Reward = 0;
            
        item.SetRankInfo(state);
        // 关键一步：把新生成的玩家放到列表的最下面
        itemObj.transform.SetAsLastSibling(); 
    }

    private void HandleMyRank(LeaderboardEntry entry)
    {
        if (entry == null || entry.rank == 0)
        {
            isMeUnranked = true; // 🌟 我未上榜
            MyRank.text = "-";
            MyAvatar.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("head" + GameDataManager.Instance.UserData.UserHeadId); // 给个默认头像
            MyName.text =  "未上榜";
            MyScore.text = "0";
            MyRankIcon.gameObject.SetActive(false);
            return;
        }
        isMeUnranked = false; // 🌟 我已经上榜了
        MyRank.gameObject.SetActive(false);
        MyRankIcon.gameObject.SetActive(false);
        switch (entry.rank)
        {
            case 1:
               
                MyRankIcon.gameObject.SetActive(true);
                MyRankIcon.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Rankicon1");
                break;
            case 2:
                MyRankIcon.gameObject.SetActive(true);
                MyRankIcon.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Rankicon2");
                break;
            case 3:
                MyRankIcon.gameObject.SetActive(true);
                MyRankIcon.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Rankicon3");
                break;
            default:
                MyRank.gameObject.SetActive(true);
                MyRank.text = entry.rank.ToString();
                break;
        }
        
        MyAvatar.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("head" + GameDataManager.Instance.UserData.UserHeadId);
        MyName.text = GameDataManager.Instance.UserData.UserName;
        MyScore.text = entry.score.ToString();
    }
   
    private void TakeBackRankItem()
    {
        for (int i = RankParent.childCount - 1; i >= 0; i--)
        {
            Transform child = RankParent.GetChild(i);
            
            if (cachedUpTag != null && child.gameObject == cachedUpTag)
            {
                cachedUpTag.SetActive(false);
            }
            else if (cachedDownTag != null && child.gameObject == cachedDownTag)
            {
                cachedDownTag.SetActive(false);
            }
            // 如果是普通的排行榜玩家 Item
            else
            {
                // 绝对不要 Destroy，安全放回对象池
                ObjectPool.ReturnObjectToPool(child.gameObject);
            }
        }
    }
    /// <summary>
    /// 外部调用：设置关闭此界面时需要返回的面板
    /// </summary>
    public void SetSourcePanel(string sourcePanel)
    {
        _returnTargetPanel = sourcePanel;
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
        SystemManager.Instance.HidePanel(PanelType.ZenRankScreen, true, () =>
        {
            SystemManager.Instance.ShowPanel(_returnTargetPanel);
            SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        });
   
    }
    // 🌟 更新 UI
    private void UpdateTimerUI(int seconds, string timeStr)
    {
        if (TimeText != null) TimeText.text = timeStr;
    }
    
    /// <summary>
    /// 启动加载状态：显示遮罩、开始旋转、列表透明
    /// </summary>
    private void StartLoading()
    {
        if (LoadingMask != null) LoadingMask.SetActive(true);
        if (RankListCanvasGroup != null) RankListCanvasGroup.alpha = 0f;

        if (loadingImage != null)
        {
            loadingImage.transform.DOKill();
            loadingImage.transform.localRotation = Quaternion.identity;
            loadingImage.transform
                .DORotate(new Vector3(0, 0, -360), 1f, RotateMode.FastBeyond360)
                .SetLoops(-1, LoopType.Restart)
                .SetEase(Ease.Linear);
        }
    }
    /// <summary>
    /// 停止加载状态：隐藏遮罩、停止旋转、列表淡入
    /// </summary>
    private void StopLoading()
    {
        if (loadingImage != null) loadingImage.transform.DOKill();
        if (LoadingMask != null) LoadingMask.SetActive(false);
        if (RankListCanvasGroup != null)
        {
            RankListCanvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutCubic);
        }
    }
    protected override void OnDisable()
    {
        TakeBackRankItem();
        StopLoading();
        if (scrollSnap != null) scrollSnap.OnEventTriggered -= OnScrollRollingListen;
        if (ZenRankManager.Instance != null)
            ZenRankManager.Instance.OnRankTimerTick -= UpdateTimerUI;
        base.OnDisable();
    }
}
