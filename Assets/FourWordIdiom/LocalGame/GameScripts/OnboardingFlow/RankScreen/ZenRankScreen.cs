using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    [SerializeField] private GameObject Uptag;
    [SerializeField] private GameObject Downtag;
    [SerializeField] private Text TimeText;
    [Header("空榜单提示")]
    [Tooltip("当该段位没有任何人上榜时显示的提示物件")]
    [SerializeField] private GameObject EmptyStateObj;

    [Header("我的排名信息")]
    [SerializeField] private Image MyRankIcon;
    [SerializeField] private Text MyRank;
    [SerializeField] private Image MyAvatar;
    [SerializeField] private Text MyName;
    [SerializeField] private GameObject MyLevel;
    [SerializeField] private Text MyScore;
    [SerializeField] private Text ZenTitle;
    
    private ZenLevelState currrentState;
    private readonly List<ZenRankState> TopRanks = new List<ZenRankState>();
    private readonly List<ZenRankState> MiddleRanks = new List<ZenRankState>();
    private readonly List<ZenRankState> BottomRanks = new List<ZenRankState>();

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
    // 声明一个协程引用，方便随时停止
    private Coroutine countdownCoroutine;
    // 判断是否上榜
    private bool isMeUnranked = false;
    protected override void Awake()
    {
        base.Awake();
        
        topScrollRect = GetComponentInChildren<ScrollRect>();
        
        // 🌟 移除原有的吸附组件监听，改为自由滑动
        HorizontalScrollSnap snap = GetComponentInChildren<HorizontalScrollSnap>(true);
        if (snap != null) Destroy(snap); // 如果有吸附组件，直接干掉它，实现自由滑动
        
        if (topScrollRect != null)
        {
            // 每次玩家手指滑动，都会触发 OnScrollValueChanged
            topScrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }
    }
    
    private IEnumerator Start()
    {
        title.text = MultilingualManager.Instance.GetString("MeditationList");
        ZenTitle.text = MultilingualManager.Instance.GetString("ZenValue");
   
        
        if (RanklvProfab == null)
        {
            RanklvProfab = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "ZenRankLvItem").GetComponent<ZenRankLevelItem>();
        }
        LevelObjectPool = new ObjectPool(RanklvProfab.gameObject, ObjectPool.CreatePoolContainer(transform, "ZenRankLvPool"));

        if (RankProfab == null)
        {
            RankProfab = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "ZenRankItem").GetComponent<ZenRankItem>();
        }
        RankObjectPool = new ObjectPool(RankProfab.gameObject, ObjectPool.CreatePoolContainer(transform, "ZenRankItemPool"));
        
        if (hehuaPrefab == null)
        {
            hehuaPrefab = AssetBundleLoader.SharedInstance.LoadGameObject("ZenHehua", "UI_hehua");
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
        
    }
    
    /// <summary>
    /// 在数据回来之前，把界面上的占位符全部清空或重置为加载状态
    /// </summary>
    private void ResetUIBeforeLoading()
    {
        EmptyStateObj.SetActive(false);
        
        // 1. 清空我的个人信息占位符
        MyRank.text = "-";
        MyName.text = "加载中..."; // 或者显示空字符串 ""
        MyScore.text = "-";
        TimeText.text = "--m--s";
        MyRankIcon.gameObject.SetActive(false);
        // 隐藏排名的段位图标
        if (MyLevel != null)
        {
            MyLevel.transform.GetChild(0).gameObject.SetActive(false);
            MyLevel.transform.GetChild(1).gameObject.SetActive(false);
        }

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
        yield return StartCoroutine(ZenRankManager.Instance.CheckAndShowSettlementRoutine());
        
        // 🌟 2. 此时肯定没有弹窗了，数据也肯定是最新的了。直接刷新 UI！
        string latestLevelCode = GameDataManager.Instance.UserData.Zenlevel;
        currrentState = ZenRankManager.Instance.ZenStates.FirstOrDefault(s => s.Code == latestLevelCode) 
                        ?? ZenRankManager.Instance.ZenStates[0];
        
        CenterLevelItemImmediately(currrentState);
        RefreshHehuaDisplay(ZenRankManager.Instance.ZenStates.IndexOf(currrentState));
        
        // 4. 让顶部的水平滑动列表跳到正确的段位页
        // HorizontalScrollSnap snapScript = GetComponentInChildren<HorizontalScrollSnap>(true);
        // if (snapScript != null)
        // {
        //     int targetIndex = zenStates.IndexOf(currrentState);
        //     snapScript.SetCurrentIndex(targetIndex);
        //     RefreshHehuaDisplay(targetIndex);
        // }

        // 5. 正式请求排行榜详细数据并刷新条目
        StartCoroutine(UpdateRankItems());
        
    }
    
    // 播放升降级特效
    private void PlayLevelChangeEffect(string type)
    {
        if (type == "up")
        {
            Debug.Log("✨ 播放升级华丽特效");
            // Instantiate(UpgradeEffectPrefab, LevelParent); // 你的粒子特效
            // AudioManager.Instance.PlaySoundEffect("LevelUp");
        }
        else if (type == "down")
        {
            Debug.Log("💧 播放降级特效");
            // AudioManager.Instance.PlaySoundEffect("LevelDown");
        }
    }
    
    // 🌟 自由滑动模式下，玩家点击某个段位触发刷新榜单
    private void OnLevelItemClicked(ZenLevelState state)
    {
        if (currrentState == state) return;

        currrentState = state;
        
        // 点击后只刷新荷花和榜单数据，不再强行把列表吸附过去
        RefreshHehuaDisplay(ZenRankManager.Instance.ZenStates.IndexOf(state));
        StartCoroutine(UpdateRankItems());
    }
    // 🌟 新增方法：实时计算玩家滑到了哪里，并刷新荷花
    private void OnScrollValueChanged(Vector2 pos)
    {
        if (ZenRankManager.Instance.ZenStates.Count <= 1) return;

        // 把 pos.x 限制在 0~1 之间
        float normalizedX = Mathf.Clamp01(pos.x);
        
        // 根据比例计算当前屏幕正中央是第几个段位的索引
        int centerIndex = Mathf.RoundToInt(normalizedX * (ZenRankManager.Instance.ZenStates.Count - 1));
        
        // 实时刷新荷花的显隐和剪影！
        RefreshHehuaDisplay(centerIndex);
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
            StartCoroutine(UpdateRankItems());
        }
    }

    protected override void InitializeUIComponents()
    {
        base.InitializeUIComponents();
        BackBtn.AddClickAction(OnClickBack);
        HelpBtn.AddClickAction(()=> SystemManager.Instance.ShowPanel(PanelType.ZenRankHelpScreen));
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
        // HorizontalScrollSnap snapScript = GetComponentInChildren<HorizontalScrollSnap>(true);
        // if (snapScript != null)
        // {
        //     snapScript.RefreshLayout(); // 让它重新计算真正的步长和最大页数
        //     // snapScript.SetCurrentIndex(zenStates.IndexOf(currrentState)); // 然后再跳转到目标页
        // }
        
        // int targetIndex = zenStates.IndexOf(currrentState);
        // GetComponentInChildren<HorizontalScrollSnap>(true).SetCurrentIndex(targetIndex);
        //
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

        yield return GetZenRankData(currrentState.Code);
      
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
        
        if(shouldShowPrompt)
            yield break;
        
        if (TopRanks.Count > 0)
        {
            foreach (var state in TopRanks)
            {
                GameObject itemObj = RankObjectPool.GetObject(RankParent);
                ZenRankItem item = itemObj.GetComponent<ZenRankItem>();
                state.Level = currrentState.Name ?? "ZenState01";
                var rewardConfig = ZenRankManager.Instance.RewardDatas.FirstOrDefault(r => r.State == currrentState.Id && r.Rank == state.Rank);
                if (rewardConfig != null && rewardConfig.rewards.TryGetValue(0, out var reward))
                {
                    state.Reward = reward;
                }
                else
                    state.Reward = 0;
                
                item.SetRankInfo(state);
            }
            GameObject upTagObj = Instantiate(Uptag, RankParent);
            upTagObj.name = Uptag.name;
        }
        foreach(var state in MiddleRanks)
        {
            GameObject itemObj = RankObjectPool.GetObject(RankParent);
            ZenRankItem item = itemObj.GetComponent<ZenRankItem>();
            state.Level = currrentState.Name ?? "ZenState01";
            var rewardConfig = ZenRankManager.Instance.RewardDatas.FirstOrDefault(r => r.State == currrentState.Id && r.Rank == state.Rank);
            if (rewardConfig != null && rewardConfig.rewards.TryGetValue(0, out var reward)) 
            {
                state.Reward = reward;
            }
            else
                state.Reward = 0;
            item.SetRankInfo(state);
        }
       
        if(BottomRanks.Count > 0)
        {
            GameObject downTagObj = Instantiate(Downtag, RankParent);
            downTagObj.name = Downtag.name;
            
            foreach(var state in BottomRanks)
            {
                GameObject itemObj = RankObjectPool.GetObject(RankParent);
                ZenRankItem item = itemObj.GetComponent<ZenRankItem>();
                state.Level = currrentState.Name ?? "ZenState01";
                var rewardConfig = ZenRankManager.Instance.RewardDatas.FirstOrDefault(r => r.State == currrentState.Id && r.Rank == state.Rank);
                if (rewardConfig != null && rewardConfig.rewards.TryGetValue(0, out var reward)) 
                {
                    state.Reward = reward;
                }
                else
                    state.Reward = 0;
                
                item.SetRankInfo(state);
            }
        }
    }
    private IEnumerator GetZenRankData(string boardId)
    {
        bool isCompleted = false;
        yield return APIGateway.Instance.LeaderboardApi.GetLeaderboard(boardId,(res) =>
        {
            if (res != null)
            {
                // 处理排行榜数据
                TopRanks.Clear();
                MiddleRanks.Clear();
                BottomRanks.Clear();
              
                    foreach (var entry in res.top)
                    {
                        TopRanks.Add(new ZenRankState
                        {
                            Rank = entry.rank,
                            Avatar = entry.avatar, // 示例头像
                            Name = entry.nickname,
                            Level = entry.leaderboard_name,
                            Score = entry.score
                        });
                    }
                
                
                    foreach (var entry in res.middle)
                    {
                        MiddleRanks.Add(new ZenRankState
                        {
                            Rank = entry.rank,
                            Avatar = entry.avatar, // 示例头像
                            Name = entry.nickname,
                            Level = entry.leaderboard_name,
                            Score = entry.score
                        });
                    }
                
                
                    foreach (var entry in res.bottom)
                    {
                        BottomRanks.Add(new ZenRankState
                        {
                            Rank = entry.rank,
                            Avatar = entry.avatar, // 示例头像
                            Name = entry.nickname,
                            Level = entry.leaderboard_name,
                            Score = entry.score
                        });
                    }
                

                // 处理我的排名数据
                HandleMyRank(res.my);
                FormatLostTime(res.remaining_seconds);
            }
            else
            {
                HandleMyRank(null);
            }
            isCompleted = true;
        });
        yield return new WaitUntil(() => isCompleted);
    }

    private void HandleMyRank(LeaderboardEntry entry)
    {
        if (entry == null || entry.rank == 0)
        {
            isMeUnranked = true; // 🌟 我未上榜
            MyRank.text = "-";
            MyAvatar.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("head" + GameDataManager.Instance.UserData.UserHeadId); // 给个默认头像
            MyName.text =  "未上榜";
            MyScore.text = "0";
            MyRankIcon.gameObject.SetActive(false);
            MyLevel.transform.GetChild(0).gameObject.SetActive(false);
            MyLevel.transform.GetChild(1).gameObject.SetActive(false);
            return;
        }
        isMeUnranked = false; // 🌟 我已经上榜了
        MyRank.gameObject.SetActive(false);
        MyRankIcon.gameObject.SetActive(false);
        switch (entry.rank)
        {
            case 1:
               
                MyRankIcon.gameObject.SetActive(true);
                MyRankIcon.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Rankicon1");
                break;
            case 2:
                MyRankIcon.gameObject.SetActive(true);
                MyRankIcon.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Rankicon2");
                break;
            case 3:
                MyRankIcon.gameObject.SetActive(true);
                MyRankIcon.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Rankicon3");
                break;
            default:
                MyRank.gameObject.SetActive(true);
                MyRank.text = entry.rank.ToString();
                break;
        }
        
        MyAvatar.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("head" + GameDataManager.Instance.UserData.UserHeadId);
        MyName.text = GameDataManager.Instance.UserData.UserName;
        MyScore.text = entry.score.ToString();
        MyLevel.transform.GetChild(0).gameObject.SetActive(false);
        MyLevel.transform.GetChild(1).gameObject.SetActive(false);
        switch (entry.rank)
        {
            case 1:
            case 2:
            case 3:
                MyLevel.transform.GetChild(0).GetComponentInChildren<Image>().sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("RankBox" + entry.rank);
                MyLevel.transform.GetChild(0).gameObject.SetActive(true);
                break;
            default:
                var rewardConfig = ZenRankManager.Instance.RewardDatas.FirstOrDefault(r => r.State == currrentState.Id && r.Rank == entry.rank);
                if (rewardConfig != null && rewardConfig.rewards.TryGetValue(0, out var reward)) 
                {
                    MyLevel.transform.GetChild(1).GetComponentInChildren<Text>().text = "×" +reward.ToString();
                    MyLevel.transform.GetChild(1).gameObject.SetActive(true);
                }
                break;
        }
    }
   
    private void TakeBackRankItem()
    {
        TopRanks.Clear();
        MiddleRanks.Clear();
        BottomRanks.Clear();
        
        for (int i = RankParent.childCount - 1; i >= 0; i--)
        {
            Transform child = RankParent.GetChild(i);

            // 如果是手动实例化的分割线（Uptag），彻底销毁
            if (Uptag != null && child.name.Contains(Uptag.name))
            {
                Destroy(child.gameObject);
            }
            // 如果是手动实例化的分割线（Downtag），彻底销毁
            else if (Downtag != null && child.name.Contains(Downtag.name))
            {
                Destroy(child.gameObject);
            }
            // 如果是普通的排行榜玩家 Item
            else
            {
                // 绝对不要 Destroy，安全放回对象池
                ObjectPool.ReturnObjectToPool(child.gameObject);
            }
        }
    }
  
    private void FormatLostTime(int remainingSeconds)
    {
        // 停掉可能正在跑的老倒计时
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
        }
        if (remainingSeconds > 0)
        {
            countdownCoroutine = StartCoroutine(StartCountdownRoutine(remainingSeconds));
        }
        else
        {
            TimeText.text =  "结算中";
        }
    }
