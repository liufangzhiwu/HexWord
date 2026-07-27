using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ZenRankStartScreen : UIWindow
{
    [Header("UI 面板 (阶段1)")]
    [SerializeField] private GameObject stage1Panel;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Text stage1TitleText;
    [SerializeField] private Text stage1TimeText;
    [SerializeField] private Text stage1DescText;
    [SerializeField] private Button closeButton;
    
    [Header("UI 面板 (阶段2 - 雷达匹配)")]
    [SerializeField] private GameObject stage2Panel;
    [SerializeField] private Image stage2MyAvatar;
    [SerializeField] private Text stage2TipText;
    [SerializeField] private Text stage2ProgressText;
    [SerializeField] private Slider progressBar;          // 进度条
    
    [Header("匹配规则设置")]
    [SerializeField] private int targetPlayerCount = 30;     // 逻辑上需要匹配的总人数
    [SerializeField] private int maxVisualAvatars = 4;       // 视觉上雷达图最多同时显示的头像数量 (防止拥挤)
    
    [Header("雷达图设置")]
    [SerializeField] private RectTransform radarCenter;   // 雷达中心容器（挂载头像的父节点）
    [SerializeField] private float maxRadarRadius = 420f; // 头像出现的最大半径
    [SerializeField] private float minRadarRadius = 160f;  // 头像出现的最小半径（避免挡住中间的“你”）
    [SerializeField] private RectTransform radarCircle;    // 雷达中心的园从小变大到420
    [SerializeField] private RectTransform radarLine; // 雷达扫描的线
    [SerializeField] private float radarRotateDuration = 2.5f; //  转一圈需要几秒（越小转得越快）
    [SerializeField] private float avatarMinDistance = 120f; // 头像之间的最小安全距离（请根据实际头像 UI 的宽高/直径在 Inspector 中调整）
    
    private Vector2 _originalCircleSize; //用于缓存雷达圆的初始大小
    
    private GameObject avatarPrefab;     // 其他玩家的头像预制体
    private ObjectPool avatarPool;
    // 用于管理已生成的视觉头像列表
    private List<GameObject> activeAvatars = new List<GameObject>();
    private Coroutine matchCoroutine;
    // 一个强制模式的标记
    private bool _isForcedMode = false;
    // 变量类型改为 string
    private string _returnTargetPanel = PanelType.PrimaryInterface;
    protected override void InitializeUIComponents()
    {
        // 绑定按钮点击事件
        startGameButton.AddClickAction(OnStartGameClicked);
        closeButton.AddClickAction(OnCloseClicked);
        avatarPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem","UserAvatar");
        avatarPool = new ObjectPool(avatarPrefab, transform, 5);
        
        _originalCircleSize = radarCircle.sizeDelta;
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        _isForcedMode = false;
        _returnTargetPanel = PanelType.PrimaryInterface;
        closeButton.gameObject.SetActive(true);
        // 初始状态：显示阶段1，隐藏阶段2
        stage1Panel.SetActive(true);
        stage2Panel.SetActive(false);
        InitLanguageAndData();
        ResetRadarUI();
        if (ZenRankManager.Instance != null)
        {
            ZenRankManager.Instance.OnRankTimerTick += UpdateTimerUI;
            string boardId = GameDataManager.Instance.UserData.Zenlevel;
            if (!string.IsNullOrEmpty(boardId))
            {
                StartCoroutine(ZenRankManager.Instance.FetchLeaderboardDataRoutine(boardId));
            }
        }
    }
    
    // 【生命周期安全清理
    protected override void OnDisable()
    {
        // 界面关闭时，必须停止匹配协程，防止后台乱跑
        if (matchCoroutine != null)
        {
            StopCoroutine(matchCoroutine);
            matchCoroutine = null;
        }
        if (ZenRankManager.Instance != null)
        {
            ZenRankManager.Instance.OnRankTimerTick -= UpdateTimerUI;
        }
        radarLine?.DOKill();
        radarCircle?.DOKill();
        // 清理所有动态生成的头像，停止它们身上的 DOTween
        ClearAllAvatars();
        base.OnDisable();
    }
    private void InitLanguageAndData()
    {
        stage2MyAvatar.sprite = LoadheadIcon("head" + GameDataManager.Instance.UserData.UserHeadId);
        
        if (stage1TitleText != null) stage1TitleText.text = MultilingualManager.Instance.GetString("MeditationList");
        if (stage1DescText != null) stage1DescText.text = MultilingualManager.Instance.GetString("ZenMatchRule");
        if (startGameButton != null) startGameButton.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("StartGame");
        if (stage2TipText != null) stage2TipText.text = MultilingualManager.Instance.GetString("Matching");
        if (stage1TimeText != null) stage1TimeText.text = "..."; 
        
    }
    // 专门的重置雷达状态方法
    private void ResetRadarUI()
    {
        if (radarLine != null)
        {
            radarLine.DOKill();
            radarLine.localEulerAngles = Vector3.zero; // 重置旋转角度
        }

        if (radarCircle != null)
        {
            radarCircle.DOKill();
            radarCircle.sizeDelta = _originalCircleSize; // 恢复到原始大小
        }
    }
    /// <summary>
    /// 外部调用：设置关闭此界面时需要返回的面板
    /// </summary>
    public void SetSourcePanel(string sourcePanel)
    {
        _returnTargetPanel = sourcePanel;
    }
    
    //  暴露给外部调用的强制模式开关
    public void SetForcedMode(bool isForced)
    {
        _isForcedMode = isForced;
        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(!isForced); // 强制模式下隐藏关闭按钮
        }
    }
    
    // 🌟 接收全局计时器推送的文本并刷新 UI
    private void UpdateTimerUI(int seconds, string timeStr)
    {
        if (stage1TimeText != null)
        {
            // 如果你需要拼接前缀（比如"剩余时间:"），可以在这里拼接
            if (seconds > 0)
            {
                stage1TimeText.text = timeStr; 
            }
            else
            {
                stage1TimeText.text = ZenRankManager.Instance?.GetNextRemainingTimeFormatted();
            }
        }
    }
    // 点击开始游戏按钮
    private void OnStartGameClicked()
    {
        stage1Panel.SetActive(false);
        stage2Panel.SetActive(true);
        
        if (matchCoroutine != null)
        {
            StopCoroutine(matchCoroutine);
        }
        // 开始模拟匹配协程
        matchCoroutine = StartCoroutine(SimulateMatchmaking());
        StartRadarAnimations();
    }
    // [新增] 启动雷达动画逻辑
    private void StartRadarAnimations()
    {
        // 1. 雷达线逻辑：无限匀速旋转
        radarLine?.DOKill();
        radarLine?.DORotate(new Vector3(0, 0, -360), radarRotateDuration, RotateMode.FastBeyond360)
            .SetLoops(-1, LoopType.Restart)
            .SetEase(Ease.Linear);

        // 2. 雷达圆逻辑：等待 -> 变大 -> 停止变大
        if (radarCircle != null)
        {
            radarCircle.DOKill();
            radarCircle.sizeDelta = _originalCircleSize; // 确保开始时是原始尺寸
            
            // 使用 DOTween 的 Sequence 实现“等待然后变大”的过程
            Sequence circleSeq = DOTween.Sequence();
            circleSeq.AppendInterval(0.4f); // 等待配置的时间
            circleSeq.Append(radarCircle.DOSizeDelta(new Vector2(616,616), radarRotateDuration*2).SetEase(Ease.OutQuad)); // 慢慢变大
            // Sequence 执行完毕后自动停止，圆不再继续变大，而雷达线还在继续转
        }
    }
    private void OnCloseClicked()
    {
        // 关闭当前窗口并返回大厅
        SystemManager.Instance.HidePanel(PanelType.ZenRankStartScreen, true, () =>
        {
            SystemManager.Instance.ShowPanel(_returnTargetPanel);
            SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        });
    }
    // 模拟匹配过程
    private IEnumerator SimulateMatchmaking()
    {
        int currentPlayerCount = 1; // 初始只有玩家自己
        UpdateProgressUI(currentPlayerCount);
        ClearAllAvatars(); // 清理旧头像
        yield return new WaitForSeconds(0.5f);
        StartCoroutine(GenerateAvatars(12));
        WaitForSeconds wait =  new WaitForSeconds(0.08f);
        while (currentPlayerCount < targetPlayerCount)
        {
            // 随机等待 0.2 到 0.8 秒模拟寻找玩家的过程
            // yield return new WaitForSeconds(Random.Range(0.2f, 0.8f));
            yield return wait;
            currentPlayerCount++;
            UpdateProgressUI(currentPlayerCount);
        }
        
        bool isSuccess = false;
        yield return StartCoroutine(ZenRankManager.Instance.RequestJoinZenRankRoutine((res) =>
        {
            isSuccess = res;
        }));
        if (!isSuccess)
        {
            if (stage2TipText != null) 
                stage2TipText.text = "<color=#FF5555>网络异常，匹配失败，请重试！</color>";
            
            // 重置按钮或让玩家手动关闭面板，这里可以根据你的 UI 需求调整
            // 为了安全，不再往下执行强制进游戏的逻辑
            yield break; 
        }
        Debug.Log("服务器确认加入成功，进入游戏！");
        
        // 触发进入战斗场景逻辑
        SystemManager.Instance.HidePanel(PanelType.ZenRankStartScreen, true, () =>
        {
            SystemManager.Instance.HidePanel(PanelType.ZenRankScreen);
            
            if (_isForcedMode)
            {
                SystemManager.Instance.HidePanel(PanelType.ChessFinishView);
                SystemManager.Instance.HidePanel(PanelType.StageFinishView);
            }
            
            UIWindow uiWindow = SystemManager.Instance.GetPanel(PanelType.PrimaryInterface);
            uiWindow?.GetComponent<PrimaryInterface>().OnPlayClick();
        });
    }
    // 独立协程：慢慢生成指定数量的头像（同时最多显示4个）
    private IEnumerator GenerateAvatars(int totalCount)
    {
        int generated = 0;
        WaitForSeconds wait = new WaitForSeconds(Random.Range(0.4f, 0.6f));
        while (generated < totalCount)
        {
            yield return wait;
             // 控制生成节奏
            SpawnRandomAvatar();
            generated++;
        }
    }
    // 更新进度条和文字
    private void UpdateProgressUI(int count)
    {
        if (progressBar != null)
        {
            progressBar.value = (float)count / targetPlayerCount;
        }
        if (stage2ProgressText != null)
        {
            stage2ProgressText.text = $"<color=#FFDC74>{count}</color>/{targetPlayerCount}";
        }
    }

    // 在雷达图上随机位置生成头像
    private void SpawnRandomAvatar()
    {
        if (avatarPrefab == null || radarCenter == null) return;

        // 【防拥挤逻辑】：如果当前显示的头像数量已经达到上限，则移除最旧的一个
        if (activeAvatars.Count >= maxVisualAvatars)
        {
            GameObject oldestAvatar = activeAvatars[0];
            activeAvatars.RemoveAt(0);
            
            // 安全销毁：先杀死它身上可能正在运行的 DOTween 动画
            oldestAvatar.transform.DOKill();
            avatarPool.ReturnObjectToPool(oldestAvatar.GetComponent<PoolObject>());
        }
        Vector2 targetPos = Vector2.zero;
        Vector2 myAvatarPos = stage2MyAvatar.rectTransform.anchoredPosition;

        float randomRadius = 0f;
        bool foundValidPos = false;
        for (int i = 0; i < 30; i++)
        {
            // 1. 随机生成角度和半径
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad; // 转换为弧度
            randomRadius = Random.Range(minRadarRadius, maxRadarRadius);
            
            // 2. 利用三角函数计算出 X 和 Y 的坐标
            float x = Mathf.Cos(randomAngle) * randomRadius;
            float y = Mathf.Sin(randomAngle) * randomRadius;
            targetPos = new Vector2(x, y);
            
            // 避免覆盖自己的头像
            if (Vector2.Distance(targetPos, myAvatarPos) < avatarMinDistance)
                continue;
            // 检查与场上现有头像的距离
            
            bool isOverlapping = false;
            foreach (var activeAvatar in activeAvatars)
            {
                if (activeAvatar == null) continue;
                Vector2 existingPos = activeAvatar.GetComponent<RectTransform>().anchoredPosition;
                
                // 如果两点之间的距离小于下限值（头像直径），说明重叠了
                if (Vector2.Distance(targetPos, existingPos) < avatarMinDistance)
                {
                    isOverlapping = true;
                    break;
                }
            }
            
            // 如果不重叠，跳出循环，使用这个坐标
            if (!isOverlapping)
            {
                foundValidPos = true;
                break;
            }
        }
        if (!foundValidPos)
        {
            Debug.LogWarning("屏幕太挤了，这次找不到合适的位置，放弃生成！");
            return; 
        }
        // 3. 实例化头像并设置位置
        GameObject newAvatar = avatarPool.GetObject(radarCenter);
        RectTransform rectTransform = newAvatar.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = targetPos;
            
        Image avatarImage = newAvatar.transform.GetChild(0).GetComponent<Image>();
        if (avatarImage != null)
        {
            // 随机抽取一个 1 到 maxHeadIconId 之间的头像
            int randomHeadId = Random.Range(0, 24);
            avatarImage.sprite = LoadheadIcon("head" + randomHeadId);
        }
            
        float t = (randomRadius - minRadarRadius) / (maxRadarRadius - minRadarRadius); // 0~1，0=最内，1=最外
        float baseScale = 0.75f;     // 最内圈的基础缩放
        float minScale = 0.35f;      // 最外圈的最小缩放
        float targetScale = Mathf.Lerp(baseScale, minScale, t);
            
        // 4. (可选) 添加一个弹出的动画效果
        // 如果你使用了 DOTween 插件，可以这样做：
        newAvatar.transform.localScale = Vector3.zero;
        newAvatar.transform.DOScale(Vector3.one * targetScale, 0.3f).SetEase(Ease.OutBack);
        
        activeAvatars.Add(newAvatar);
    }
    
    // 清理所有已生成的头像
    private void ClearAllAvatars()
    {
        foreach (var avatar in activeAvatars)
        {
            if (avatar != null)
            {
                avatar.transform.DOKill(); // 必须 Kill，否则直接 Destroy 会导致 DOTween 报错
                Destroy(avatar);
            }
        }
        activeAvatars.Clear();
    }
    
    private Sprite LoadheadIcon(string showIcon)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon);
    }
}