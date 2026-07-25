using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class ZenRankChangePanel : UIWindow
{
    [Header("UI 核心组件")]
    [SerializeField] private CanvasGroup mainCanvasGroup;
    [SerializeField] private RectTransform topBanner;       // 顶部 "提升了X名" 横幅
    [SerializeField] private Text topBannerText;            // 横幅文字
    [SerializeField] private CanvasGroup continueBtn;       // 继续按钮
    
    [Header("段位信息区")]
    [SerializeField] private Text zenTitleText;             // 禅修榜标题
    [SerializeField] private Text zenTimeText;              // 当前榜单的剩余时间
    [SerializeField] private Image zenImage;                // 榜单等级图标 (荷花)
    [SerializeField] private Text zenNameText;              // 榜单名称 (枯淡界等)

    [Header("玩家列表区域")] 
    [SerializeField] private RectTransform listContainer;
    [Tooltip("列表滚动组件")]
    [SerializeField] private RectTransform contentRect;
    [SerializeField] private RectTransform glowFrame;       // 像Vita那样的发光/星星高亮框
    [Tooltip("从旁边飞入的莲花图标")]
    [SerializeField] private GameObject flyingLotus;   
    [Tooltip("莲花出现在玩家条目右侧的偏移量 (可根据 UI 宽度微调)")]
    [SerializeField] private float lotusOffsetX = 350f;
    
    [Header("动画与测试")]
    [SerializeField] private float itemSpacing = 120f;      // 列表中每个条目的间距
    [SerializeField] private float animationDelay = 0.5f;   // 界面打开后多久开始播动画

    private RectMask2D viewportMask;
    private ObjectPool _scoreLotusPool;
    // ==========================================
    // 🛡️ 大厂级优化：性能缓存区
    // ==========================================
    private GameObject rankItemPrefab;          // 动态加载的预制体
    private ObjectPool rankItemPool;
    private ZenRankItem myRankItem;             // 动态生成的玩家自己
    private List<ZenRankItem> otherPlayersPool = new List<ZenRankItem>(); // 动态生成的垫背玩家池
    // private GameObject cachedUpTag;
    // private GameObject cachedDownTag;
    
    private WaitForSeconds delayWait;
    private bool isAnimating = false;
    private Sequence currentSequence; // 缓存当前动画序列，方便统一 Kill
    
    [Header("重播按钮")]
    [SerializeField] private Button replayBtn;          // 拖拽赋值
    [SerializeField] private CanvasGroup replayBtnCanvasGroup; // 可选，控制显隐

    // 缓存重播参数
    private int cacheOldRank, cacheNewRank, cacheOldScore, cacheNewScore;
    private string cacheLevelCode, cacheLevelName;
    private int cacheRemainingSeconds;
    private List<ZenRankState> cachePassedPlayers;
    public void OnClickReplay()
{
    // 避免连点或在动画未完全结束时混乱
    if (replayBtn != null) replayBtn.interactable = false;
    
    // 1. 强制停止当前正在运行的动画序列
    if (currentSequence != null && currentSequence.IsActive())
        currentSequence.Kill();
    
    // 2. 杀掉所有相关物体的 DOTween 动画
    KillAllTweens();
    
    // 3. 重置 UI 视觉状态（回到动画前的样子）
    ResetUIForReplay();
    
    // 4. 用缓存的参数重新播放
    PlayRankChange(cacheOldRank, cacheNewRank, cacheOldScore, cacheNewScore,
                   cacheLevelCode, cacheLevelName, cacheRemainingSeconds, cachePassedPlayers);
    
    // 播放开始后可以再次允许重播（或等动画结束再允许，按需设定）
    replayBtn.interactable = true;
}

private void KillAllTweens()
{
    // 涵盖所有会动的对象
    if (myRankItem != null)
    {
        myRankItem.GetComponent<RectTransform>().DOKill();
        if (myRankItem.ScoreText != null) myRankItem.ScoreText.transform.DOKill();
    }
    foreach (var p in otherPlayersPool)
        p.GetComponent<RectTransform>()?.DOKill();
    
    contentRect.DOKill();
    mainCanvasGroup.DOKill();
    topBanner.DOKill();
    if (glowFrame != null) glowFrame.DOKill();
    // if (cachedUpTag != null) cachedUpTag.transform.DOKill();
    // if (cachedDownTag != null) cachedDownTag.transform.DOKill();
    continueBtn.transform.DOKill();
}

private void ResetUIForReplay()
{
    // 隐藏特效
    if (glowFrame != null) glowFrame.gameObject.SetActive(false);
    // if (cachedUpTag != null) cachedUpTag.SetActive(false);
    // if (cachedDownTag != null) cachedDownTag.SetActive(false);
    
    // 恢复缩放（重点：之前我们放大过）
    if (myRankItem != null)
    {
        RectTransform myRect = myRankItem.GetComponent<RectTransform>();
        myRect.localScale = Vector3.one;
        // 如有层级变动，恢复默认（设为最后一个兄弟可能导致布局错乱，视需求决定）
        // myRect.SetSiblingIndex(0); 
    }
    foreach (var p in otherPlayersPool)
    {
        if (p != null)
        {
            p.GetComponent<RectTransform>().localScale = Vector3.one;
            p.gameObject.SetActive(false); // 等 PlayRankChange 会重新激活
        }
    }
    
    // 重置容器位置
    contentRect.anchoredPosition = Vector2.zero;
    mainCanvasGroup.alpha = 1;
    continueBtn.alpha = 0;
    continueBtn.interactable = false;
    topBanner.anchoredPosition = new Vector2(0, 500f);
}
    protected override void InitializeUIComponents()
    {
        base.InitializeUIComponents();
        viewportMask = contentRect.parent.GetComponent<RectMask2D>();
        if (replayBtn != null)
            replayBtn.onClick.AddListener(OnClickReplay);
        // 绑定继续按钮事件
        continueBtn.GetComponent<Button>().AddClickAction(OnClickContinue);
        delayWait = new WaitForSeconds(animationDelay);
        // oneSecondWait = new WaitForSeconds(1f);
        // if (cachedUpTag == null)
        // {
        //     GameObject upTag = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "ChangeUptag");
        //     if (upTag != null)
        //     {
        //         cachedUpTag = Instantiate(upTag, listContainer);
        //         cachedUpTag.GetComponentInChildren<Text>(true).text = MultilingualManager.Instance.GetString("ImproveRanking");
        //         cachedUpTag.SetActive(false);
        //     }
        //    
        // }
        // if (cachedDownTag == null)
        // {
        //     GameObject downTag = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "Changedowntag");
        //     if (downTag != null)
        //     {
        //         cachedDownTag = Instantiate(downTag, listContainer);
        //         cachedDownTag.GetComponentInChildren<Text>(true).text = MultilingualManager.Instance.GetString("LevelDecline");
        //         cachedDownTag.SetActive(false);
        //     }
        // }
        if (rankItemPrefab == null)
        {
            rankItemPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "ChangeRankItem");
        }
         
        rankItemPool = new ObjectPool(rankItemPrefab, listContainer, 5, PoolBehaviour.GameObject);
        
        _scoreLotusPool = new ObjectPool(flyingLotus.gameObject, ObjectPool.CreatePoolContainer(transform,"scoreLotusPool"), 5, PoolBehaviour.GameObject);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        DOTween.Init();
        
        // 🌟 注册事件
        if (ZenRankManager.Instance != null)
            ZenRankManager.Instance.OnRankTimerTick += UpdateTimerUI;
    }

    private void Start()
    {
        // 初始化基础静态文本
        if (zenTitleText != null) 
            zenTitleText.text = MultilingualManager.Instance.GetString("MeditationList") ?? "禅修榜";
        continueBtn.GetComponentInChildren<Text>(true).text = MultilingualManager.Instance.GetString("Continue");
    }
    
    // ==========================================
    // 🚀 正式调用入口 (带假数据兜底)
    // ==========================================
    public void PlayRankChange(int oldRank, int newRank, int oldScore, int newScore, string levelCode, string levelName, int remainingSeconds, List<ZenRankState> passedPlayers)
    {
        if (passedPlayers == null) passedPlayers = new List<ZenRankState>();
        // 缓存
        cacheOldRank = oldRank; cacheNewRank = newRank;
        cacheOldScore = oldScore; cacheNewScore = newScore;
        cacheLevelCode = levelCode; cacheLevelName = levelName;
        cacheRemainingSeconds = remainingSeconds;
        cachePassedPlayers = new List<ZenRankState>(passedPlayers); // 深拷贝防止外部修改
        
        // 解决后端延迟未分组导致 rank = 0，但本地明明有分数的情况
        if (newRank <= 0 && newScore > 0)
        {
            newRank = 1; // 假定为第1名
            foreach (var p in passedPlayers)
            {
                if (p.Score >= newScore) newRank++; // 谁分数比你高，你就往后退一名
            }
        }
        // ==========================================
        // 防止视觉上出现两个一样的名次！
        // 判断当前拿到的垫背玩家列表里，是否有人占据了我的新名次
        // 如果有，说明是在“前端预测模式”，别人还没给我腾位置，需要把他们往后挤
        // 如果没有（因为自己被过滤了，列表里本来就空出了这个名次），说明这是“服务器最新数据”，大家已经站好位了，无需再挤
        // ==========================================
        bool hasCollision = passedPlayers.Exists(p => p.Rank == newRank);
        if (hasCollision)
        {
            foreach (var p in passedPlayers)
            {
                // 如果我原来没上榜(oldRank <= 0)，或者我原来的名次比他低(数值大)
                // 并且现在我的新名次挤占了他的位置（或超过了他）
                if ((oldRank <= 0 || oldRank >= p.Rank) && p.Rank >= newRank)
                {
                    p.Rank += 1; // 给预测出的我让位
                }
            }
        }
        Debug.LogError($"【结算调试】传给面板的分数 -> 旧分数:{oldScore}, 新分数:{newScore}, 差值:{newScore - oldScore}");
        // passedPlayers.Sort((a, b) => b.Score.CompareTo(a.Score));
        passedPlayers.Sort((a, b) => {
            int scoreComp = b.Score.CompareTo(a.Score);
            if (scoreComp == 0) return a.Rank.CompareTo(b.Rank);
            return scoreComp;
        });
        StartCoroutine(RankChangeRoutine(oldRank, newRank, oldScore, newScore, levelCode, levelName, remainingSeconds, passedPlayers));
    }
   
    // ==========================================
    // 🛠️ 动态对象池生成器
    // ==========================================
    private void PrepareRankItems(int requiredOthersCount)
    {
        // 1. 确保玩家自己的条目已生成
        if (myRankItem == null)
        {
            myRankItem = rankItemPool.GetObject<ZenRankItem>();
        }
        myRankItem.gameObject.SetActive(true);

        // 2. 确保垫背玩家条目已生成足够的数量
        while (otherPlayersPool.Count < requiredOthersCount)
        {
            otherPlayersPool.Add(rankItemPool.GetObject<ZenRankItem>());
        }

        // 3. 隐藏多余的垫背条目
        for (int i = 0; i < otherPlayersPool.Count; i++)
        {
            otherPlayersPool[i].gameObject.SetActive(i < requiredOthersCount);
        }
    }
    
   // ==========================================
    // 🎬 核心动效协程 (完美整合：插槽防重叠 + 摄像机跟随 + 顺序修正)
    // 🎬 核心动效协程 (最终修正：插槽空间补全 + 莲花坐标系修复)
    // ==========================================
    private IEnumerator RankChangeRoutine(int oldRank, int newRank, int oldScore, int newScore, string levelCode, string levelName, int remainingSeconds, List<ZenRankState> surpassedPlayers)
    {
        isAnimating = true;
        mainCanvasGroup.alpha = 0;
        topBanner.anchoredPosition = new Vector2(0, 500f); 
        continueBtn.alpha = 0;
        continueBtn.interactable = false;
        if (myRankItem != null && myRankItem.ScoreText != null)
            myRankItem.ScoreText.transform.localScale = Vector3.one;
        if(glowFrame != null) glowFrame.gameObject.SetActive(false);
        // if (cachedUpTag != null) cachedUpTag.SetActive(false);
        // if (cachedDownTag != null) cachedDownTag.SetActive(false);
        
        string extractName = MultilingualManager.Instance.GetString(levelCode) ?? levelName;
        if (zenNameText != null) zenNameText.text = extractName;
        // ==========================================
        // 提取 levelCode(如"ZenState01") 中的数字，动态加载对应的荷花图片
        // ==========================================
        if (zenImage != null && !string.IsNullOrEmpty(levelCode))
        {
            string zenLevelNum = UIUtilities.ExtractNumber(levelCode);
            // 先尝试 lotus_p 系列命名，如果没有再尝试 zenicon_ 系列 (根据你之前的代码习惯兼容)
            Sprite icon = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("zenlevel_icon" + zenLevelNum);
            if (icon != null) 
            {
                zenImage.sprite = icon;
                zenImage.SetNativeSize(); // 保证图片不变形
            }
        }
        // 1. 状态判定
        bool isUp = (oldRank == 0 && newRank > 0) || (oldRank > newRank);
    
        if (viewportMask != null)
            viewportMask.softness = (newRank == 1) ? new UnityEngine.Vector2Int(0, 0) : new UnityEngine.Vector2Int(0, 110);
        bool isKeep = oldRank == newRank;
        // bool isDown = (oldRank > 0 && oldRank < newRank);
        // 2. 准备 UI 节点
        PrepareRankItems(surpassedPlayers.Count);
        
        ZenRankState myState = new ZenRankState { Rank = newRank, Score = oldScore, Name = GameDataManager.Instance.UserData.UserName, Avatar = GameDataManager.Instance.UserData.UserHeadId };
        myRankItem.SetRankInfo(myState, true);
        RectTransform myRect = myRankItem.GetComponent<RectTransform>();

        // 初始化时先锁定展示旧分数，防止组件自己的 SetRankInfo 提前把新分数刷上去
        if (myRankItem.ScoreText != null) myRankItem.ScoreText.text = oldScore.ToString();
        if (myRankItem.RankText != null) myRankItem.RankText.text = oldRank <= 0 ? "-" : oldRank.ToString();
        myRankItem.UpdateRankVisual(oldRank, true);
        
        // 全部挂载到滚动的 Content 容器下
        myRect.SetParent(contentRect, false);
        for (int i = 0; i < surpassedPlayers.Count; i++) {
            otherPlayersPool[i].transform.SetParent(contentRect, false);
            ZenRankState p = surpassedPlayers[i];
            otherPlayersPool[i].SetRankInfo(p, true);
            // 如果我是上升状态，并且这个人恰好是被我超越的（他的分数被夹在我的新旧分数之间）
            // 只要他现在的名次排在我新名次的后面，且原先排在我前面（或我原本没上榜），他就是被我挤下去的！
            if (isUp && p.Rank > newRank && (oldRank <= 0 || p.Rank <= oldRank))
            {
                // 在动画开始前，他的名次应该假装比现在“高 1 名”（即减1），保持旧榜单的连贯性
                int initialVisualRank = p.Rank - 1;
                if (otherPlayersPool[i].RankText != null) 
                    otherPlayersPool[i].RankText.text = initialVisualRank.ToString();
            
                otherPlayersPool[i].UpdateRankVisual(initialVisualRank, true);
            }
        }

        // 3. 核心数学模型：计算插槽 (Slot)
        int totalOthers = surpassedPlayers.Count;
        int myTargetLogicalIndex = 0;
        for (int i = 0; i < totalOthers; i++) {
            // 如果对方分数比我高，或者【分数相等但对方名次数字比我小】，他依然排在我前面！
            if (surpassedPlayers[i].Score > newScore || 
                (surpassedPlayers[i].Score == newScore && surpassedPlayers[i].Rank < newRank)) 
            {
                myTargetLogicalIndex++;
            }
        }

        int myStartLogicalIndex = 0;
        if (oldRank <= 0) 
        {
            // 🌟 修复 2：首次上榜时，初始位置强制锁定在视口的最底部，防止乱挤
            myStartLogicalIndex = totalOthers;
        } 
        else 
        {
            for (int i = 0; i < totalOthers; i++) {
                // 同样的同分比对逻辑应用在初始排布上
                if (surpassedPlayers[i].Score > oldScore || 
                    (surpassedPlayers[i].Score == oldScore && surpassedPlayers[i].Rank < oldRank)) 
                {
                    myStartLogicalIndex++;
                }
            }
        }
        // if (isDown) myStartLogicalIndex = 0;         
        // if (isDown) tagSlot = myTargetLogicalIndex;   
        // 定义标签专属的“微小间隙”，不再粗暴占用一整格(120像素)
        float tagHeightOffset = 70f; // 这个值控制垫背玩家往下让出的距离，可微调
        // float tagHeight = cachedUpTag != null ? cachedUpTag.GetComponent<RectTransform>().sizeDelta.y : 0f;
        float gap = 20f;   // 标签与条目之间的垂直间隙，可微调
        // 补全给标签让位的空间 (+= 2)
        float GetSlotY(int index, bool isPlayer, bool isStart) 
        {
            int slot;
            // float extraY = 0f;
            if (isStart) 
            {
                // 初始状态排布
                if (isPlayer) {
                    slot = myStartLogicalIndex; // 玩家站在旧分数的正确位置
                } else {
                    // 其他人如果排在旧分数的玩家后面，必须往下挪一格，给玩家腾出初始空位
                    if (index >= myStartLogicalIndex) slot = index + 1;
                    else slot = index;
                }
            }
            else 
            {
                // 下降或不变时，不需要任何换位动画，直接输出最终正确的排布结构
                if (isPlayer) slot = myTargetLogicalIndex;
                else {
                    if (index >= myTargetLogicalIndex)
                    {
                        slot = index + 1;
                        // if (isUp) extraY = tagHeight + gap;
                    }
                    else slot = index;
                }
            }

            return -(slot * itemSpacing);
        }

        // 4. 应用初始位置
        myRect.anchoredPosition = new Vector2(0, GetSlotY(myStartLogicalIndex, true, true));
        for (int i = 0; i < totalOthers; i++) {
            otherPlayersPool[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(0, GetSlotY(i, false, true));
        }
        contentRect.anchoredPosition = Vector2.zero;
        // float centerOffset = itemSpacing * 1.5f; 
        // float initialContentY = Mathf.Abs(myRect.anchoredPosition.y) - centerOffset;
        // //  如果算出负数，强制设为 0，保证刚打开面板时，顶部的人完美贴顶没有空隙
        // if (initialContentY < 0) initialContentY = 0; 
        // contentRect.anchoredPosition = new Vector2(0, initialContentY);
        mainCanvasGroup.DOFade(1f, 0.3f);
        yield return delayWait; 

        // =====================================
        // 5. 动画播放序列
        // =====================================
        currentSequence = DOTween.Sequence();
        currentSequence.SetUpdate(true);
        
        // ----------------------------------------------------
        // [阶段 1]: 莲花飞入与分数上涨
        // ----------------------------------------------------
        if (newScore > oldScore && myRankItem.ScoreText != null)
        {
            int totalAdd = newScore - oldScore;
            int lotusCount = 5; // 固定 5 次
            // 计算每次增加的基础值和余数（最后一次补齐）
            int baseAdd = totalAdd / lotusCount;
            int remainder = totalAdd - baseAdd * lotusCount;
            int currentScore = oldScore;
            float popDuration = 0.08f;
            float flightDuration = 0.3f;   // 飞行与滚动同步时长（加快）
            float lotusInterval = 0.15f;
            
            // ========== 1. 从对象池取出 5 朵莲花，并水平排列 ==========
            List<RectTransform> lotusList = new List<RectTransform>();
            for (int i = 0; i < lotusCount; i++)
            {
                RectTransform lotus = _scoreLotusPool.GetObject<RectTransform>();
                lotus.SetParent(contentRect, false);
                lotusList.Add(lotus);
            }
            float phase1Start = currentSequence.Duration();
            
            // 目标散落点 (以分数为中心的相对偏移)
            Vector2[] fannedFinalTargetsRel = new Vector2[] {
                new Vector2(-15f, 30f),
                new Vector2(0f, 20f),
                new Vector2(15f, 0f), 
                new Vector2(0f, -20f),
                new Vector2(-15f, -30f)
            };
            RectTransform scoreLotusLocalPos = myRankItem.ScoreText.transform.parent.parent.GetChild(0).GetComponent<RectTransform>();
            // 如果分数文本的父物体不是 contentRect，需要转换一次，但一般情况下它是 myRect 的子物体，所以需要转换：
            Vector2 scoreLotusLocalInContent = contentRect.InverseTransformPoint(scoreLotusLocalPos.position);
            
            for (int i = 0; i < lotusCount; i++)
            {
                // 本次增加量（最后一次加上余数）
                int addThisStep = baseAdd + (i == lotusCount - 1 ? remainder : 0);
                int nextScore = currentScore + addThisStep;
                int stepStart = currentScore;
                int stepEnd = nextScore;
                
                RectTransform lotusI = lotusList[i];
                float itemStart = phase1Start + (i * lotusInterval); // 计算每朵莲花出现的绝对时间
                // ================= 核心修复：纯局部坐标系 (Local Space) 计算 =================
                Vector2 itemBasePos = myRect.anchoredPosition;
            
                // 1. 起点：玩家条目偏左的位置，并在 Y 轴上散开 (制造排队飞出的感觉)
                Vector2 originLocal = itemBasePos + new Vector2(lotusOffsetX + 50f, (i - 2) * 3f - 20f);
                originLocal = originLocal + fannedFinalTargetsRel[i];
                // 2. 终点：玩家条目右侧 (分数的区域) 加上散落偏移
                Vector2 targetCenterLocal = scoreLotusLocalInContent;
                Vector2 targetLocal = targetCenterLocal + new Vector2(Random.Range(-2f, 2f),Random.Range(-2f, 2f));
            
                // 3. 控制点：在上空画一个抛物线弧度
                Vector2 controlLocal = itemBasePos + new Vector2(lotusOffsetX +60f, 20f + Mathf.Abs(i - 2) * 5f);
            
                // 对于 CatmullRom 曲线，仅需要传入起点之后的路径点
                Vector3[] pathPoints = new Vector3[] { controlLocal, targetLocal };
                
                // =====================================================================
                currentSequence.InsertCallback(itemStart,() =>
                {
                    lotusI.anchorMin = myRect.anchorMin;
                    lotusI.anchorMax = myRect.anchorMax;
                    lotusI.pivot = myRect.pivot;
                    lotusI.localScale = Vector3.zero; // 关键修复：加入滑动容器
                    lotusI.anchoredPosition = originLocal; // Set starting absolute position
                    // lotusI.anchoredPosition = new Vector2(myRect.anchoredPosition.x + lotusOffsetX + (i * 8f), myRect.anchoredPosition.y);
                    lotusI.gameObject.SetActive(true);
                    
                    // 打印调试信息
                    Debug.Log($"[Lotus {i}] originLocal={originLocal}  |  targetLocal={targetLocal}  |scoreLotusLocalInContent={scoreLotusLocalInContent}|  myRect.anchoredPosition={myRect.anchoredPosition}  |  lotusI.anchoredPosition={lotusI.anchoredPosition}  |  lotusI.worldPos={lotusI.position}  |  myRect.worldPos={myRect.position}");
              
                });
                
                currentSequence.Insert(itemStart,lotusI.DOScale(Vector3.one, popDuration).SetEase(Ease.OutBack));
                float flyStart = itemStart + popDuration;
                Tween flyAnim = lotusI.DOLocalPath(pathPoints, flightDuration,PathType.CatmullRom).SetEase(Ease.OutQuad);
                currentSequence.Insert(flyStart, flyAnim);
                // currentSequence.Insert(flyStart, lotusI.DORotate(new Vector3(0f, 0f, (i - 2) * 25f), flightDuration));
                
                // 使用 Join 让数字滚动与飞行同时发生、同时结束
                currentSequence.Insert(flyStart,DOVirtual.Float(stepStart, stepEnd, flightDuration, (v) =>
                {
                    if (myRankItem.ScoreText != null) myRankItem.ScoreText.text = Mathf.RoundToInt(v).ToString();
                }).SetEase(Ease.OutQuad)); // 缓动和飞行保持一致

                float hitTime = flyStart + flightDuration;
                // 莲花碰到分数，分数跳动
                currentSequence.InsertCallback(hitTime,() =>
                {
                    lotusI.gameObject.SetActive(false);
                    myRankItem.ScoreText.transform.DOKill();
                    myRankItem.ScoreText.transform.localScale = Vector3.one;
                    myRankItem.ScoreText.text = stepEnd.ToString(); // 兜底准确值
                    myRankItem.ScoreText.transform.DOPunchScale(new Vector3(0.5f, 0.5f, 0), 0.2f, 10, 1f);
                    _scoreLotusPool.ReturnObjectToPool(lotusI.GetComponent<PoolObject>());
                });
                currentScore = nextScore;
            }
            float phase1End = phase1Start + (lotusCount * lotusInterval) + popDuration + flightDuration;
            currentSequence.InsertCallback(phase1End, () => {});
            currentSequence.AppendInterval(0.2f); // 数字滚完稍微停顿下
            lotusList = null;
        }
        else
        {
            currentSequence.Append(DOVirtual.Float(oldScore, newScore, 0.8f, (v) => {
                if (myRankItem.ScoreText != null) myRankItem.ScoreText.text = Mathf.RoundToInt(v).ToString();
            }).SetEase(Ease.OutCubic));
        }
        Debug.LogWarning("是否排名不变" + isKeep);
        // ----------------------------------------------------
        // [阶段 2 & 3]: 自身变大飞起 -> 滚动覆盖
        // ----------------------------------------------------
        float targetMyY = 0f;
        if (totalOthers > 0 && isUp)
        {
            // [阶段 2]: 提层级、放大
            currentSequence.AppendCallback(() => {
                myRect.SetAsLastSibling(); // 提升到最顶层，绝对覆盖其他人
            });
            // 先放大，产生飞起来超越的感觉
            currentSequence.Append(myRect.DOScale(1.15f, 0.35f).SetEase(Ease.OutBack)); 
            currentSequence.AppendInterval(0.1f);
            
            // [阶段 3]: 获取此时时间轴点，开始同步滚动换位
            // currentSequence.AppendInterval(0.2f);
            float moveStartTime = currentSequence.Duration(); 
            float moveDuration = 1.3f; // 干脆的超越速度
            
            if (isUp)
            {
                if (oldRank > 0)
                {
                    // 🌟 只有【本来就有名次】的老玩家，才播放数字滚动突破的动画
                    currentSequence.Insert(moveStartTime, DOVirtual.Float(oldRank, newRank, moveDuration, (v) => {
                        int currentTempRank = Mathf.RoundToInt(v);
                        if (myRankItem.RankText != null) myRankItem.RankText.text = currentTempRank.ToString();
                        myRankItem.UpdateRankVisual(currentTempRank, true);
                    }).SetEase(Ease.Linear)); 
                }
                // 兜底：如果之前没上榜(oldRank<=0)，给个虚拟的起始大名次让它往下滚
                // int startVisualRank = oldRank > 0 ? oldRank : newRank + surpassedPlayers.Count; 
                //
                // currentSequence.Insert(moveStartTime, DOVirtual.Float(startVisualRank, newRank, moveDuration, (v) => {
                //     int currentTempRank = Mathf.RoundToInt(v);
                //     if (myRankItem.RankText != null) myRankItem.RankText.text = currentTempRank.ToString();
                //     myRankItem.UpdateRankVisual(currentTempRank, true);
                // }).SetEase(Ease.Linear)); // 线性变化，仿佛一层层突破
            }
            
            // 别人让位
            for (int i = 0; i < totalOthers; i++) {
                float targetOtherY = GetSlotY(i, false, false);
                currentSequence.Insert(moveStartTime, otherPlayersPool[i].GetComponent<RectTransform>().DOAnchorPosY(targetOtherY, moveDuration).SetEase(Ease.InOutCubic));
            }
      
            // 自己往前飞：延迟 0.15s 再开始，时长为 1.8s，并伴随放大效果
            float myDelay = 0.15f;
            targetMyY = GetSlotY(myTargetLogicalIndex, true, false);
            currentSequence.Insert(moveStartTime+ myDelay, myRect.DOAnchorPosY(targetMyY, moveDuration).SetEase(Ease.OutCubic));
            
            // --- 核心改动：Content 的跟随滚动与第一名防挡判定 ---
            // float targetContentY = Mathf.Abs(targetMyY) - itemSpacing * 1.5f; 
            // if (targetContentY < 0) targetContentY = 10; // 不再为第一名或最后一名强制留出顶部/底部异常的空位
            // if (newRank == 1) 
            // {
            //     // 第一名：允许 Content Y 出现负数(往下扯)，在顶部留出约一个身位的距离，避免被透明虚化挡住
            //     targetContentY = Mathf.Abs(targetMyY) - itemSpacing * 0.4f; 
            // } 
            // else 
            // {
            //     // 其他名次：保证上下边界不留出奇怪的空白 (限制不小于0)
            //     if (targetContentY < 0) targetContentY = 0; 
            // }
            // currentSequence.Insert(moveStartTime, contentRect.DOAnchorPosY(targetContentY, moveDuration).SetEase(Ease.InOutQuad));
            
            // 放大覆盖动画：在移动开始前提升层级，移动期间放大，落位后缩回
            currentSequence.InsertCallback(moveStartTime + moveDuration - 0.05f, () =>
            {
                myRankItem.RankText.text = newRank.ToString();
                myRankItem.UpdateRankVisual(newRank, true);
            });
            // 核心修改：等待 0.2 秒后落下
            // [阶段 4]: 落位并缩回原始尺寸
            currentSequence.Insert(moveStartTime + moveDuration + 0.2f, myRect.DOScale(1f, 0.3f).SetEase(Ease.InBack));
            currentSequence.InsertCallback(moveStartTime + moveDuration + 0.4f, () =>
            {
                for (int i = 0; i < totalOthers; i++) {
                    ZenRankState p = surpassedPlayers[i];
                    if (isUp && p.Rank > newRank && (oldRank <= 0 || p.Rank <= oldRank)) 
                    {
                        // 恢复他们真实的（被挤下后的）名次
                        if (otherPlayersPool[i].RankText != null) 
                            otherPlayersPool[i].RankText.text = p.Rank.ToString();
                
                        otherPlayersPool[i].UpdateRankVisual(p.Rank, true);
                
                        // 加上重重落地的震颤感
                        if (otherPlayersPool[i].RankText != null) {
                            otherPlayersPool[i].RankText.transform.DOPunchScale(new Vector3(0.4f, 0.4f, 0), 0.3f, 10, 1f);
                        }
                    }
                }
            });
        }
        else if (isUp && totalOthers == 0) 
        {
            // 核心修改：排名不变（或下降），在分数加完后，也抬起来停顿一下，然后原地落下播放发光粒子框
            float moveStartTime = currentSequence.Duration();
            float origY = myRect.anchoredPosition.y; // 记录原始位置
            targetMyY = origY;
            currentSequence.InsertCallback(moveStartTime, () => {
                myRect.SetAsLastSibling();
            });
            // 抬起：Y轴向上偏移 25 像素，同时放大
            currentSequence.Insert(moveStartTime, myRect.DOAnchorPosY(origY + 12f, 0.35f).SetEase(Ease.OutCubic));
            currentSequence.Insert(moveStartTime, myRect.DOScale(1.12f, 0.35f).SetEase(Ease.OutBack));
   
            // 原地等待 0.2s 之后落下
            float dropTime = moveStartTime + 0.35f + 0.2f;
            currentSequence.InsertCallback(dropTime - 0.05f, () => {
                if (myRankItem.RankText != null) myRankItem.RankText.text = newRank <= 0 ? "-" : newRank.ToString();
                myRankItem.UpdateRankVisual(newRank, true);
            });
            // 落下
            currentSequence.Insert(dropTime, myRect.DOAnchorPosY(origY, 0.3f).SetEase(Ease.InCubic));
            currentSequence.Insert(dropTime, myRect.DOScale(1f, 0.3f).SetEase(Ease.InBack));
           
            // 落下后播放发光框特效
            currentSequence.InsertCallback(dropTime + 0.3f, () => {
                if (glowFrame != null) {
                    glowFrame.transform.SetParent(myRect, false);
                    // glowFrame.anchoredPosition = new Vector2(0, origY);
                    glowFrame.anchoredPosition = Vector2.zero;
                    glowFrame.gameObject.SetActive(true);
                    // 严禁对粒子节点使用 DOScale，保持 1:1 原始大小
                    glowFrame.localScale = Vector3.one;
                    // glowFrame.localScale = new Vector3(1.05f, 1.05f, 1f);
                    // glowFrame.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBounce);
                }
            });
            
            // 填充序列长度
            currentSequence.InsertCallback(dropTime + 0.3f + 0.4f, () => {});
        }
        else if (isKeep)
        {
            // 核心修改：排名不变（或下降），在分数加完后，也抬起来停顿一下，然后原地落下播放发光粒子框
            float moveStartTime = currentSequence.Duration();
            float origY = myRect.anchoredPosition.y; // 记录原始位置
            targetMyY = origY;
            currentSequence.InsertCallback(moveStartTime, () => {
                myRect.SetAsLastSibling();
            });
            // 抬起：Y轴向上偏移 25 像素，同时放大
            currentSequence.Insert(moveStartTime, myRect.DOAnchorPosY(origY + 12f, 0.35f).SetEase(Ease.OutCubic));
            currentSequence.Insert(moveStartTime, myRect.DOScale(1.12f, 0.35f).SetEase(Ease.OutBack));
   
            // 原地等待 0.2s 之后落下
            float dropTime = moveStartTime + 0.35f + 0.2f;
            currentSequence.InsertCallback(dropTime - 0.05f, () => {
                if (myRankItem.RankText != null) myRankItem.RankText.text = newRank <= 0 ? "-" : newRank.ToString();
                myRankItem.UpdateRankVisual(newRank, true);
            });
            // 落下
            currentSequence.Insert(dropTime, myRect.DOAnchorPosY(origY, 0.3f).SetEase(Ease.InCubic));
            currentSequence.Insert(dropTime, myRect.DOScale(1f, 0.3f).SetEase(Ease.InBack));
           
            // 落下后播放发光框特效
            currentSequence.InsertCallback(dropTime + 0.3f, () => {
                if (glowFrame != null) {
                    glowFrame.transform.SetParent(myRect, false);
                    // glowFrame.anchoredPosition = new Vector2(0, origY);
                    glowFrame.anchoredPosition = Vector2.zero;
                    glowFrame.gameObject.SetActive(true);
                    // glowFrame.localScale = new Vector3(1.05f, 1.05f, 1f);
                    // glowFrame.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBounce);
                    glowFrame.localScale = Vector3.one;
                }
            });
            
            // 填充序列长度
            currentSequence.InsertCallback(dropTime + 0.3f + 0.4f, () => {});
        }
        else
        {
            // 🌟 排名下降或不变：不播放移动，直接更新自身的 UI 名次即可
            currentSequence.AppendCallback(() => {
                if (myRankItem.RankText != null) myRankItem.RankText.text = newRank <= 0 ? "-" : newRank.ToString();
                myRankItem.UpdateRankVisual(newRank, true);
            });
        }

        // 6. 爆出标签和特效
        currentSequence.AppendCallback(() => 
        {
            if (isUp)
            {
                if (glowFrame != null){
                    glowFrame.transform.SetParent(myRect, false);
                    glowFrame.anchoredPosition = Vector2.zero;
                    glowFrame.gameObject.SetActive(true);
                    glowFrame.localScale = Vector3.one;
                    // glowFrame.localScale = new Vector3(1.15f, 1.15f, 1f);
                    // glowFrame.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBounce);
                }
                // if (cachedUpTag != null)
                // {
                //     cachedUpTag.transform.SetParent(contentRect, false);
                //     myRect.SetAsLastSibling();
                //     cachedUpTag.transform.SetAsLastSibling();
                //
                //     RectTransform tagRect = cachedUpTag.GetComponent<RectTransform>();
                //     tagRect.anchorMin = new Vector2(0.5f, 1f);
                //     tagRect.anchorMax = new Vector2(0.5f, 1f);
                //     tagRect.pivot = new Vector2(0.5f, 0.5f);
                //     float playerY = -(myTargetLogicalIndex * itemSpacing);
                //     float baseTagY = playerY - itemSpacing - (tagHeightOffset / 2f);
                //     float visualOffsetUp = 15f;
                //     // float tagY = -(myTargetLogicalIndex * itemSpacing) - itemSpacing - (tagHeightOffset / 2f);
                //     float tagY = baseTagY - visualOffsetUp;
                //     float playerTopEdgeY = targetMyY - itemSpacing / 2f;
                //     // float tagY = playerTopEdgeY - tagHeight / 2f - gap;
                //     tagRect.anchoredPosition = new Vector2(0, tagY);
                //     cachedUpTag.SetActive(true);
                //     cachedUpTag.transform.DOPunchScale(Vector3.one * 0.11f, 0.4f);
                // }
            }
            // else if (isDown)
            // {
            //     if (cachedDownTag != null)
            //     {
            //         cachedDownTag.transform.SetParent(contentRect, false);
            //         RectTransform tagRect = cachedDownTag.GetComponent<RectTransform>();
            //         tagRect.anchorMin = new Vector2(0.5f, 1f);
            //         tagRect.anchorMax = new Vector2(0.5f, 1f);
            //         tagRect.pivot = new Vector2(0.5f, 1f);
            //         tagRect.anchoredPosition = new Vector2(0, -(tagSlot * itemSpacing));
            //         cachedDownTag.SetActive(true);
            //         cachedDownTag.transform.DOPunchRotation(new Vector3(0, 0, 10f), 0.4f);
            //     }
            // }
        });
        
        // 只有上升段位才展示挂牌
        if (isUp)
        {
            if (oldRank <= 0)
            {
                string firstRankDesc = MultilingualManager.Instance.GetString("FirstOnBoard");
                if(firstRankDesc == "FirstOnBoard") firstRankDesc = "新晋榜单！";
                topBannerText.text = firstRankDesc;
            }
            else
            {
                string desc = MultilingualManager.Instance.GetString("WellDone");
                int rankDiff =  oldRank - newRank;
                topBannerText.text = string.Format(desc, rankDiff); // 展示提升了几个名次 
            }
            currentSequence.Append(topBanner.DOAnchorPosY(0f, 0.4f).SetEase(Ease.OutBack));
        }
        yield return currentSequence.WaitForCompletion();

        yield return new WaitForSeconds(0.2f);
        continueBtn.DOFade(1f, 0.3f);
        continueBtn.interactable = true;
        continueBtn.transform.DOScale(1.05f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

        isAnimating = false;
    }
    
    // ==========================================
    // 👆 交互控制
    // ==========================================
    public void OnClickContinue()
    {
        if (isAnimating) return; // 动画未播完严禁关闭
        
        // 停止按钮动画
        continueBtn.transform.DOKill();
        
        // 关闭当前变动页面，去往真正的发奖结算界面
        SystemManager.Instance.HidePanel(PanelType.ZenRankChangePanel);
    }

    protected override void OnDisable()
    {
        if (currentSequence != null && currentSequence.IsActive()) currentSequence.Kill();
        // 安全清理正在运行的 Tween
        mainCanvasGroup.DOKill();
        if (myRankItem != null)
        {
            myRankItem.GetComponent<RectTransform>().DOKill();
            if (myRankItem.ScoreText != null) myRankItem.ScoreText.transform.DOKill();
        }
        foreach (var p in otherPlayersPool) { p.GetComponent<RectTransform>()?.DOKill(); }
        
        // 🌟 注销事件
        if (ZenRankManager.Instance != null)
            ZenRankManager.Instance.OnRankTimerTick -= UpdateTimerUI;
        
        topBanner.DOKill();
        glowFrame.DOKill();
        // if (cachedUpTag != null) cachedUpTag.transform.DOKill();
        // if (cachedDownTag != null) cachedDownTag.transform.DOKill();
        continueBtn.transform.DOKill(); 
    
        base.OnDisable();
    }
    // 🌟 更新 UI
    private void UpdateTimerUI(int seconds, string timeStr)
    {
        if (zenTimeText != null) zenTimeText.text = timeStr;
    }
    
    #if UNITY_EDITOR
    // ==========================================
    // 🧪 开发者测试区 (右键点击脚本组件运行)
    // ==========================================

    [ContextMenu("▶ 测试: 排名上升 (5名 -> 1名)")]
    public void TestRankUp()
    {
        // 伪造被你超越的玩家数据 (分数要介于你的旧分数和新分数之间)
        var mockPlayers = new List<ZenRankState>
        {
            new ZenRankState { Rank = 2, Score = 450, Name = "无名扫地僧", Avatar = 1 },
            new ZenRankState { Rank = 3, Score = 400, Name = "太极传人", Avatar = 2 },
            new ZenRankState { Rank = 4, Score = 350, Name = "少林武僧", Avatar = 3 },
            new ZenRankState { Rank = 5, Score = 300, Name = "青城掌门", Avatar = 4 }
        };
        // oldRank: 5, newRank: 1, oldScore: 200, newScore: 500
        PlayRankChange(5, 1, 200, 500, "ZenState01", "枯淡界", 3600, mockPlayers);
    }

    [ContextMenu("▶ 测试: 排名未变 (保持第3名, 但分数增加)")]
    public void TestRankKeep()
    {
        // 伪造你前后的玩家数据 (分数比你高的在前面，比你低的在后面)
        var mockPlayers = new List<ZenRankState>
        {
            new ZenRankState { Rank = 1, Score = 1000, Name = "榜一大哥", Avatar = 5 },
            new ZenRankState { Rank = 2, Score = 800, Name = "榜二大姐", Avatar = 6 },
            new ZenRankState { Rank = 4, Score = 400, Name = "萌新修士", Avatar = 7 }
        };
        // oldRank: 3, newRank: 3, oldScore: 500, newScore: 550
        PlayRankChange(3, 3, 500, 550, "ZenState01", "枯淡界", 3600, mockPlayers);
    }

    [ContextMenu("▶ 测试: 排名下降 (2名 -> 6名)")]
    public void TestRankDown()
    {
        // 伪造超越你的玩家数据 (他们的分数现在比你高了)
        var mockPlayers = new List<ZenRankState>
        {
            new ZenRankState { Rank = 2, Score = 600, Name = "卷王A", Avatar = 8 },
            new ZenRankState { Rank = 3, Score = 580, Name = "卷王B", Avatar = 9 },
            new ZenRankState { Rank = 4, Score = 550, Name = "卷王C", Avatar = 10 },
            new ZenRankState { Rank = 5, Score = 520, Name = "卷王D", Avatar = 11 }
        };
        // oldRank: 2, newRank: 6, oldScore: 500, newScore: 500 (分数没变，但别人涨了)
        PlayRankChange(2, 6, 500, 500, "ZenState01", "枯淡界", 3600, mockPlayers);
    }
    
    [ContextMenu("▶ 测试: 首次进榜 (未上榜 -> 99名)")]
    public void TestFirstTimeRank()
    {
        // 兜底测试：oldRank = 0 的情况
        var mockPlayers = new List<ZenRankState>
        {
            new ZenRankState { Rank = 98, Score = 120, Name = "守门员", Avatar = 12 }
        };
        PlayRankChange(0, 99, 0, 100, "ZenState01", "枯淡界", 3600, mockPlayers);
    }
#endif
}