// 🌟 纯本地脱机跑秒，与系统时钟无关，绝不出现负数
    private IEnumerator StartCountdownRoutine(float remainingSeconds)
    {
        WaitForSeconds wait = new WaitForSeconds(1f); 
        
        String day = MultilingualManager.Instance.GetString("TimeD") ?? "d ";
        String hour = MultilingualManager.Instance.GetString("TimeH") ?? "h ";
        String minute = MultilingualManager.Instance.GetString("TimeM") ?? "m";
        String second = MultilingualManager.Instance.GetString("TimeS") ?? "s";

        while (remainingSeconds > 0)
        {
            TimeSpan ts = TimeSpan.FromSeconds(remainingSeconds);

            if (ts.TotalDays >= 1)
            {
                TimeText.text = $"{ts.Days}{day}{ts.Hours:D2}{hour}";
            }
            else if (ts.TotalHours >= 1)
            {
                TimeText.text = $"{(int)ts.TotalHours}{hour}{ts.Minutes:D2}{minute}";
            }
            else
            {
                // 最后不到一小时，显示分和秒的跳动
                TimeText.text = $"{ts.Minutes:D2}{minute}{ts.Seconds:D2}{second}";
            }

            yield return wait; 
            remainingSeconds -= 1f; // 纯数值递减
        }

        TimeText.text = MultilingualManager.Instance.GetString("Settling") ?? "结算中...";
        yield return new WaitForSeconds(2f);
        StartCoroutine(CheckSettlementAndLoadRank());
    }

    private void OnClickBack()
    {
        
        if (GameCoreManager.Instance.PanelState == PanelState.MainMenuPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
        }
        else if (GameCoreManager.Instance.PanelState == PanelState.FinishHexPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.StageFinishView);
        }else if (GameCoreManager.Instance.PanelState == PanelState.FinishPingPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.ChessFinishView);
        }else if (GameCoreManager.Instance.PanelState == PanelState.GameHexPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.HexGamePlayArea);
        }
        else if (GameCoreManager.Instance.PanelState == PanelState.GamePingPanel)
        {
            SystemManager.Instance.HidePanel(PanelType.ChessPlayArea);
        }
        if (SystemManager.Instance.PanelIsShowing(PanelType.ButterflyHome))
        {
            Debug.Log("是否存在？" + PanelType.ButterflyHome);
            SystemManager.Instance.HidePanel(PanelType.ButterflyHome);
        }
        SystemManager.Instance.HidePanel(PanelType.ZenRankScreen, true, () =>
        {
            SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
        });
   
    }
    protected override void OnDisable()
    {
        if (countdownCoroutine != null) StopCoroutine(countdownCoroutine); // 🌟 加上这句防泄漏
        TakeBackRankItem();
        // GetComponentInChildren<HorizontalScrollSnap>(true).OnEventTriggered -= OnScrollRollingListen;
        base.OnDisable();
    }
}
