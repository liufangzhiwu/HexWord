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
    [SerializeField] private RectTransform flyingLotus;   
    [Tooltip("莲花出现在玩家条目右侧的偏移量 (可根据 UI 宽度微调)")]
    [SerializeField] private float lotusOffsetX = 350f;
    
    [Header("动画与测试")]
    [SerializeField] private float itemSpacing = 120f;      // 列表中每个条目的间距
    [SerializeField] private float animationDelay = 0.5f;   // 界面打开后多久开始播动画
    
    // ==========================================
    // 🛡️ 大厂级优化：性能缓存区
    // ==========================================
    private GameObject rankItemPrefab;          // 动态加载的预制体
    private ObjectPool rankItemPool;
    private ZenRankItem myRankItem;             // 动态生成的玩家自己
    private List<ZenRankItem> otherPlayersPool = new List<ZenRankItem>(); // 动态生成的垫背玩家池
    private GameObject cachedUpTag;
    private GameObject cachedDownTag;
    
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
    if (flyingLotus != null) flyingLotus.DOKill();
    if (cachedUpTag != null) cachedUpTag.transform.DOKill();
    if (cachedDownTag != null) cachedDownTag.transform.DOKill();
    continueBtn.transform.DOKill();
}

private void ResetUIForReplay()
{
    // 隐藏特效
    if (glowFrame != null) glowFrame.gameObject.SetActive(false);
    if (flyingLotus != null) flyingLotus.gameObject.SetActive(false);
    if (cachedUpTag != null) cachedUpTag.SetActive(false);
    if (cachedDownTag != null) cachedDownTag.SetActive(false);
    
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
        if (replayBtn != null)
            replayBtn.onClick.AddListener(OnClickReplay);
        // 绑定继续按钮事件
        continueBtn.GetComponent<Button>().AddClickAction(OnClickContinue);
        delayWait = new WaitForSeconds(animationDelay);
        // oneSecondWait = new WaitForSeconds(1f);
        if (cachedUpTag == null)
        {
            GameObject upTag = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "ZenUptag");
            if (upTag != null)
            {
                cachedUpTag = Instantiate(upTag, listContainer);
                cachedUpTag.GetComponentInChildren<Text>(true).text = MultilingualManager.Instance.GetString("ImproveRanking");
                cachedUpTag.SetActive(false);
            }
           
        }
        if (cachedDownTag == null)
        {
            GameObject downTag = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "ZenDowntag");
            if (downTag != null)
            {
                cachedDownTag = Instantiate(downTag, listContainer);
                cachedDownTag.GetComponentInChildren<Text>(true).text = MultilingualManager.Instance.GetString("LevelDecline");
                cachedDownTag.SetActive(false);
            }
        }
        if (rankItemPrefab == null)
        {
            rankItemPrefab = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "ZenRankItem");
        }
         
        rankItemPool = new ObjectPool(rankItemPrefab, listContainer, 5, PoolBehaviour.GameObject);
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
        
        
        // 🌟 核心修复 1：智能预测名次 
        // 解决后端延迟未分组导致 rank = 0，但本地明明有分数的情况
        if (newRank <= 0 && newScore > 0)
        {
            newRank = 1; // 假定为第1名
            foreach (var p in passedPlayers)
            {
                if (p.Score >= newScore) newRank++; // 谁分数比你高，你就往后退一名
            }
        }
        passedPlayers.Sort((a, b) => b.Score.CompareTo(a.Score));
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
        
        if(glowFrame != null) glowFrame.gameObject.SetActive(false);
        if (flyingLotus != null) { flyingLotus.gameObject.SetActive(false); flyingLotus.localScale = Vector3.zero; }
        if (cachedUpTag != null) cachedUpTag.SetActive(false);
        if (cachedDownTag != null) cachedDownTag.SetActive(false);
        
 
        string extractName = MultilingualManager.Instance.GetString(levelCode) ?? levelName;
        if (zenNameText != null) zenNameText.text = extractName;
        // ==========================================
        // 提取 levelCode(如"ZenState01") 中的数字，动态加载对应的荷花图片
        // ==========================================
        if (zenImage != null && !string.IsNullOrEmpty(levelCode))
        {
            string zenLevelNum = UIUtilities.ExtractNumber(levelCode);
            // 先尝试 lotus_p 系列命名，如果没有再尝试 zenicon_ 系列 (根据你之前的代码习惯兼容)
            Sprite icon = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("zenlevel_icon" + zenLevelNum);
            if (icon != null) 
            {
                zenImage.sprite = icon;
                zenImage.SetNativeSize(); // 保证图片不变形
            }
        }
        // 1. 状态判定
        bool isUp = (oldRank == 0 && newRank > 0) || (oldRank > newRank);
        bool isDown = (oldRank > 0 && oldRank < newRank);

        // 2. 准备 UI 节点
        PrepareRankItems(surpassedPlayers.Count);
        ZenRankState myState = new ZenRankState { Rank = newRank, Score = oldScore, Name = GameDataManager.Instance.UserData.UserName, Avatar = GameDataManager.Instance.UserData.UserHeadId };
        myRankItem.SetRankInfo(myState, true);
        RectTransform myRect = myRankItem.GetComponent<RectTransform>();

        // 全部挂载到滚动的 Content 容器下
        myRect.SetParent(contentRect, false);
        for (int i = 0; i < surpassedPlayers.Count; i++) {
            otherPlayersPool[i].transform.SetParent(contentRect, false);
            otherPlayersPool[i].SetRankInfo(surpassedPlayers[i], true);
        }

        // 3. 核心数学模型：计算插槽 (Slot)
        int totalOthers = surpassedPlayers.Count;
        
        int myTargetLogicalIndex = 0;
        for (int i = 0; i < totalOthers; i++) {
            if (surpassedPlayers[i].Score > newScore) myTargetLogicalIndex++;
        }

        int myStartLogicalIndex = myTargetLogicalIndex;
        if (isUp) myStartLogicalIndex = totalOthers; 
        if (isDown) myStartLogicalIndex = 0;         

        int tagSlot = -1;
        if (isUp) tagSlot = myTargetLogicalIndex + 1; 
        if (isDown) tagSlot = myTargetLogicalIndex;   

        // 🌟 修复 1：补全给标签让位的空间 (+= 2)
        float GetSlotY(int index, bool isPlayer, bool isStart) 
        {
            int slot = index;
            if (!isStart) 
            {
                if (isUp) {
                    // 上升：比你差的人，必须往下挪 2 格（1格给你，1格给标签）
                    if (!isPlayer && index >= myTargetLogicalIndex) slot += 2; 
                } else if (isDown) {
                    // 下降：你被压在标签下面，别人也要给标签让位
                    if (isPlayer) slot++; 
                    else if (index >= myTargetLogicalIndex) slot += 2; 
                } else {
                    if (!isPlayer && index >= myTargetLogicalIndex) slot++; 
                }
            } 
            else 
            {
                 if (isUp) {
                     if (isPlayer) slot = totalOthers;
                     else slot = index;
                 } else if (isDown) {
                     if (isPlayer) slot = 0;
                     else slot = index + 1;
                 } else {
                     if (!isPlayer && index >= myTargetLogicalIndex) slot++;
                 }
            }
            return -(slot * itemSpacing); 
        }

        // 4. 应用初始位置
        myRect.anchoredPosition = new Vector2(0, GetSlotY(myStartLogicalIndex, true, true));
        for (int i = 0; i < totalOthers; i++) {
            otherPlayersPool[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(0, GetSlotY(i, false, true));
        }

        float centerOffset = itemSpacing * 1.5f; 
        contentRect.anchoredPosition = new Vector2(0, Mathf.Abs(myRect.anchoredPosition.y) - centerOffset);

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
        if (newScore >= oldScore && flyingLotus != null && myRankItem.ScoreText != null)
        {
            currentSequence.AppendCallback(() => {
                flyingLotus.SetParent(contentRect, false);
                flyingLotus.localScale = Vector3.zero; // 关键修复：加入滑动容器
                flyingLotus.anchoredPosition = myRect.anchoredPosition + new Vector2(lotusOffsetX, 0f);
                flyingLotus.gameObject.SetActive(true);
            });
            currentSequence.Append(flyingLotus.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack));
            currentSequence.AppendInterval(0.2f);
            currentSequence.Append(flyingLotus.DOMove(myRankItem.ScoreText.transform.position, 0.5f).SetEase(Ease.InBack));
            // 莲花碰到分数，分数跳动
            currentSequence.AppendCallback(() => {
                flyingLotus.gameObject.SetActive(false);
                myRankItem.ScoreText.transform.DOPunchScale(new Vector3(0.5f, 0.5f, 0), 0.3f, 10, 1f);
            });
            
            currentSequence.Append(DOVirtual.Float(oldScore, newScore, 0.8f, (v) => {
                if (myRankItem.ScoreText != null) myRankItem.ScoreText.text = Mathf.RoundToInt(v).ToString();
            }).SetEase(Ease.OutCubic));
            
            currentSequence.AppendInterval(0.1f);
        }
        else
        {
            currentSequence.Append(DOVirtual.Float(oldScore, newScore, 0.8f, (v) => {
                if (myRankItem.ScoreText != null) myRankItem.ScoreText.text = Mathf.RoundToInt(v).ToString();
            }).SetEase(Ease.OutCubic));
        }
        // ----------------------------------------------------
        // [阶段 2 & 3]: 自身变大飞起 -> 滚动覆盖
        // ----------------------------------------------------
        if (totalOthers > 0)
        {
            // [阶段 2]: 提层级、放大
            currentSequence.AppendCallback(() => {
                myRect.SetAsLastSibling(); // 提升到最顶层，绝对覆盖其他人
            });
            // 先放大，产生飞起来超越的感觉
            currentSequence.Append(myRect.DOScale(1.25f, 0.35f).SetEase(Ease.OutBack)); 
            currentSequence.AppendInterval(0.1f);
            
            // [阶段 3]: 获取此时时间轴点，开始同步滚动换位
            // currentSequence.AppendInterval(0.2f);
            float moveStartTime = currentSequence.Duration(); 
            float moveDuration = 1.3f; // 干脆的超越速度
            
            // 别人让位
            for (int i = 0; i < totalOthers; i++) {
                float targetOtherY = GetSlotY(i, false, false);
                currentSequence.Insert(moveStartTime, otherPlayersPool[i].GetComponent<RectTransform>().DOAnchorPosY(targetOtherY, moveDuration).SetEase(Ease.InOutCubic));
            }
      
            // 自己往前飞：延迟 0.15s 再开始，时长为 1.8s，并伴随放大效果
            float myDelay = 0.15f;
            float targetMyY = GetSlotY(myTargetLogicalIndex, true, false);
            currentSequence.Insert(moveStartTime+ myDelay, myRect.DOAnchorPosY(targetMyY, moveDuration).SetEase(Ease.OutCubic));
            
            // --- 核心改动：Content 的跟随滚动与第一名防挡判定 ---
            float targetContentY = Mathf.Abs(targetMyY) - itemSpacing * 1.5f; 
            if (newRank == 1) 
            {
                // 第一名：允许 Content Y 出现负数(往下扯)，在顶部留出约一个身位的距离，避免被透明虚化挡住
                targetContentY = Mathf.Abs(targetMyY) - itemSpacing * 0.4f; 
            } 
            else 
            {
                // 其他名次：保证上下边界不留出奇怪的空白 (限制不小于0)
                if (targetContentY < 0) targetContentY = 0; 
            }
            currentSequence.Insert(moveStartTime, contentRect.DOAnchorPosY(targetContentY, moveDuration).SetEase(Ease.InOutQuad));
            
            // 放大覆盖动画：在移动开始前提升层级，移动期间放大，落位后缩回
            currentSequence.InsertCallback(moveStartTime + myDelay - 0.05f, () =>
            {
                myRankItem.UpdateRankVisual(newRank, true);
            });
            
            // [阶段 4]: 落位并缩回原始尺寸
            currentSequence.Insert(moveStartTime + moveDuration, myRect.DOScale(1f, 0.3f).SetEase(Ease.InBack));
        }
        else
        {
            currentSequence.AppendCallback(() => {
                if (myRankItem.RankText != null) myRankItem.RankText.text = newRank <= 0 ? "-" : newRank.ToString();
            });
        }

        // 6. 爆出标签和特效
        currentSequence.AppendCallback(() => 
        {
            if (isUp)
            {
                if (glowFrame != null){
                    glowFrame.transform.SetParent(contentRect, false);
                    glowFrame.anchoredPosition = myRect.anchoredPosition;
                    glowFrame.gameObject.SetActive(true);
                    glowFrame.localScale = new Vector3(1.15f, 1.15f, 1f);
                    glowFrame.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBounce);
                }
                if (cachedUpTag != null)
                {
                    cachedUpTag.transform.SetParent(contentRect, false);
                    RectTransform tagRect = cachedUpTag.GetComponent<RectTransform>();
                    tagRect.anchorMin = new Vector2(0.5f, 1f);
                    tagRect.anchorMax = new Vector2(0.5f, 1f);
                    tagRect.pivot = new Vector2(0.5f, 1f);
                    tagRect.anchoredPosition = new Vector2(0, -(tagSlot * itemSpacing));
                    cachedUpTag.SetActive(true);
                    cachedUpTag.transform.DOPunchScale(Vector3.one * 0.11f, 0.4f);
                }
            }
            else if (isDown)
            {
                if (cachedDownTag != null)
                {
                    cachedDownTag.transform.SetParent(contentRect, false);
                    RectTransform tagRect = cachedDownTag.GetComponent<RectTransform>();
                    tagRect.anchorMin = new Vector2(0.5f, 1f);
                    tagRect.anchorMax = new Vector2(0.5f, 1f);
                    tagRect.pivot = new Vector2(0.5f, 1f);
                    tagRect.anchoredPosition = new Vector2(0, -(tagSlot * itemSpacing));
                    cachedDownTag.SetActive(true);
                    cachedDownTag.transform.DOPunchRotation(new Vector3(0, 0, 10f), 0.4f);
                }
            }
        });
        
        // 只有上升段位才展示挂牌
        if (isUp)
        {
            string desc = MultilingualManager.Instance.GetString("WellDone");
            topBannerText.text = string.Format(desc,  (newRank - oldRank));  // 展示提升了几个名次 
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
        if (flyingLotus != null) flyingLotus.DOKill();
        if (cachedUpTag != null) cachedUpTag.transform.DOKill();
        if (cachedDownTag != null) cachedDownTag.transform.DOKill();
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