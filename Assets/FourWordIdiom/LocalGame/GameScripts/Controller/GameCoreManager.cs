using Middleware;
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_IOS
using UnityEngine.iOS;
#endif

public enum PanelState
{
    Null,
    MainMenuPanel,
    FinishXiaoPanel,
    FinishPingPanel,
    GameXiaoPanel,
    GamePingPanel,
    FinishHexPanel,
    GameHexPanel
}

/// <summary>
/// 游戏核心管理器（单例模式）
/// 功能：
/// 1. 游戏全局初始化
/// 2. 隐私协议处理
/// 3. 设备信息检测
/// 4. 游戏流程控制
/// </summary>
public sealed class GameCoreManager : MonoBehaviour
{
    #region 单例实现

    public static GameCoreManager Instance;

    #endregion

    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject changjing01_texiao;

    // ======================= 大厅场景特效（按主题 id 动态加载） =======================
    /// <summary>
    /// 主题 id → 大厅场景特效预制体名（放在 Resources/LobbyEffect 下）。
    /// 没有配置的主题不会加载任何特效。
    /// </summary>
    private static readonly Dictionary<int, string> LobbyEffectByThemeId = new Dictionary<int, string>()
    {
        { 0,  "changjing01_texiao" },
        { 1,  "changjing02_texiao" },
        { 3,  "changjing04_texiao" },
        { 9,  "changjing10_texiao" },
        { 13, "changjing14_texiao" },
        { 14, "changjing15_texiao" },
    };

    private const string LobbyEffectResPath = "LobbyEffect/";

    /// <summary>当前大厅特效实例（null = 该主题没有特效）</summary>
    private GameObject _lobbyEffect;
    /// <summary>当前实例对应的主题 id，用于避免重复加载</summary>
    private int _lobbyEffectThemeId = int.MinValue;
    /// <summary>当前主题是否已经处理过（含"判定为无特效"的情况）</summary>
    private bool _lobbyEffectResolved;
    /// <summary>特效挂载点（沿用原先 changjing01_texiao 的父节点，保证层级/排序一致）</summary>
    private Transform _lobbyEffectRoot;

    [Header("Blur Settings")] [SerializeField]
    private Material blurMaterial; // 挂载你第一版写的3x3模糊Shader的材质球

    [Range(1, 8)] [SerializeField] private int downSample = 4; // 降采样倍数（建议4，越大越平滑且计算越快）
    [Range(1, 5)] [SerializeField] private int blurIterations = 3; // 模糊迭代次数（建议3次）

    private Sprite _originalBgSprite;
    private Sprite _blurredBgSprite;
    private bool _isBlurred = false; // 当前模糊状态

    [HideInInspector] public bool IsNetworkActive=true;
    public PanelState PanelState = PanelState.Null;

    public bool IsTrueAuto;
    public GameObject AutoLevelTalbe;


    // 原 ps 变量删除，换成以下：
    private List<ParticleSystem> allPs = new List<ParticleSystem>();
    private List<ParticleSystem.Particle[]> allParticleArrays = new List<ParticleSystem.Particle[]>();
    private List<float> originalRates = new List<float>();

    // 协程管理
    private Coroutine fadeInCoroutine;
    private Coroutine fadeOutCoroutine;
    private Coroutine _bgLoginCoroutine;
    
    // 新增：淡入时是否已经完成（避免重复）
    private bool isFadeInComplete = false;
    
    public UserProfile userProfile;
    public PublicProfileDto otherPersonProfile;
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }

    #region 公共API

    private void Start()
    {
        HTTPClient.Instance.OnTokenExpired += HandleTokenExpired;
        StartCoroutine(InitializeGameRoutine());
        StartCoroutine(CheckNetworkConnection());
        AutoLevelTalbe.GetComponent<Toggle>().onValueChanged.AddListener(OnAutoLevelTalbeValueChanged);

#if Unity_ShowLog || UNITY_EDITOR
        IsTrueAuto = false;
        AutoLevelTalbe.gameObject.SetActive(false);
        Debug.unityLogger.logEnabled = true;
#else
        IsTrueAuto = false;
        AutoLevelTalbe.gameObject.SetActive(false);
        Debug.unityLogger.logEnabled = false;
#endif

        LimitTimeManager.Instance.StartTickTimer();
        StreakManager.Instance.UpdateWinStreak();

        ThemeDataItem curDataItem =
            ThemeManager.Instance.GetThemeDataItem(GameDataManager.Instance.UserData.userthemeid);
        Sprite sprite = GetSprite(curDataItem.iconName);

        // 换背景图的同时按主题 id 加载/切换大厅场景特效（没有对应 id 的预制体就不加载）
        ChangeBackgroundImage(sprite);

        // ② 满足「关卡进度 ≥ 1 + 已过 Loading」后
        Game.self.Pushs?.RequestEnableNotification();

        // ③ 广告追踪激励：首启时在加载阶段同意了授权，奖励在进入主界面后补弹"领取金币"弹窗
        AdTrackingManager.EnsureInstance().TryShowPendingReward();

        // 同步自 WT 1.9.8：保证玩家有默认昵称（WT 原位置在大厅特效方法末尾，此处放在启动流程末尾等价）
        if (string.IsNullOrEmpty(GameDataManager.Instance.UserData.UserName))
        {
            GameDataManager.Instance.UserData.UserName = FishInfoController.Instance.GeneratePlayerName();
        }
    }

    public void UpdateMyPersonInfo()
    {
        StartCoroutine(APIGateway.Instance.LoginApi.FetchUserProfile((res) =>
        {
            if (res == null)
            {
                UserData userData = GameDataManager.Instance.UserData;
                
                // 拉取失败，可使用本地缓存的旧数据，或构造默认空数据
                userProfile = new UserProfile()
                {
                    zen_level = userData.Zenlevel,
                    highest_zen_level = userData.Zenlevel,
                    old_zen_level = userData.Zenlevel,
                    hof_awards = new HofAwardsDto()
                };
            }
            else
            {
                userProfile = res;
                Debug.Log("用户信息 " + res);
            }
        }));
    }

    private void OnDestroy()
    {
        if (HTTPClient.Instance != null)
            HTTPClient.Instance.OnTokenExpired -= HandleTokenExpired;
    }


    private Sprite GetSprite(string spriteName)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(spriteName, "UI_Theme");
    }


    public void SetAutoLevelTalbe(bool isShow)
    {
        AutoLevelTalbe.gameObject.SetActive(isShow);
    }

    private void OnAutoLevelTalbeValueChanged(bool ison)
    {
        IsTrueAuto = ison;
        if (ison)
        {
            Game.self.Ratex2Game();
        }
        else
        {
            Game.self.ResumeGame();
        }

        EventDispatcher.instance.TriggerAutoPassLevel();
    }

    #endregion

    #region 私有方法
    
    /// <summary>
    /// 游戏初始化协程
    /// </summary>
    private IEnumerator InitializeGameRoutine()
    {
        // 原 0.2s，进大厅提速一并减半
        yield return new WaitForSeconds(0.1f);
        
        if (GameDataManager.Instance.UserData.IsFirstLaunch)
        {
            ShowGamePanel();
        }
        else
        {
            // 加载页 → 首次进大厅：标记走 2 倍速入场动画（消费一次后自动失效）
            PrimaryInterface.FastEnterOnce = true;
            SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
            
            yield return new WaitForSeconds(0.5f);
        }
       
        Game.self.Shop.Restore(null);
    }

    /// <summary>
    /// 显示游戏主界面
    /// </summary>
    public void ShowGamePanel()
    {
        // StageController.Instance.SetStageData(GameDataManager.Instance.UserData.CurrentStage);
        // SystemManager.Instance.ShowPanel(PanelType.GamePlayArea);

        ChessStageController.Instance.SetStageData(GameDataManager.Instance.UserData.CurrentChessStage);
        SystemManager.Instance.ShowPanel(PanelType.ChessPlayArea);
        GameDataManager.Instance.UserData.IsFirstLaunch = false;
    }

    /// <summary>
    /// 显示隐私协议界面
    /// </summary>
    private void ShowPrivacyScreen()
    {
        SystemManager.Instance.ShowPanel(PanelType.PolicyView);
    }

    private IEnumerator CheckNetworkConnection()
    {
        WaitForSeconds wait = new WaitForSeconds(5);
        bool wasNetworkActive = false; // 记录上一次的网络状态

        while (true)
        {
            bool isSuccess = false;
            using (UnityWebRequest request = UnityWebRequest.Head("https://www.apple.com/library/test/success.html"))
            {
                // 设置超时 5 秒
                request.timeout = 5;
                yield return request.SendWebRequest();
                // 成功条件：结果为 Success
                if (request.result == UnityWebRequest.Result.Success)
                {
                    isSuccess = true;
                }
                else
                {
                    Debug.Log($"网络检测失败: {request.error}");
                }
            }

            IsNetworkActive = isSuccess;

            if (isSuccess && !wasNetworkActive)
            {
                Debug.Log("<color=green>网络恢复！</color>");

                // 如果当前没有Token（比如之前是纯单机/游客模式），且设备有ID，尝试登录
                //bool needSync = !HTTPClient.Instance.IsTokenValid() || !GameDataManager.HasSyncedThisSession;
                //bool needSync = !GameDataManager.HasSyncedThisSession;

                //if (needSync && !string.IsNullOrEmpty(Game.self.GetUniqueId()) && _bgLoginCoroutine == null)
                {
                    Debug.Log("检测到网络恢复且未登录，尝试后台登录并同步数据...");
                    _bgLoginCoroutine = StartCoroutine(TryBackgroundLoginAndSync());
                }
                // 如果已经有Token了，说明之前登录过，下次触发 CommitGameData 时会自动上传，无需额外操作
            }

            if (!isSuccess && _bgLoginCoroutine != null)
            {
                Debug.LogWarning("网络再次断开，终止后台登录重试流程。");
                StopCoroutine(_bgLoginCoroutine);
                _bgLoginCoroutine = null;
                // 确保释放锁
                if (GameDataManager.Instance != null)
                    GameDataManager.Instance.IsWaitingForHistoryResolution = false;
            }

            wasNetworkActive = isSuccess;
            yield return wait;
        }
    }

    private Coroutine _reLoginCoroutine;

    private void HandleTokenExpired()
    {
        Debug.LogWarning("[GameCoreManager] Token 过期，尝试静默重新登录...");

        // 防止重复触发
        if (_reLoginCoroutine != null) return;

        _reLoginCoroutine = StartCoroutine(SilentReLogin());
    }

    // 【新增】静默重新登录
    private IEnumerator SilentReLogin()
    {
        // 如果正在处理历史弹窗，先解锁
        if (GameDataManager.Instance != null)
            GameDataManager.Instance.IsWaitingForHistoryResolution = true;

        bool loginCompleted = false;
        bool loginSuccess = false;

        if (!string.IsNullOrEmpty(Game.self.GetUniqueId()))
        {
            StartCoroutine(APIGateway.Instance.LoginApi.Login(
                (res) =>
                {
                    if (res != null)
                    {
                        loginSuccess = true;
                        Debug.Log("[GameCoreManager] Token 刷新成功");
                    }

                    loginCompleted = true;
                }
            ));

            yield return new WaitUntil(() => loginCompleted);
        }

        if (GameDataManager.Instance != null)
            GameDataManager.Instance.IsWaitingForHistoryResolution = false;

        _reLoginCoroutine = null;

        if (!loginSuccess)
        {
            Debug.LogWarning("[GameCoreManager] 重新登录失败，保持离线模式");
        }
    }

    private void OnDisable()
    {
        StopCoroutine(CheckNetworkConnection());
        if (_reLoginCoroutine != null) StopCoroutine(_reLoginCoroutine);
    }

    #endregion

    public void ChangeBackgroundImage(Sprite image)
    {
        backgroundImage.sprite = image;

        // 清理旧的模糊缓存，更新原图记录
        _originalBgSprite = image;
        if (_blurredBgSprite != null)
        {
            Destroy(_blurredBgSprite.texture); // 释放内存
            Destroy(_blurredBgSprite);
            _blurredBgSprite = null;
        }

        // 如果当前正处于模糊状态，重新生成新背景的模糊图
        if (_isBlurred)
        {
            ToggleBackgroundBlur(true);
        }

        // ★ 背景换了，同步按主题 id 切换场景特效（进入大厅 + 换肤都会走这里）
        int themeId = GameDataManager.Instance != null ? GameDataManager.Instance.UserData.userthemeid : 0;
        SetupLobbyEffect(themeId);
    }

    /// <summary>
    /// 按主题 id 加载并播放大厅场景特效。
    /// 没有对应预制体的主题：不加载、不播放（并清掉上一个主题的实例）。
    /// 粒子淡入淡出沿用原来 changjing01_texiao 那一套（FadeIn / FadeOut）。
    /// </summary>
    private void SetupLobbyEffect(int themeId)
    {
        // 同一主题已经处理过（无论加载成功还是判定为"无特效"），直接返回
        if (_lobbyEffectResolved && _lobbyEffectThemeId == themeId) return;

        // 先停掉可能还在跑的淡入淡出，避免它们引用到马上要销毁的粒子
        if (fadeInCoroutine != null) { StopCoroutine(fadeInCoroutine); fadeInCoroutine = null; }
        if (fadeOutCoroutine != null) { StopCoroutine(fadeOutCoroutine); fadeOutCoroutine = null; }

        // 清掉旧实例
        if (_lobbyEffect != null)
        {
            _lobbyEffect.SetActive(false);
            Destroy(_lobbyEffect);
            _lobbyEffect = null;
        }

        allPs.Clear();
        allParticleArrays.Clear();
        originalRates.Clear();
        isFadeInComplete = false;

        _lobbyEffectThemeId = themeId;
        _lobbyEffectResolved = true;

        if (!LobbyEffectByThemeId.TryGetValue(themeId, out string prefabName))
        {
            Debug.Log($"[LobbyEffect] 主题 {themeId} 没有配置场景特效，不加载");
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(LobbyEffectResPath + prefabName);
        if (prefab == null)
        {
            Debug.LogWarning($"[LobbyEffect] 找不到预制体 Resources/{LobbyEffectResPath}{prefabName}，不加载");
            return;
        }

        if (_lobbyEffectRoot == null)
        {
            // ★ UI 粒子必须挂在 Canvas 节点下才会渲染。
            //   优先用大厅背景图所在的那个 Canvas（RootCanvas 层级）；
            //   场景里没挂 changjing01 摆件时也能拿到正确的父节点。
            Canvas uiCanvas = backgroundImage != null ? backgroundImage.canvas : null;
            if (uiCanvas != null)
            {
                _lobbyEffectRoot = uiCanvas.transform;
            }
            else if (changjing01_texiao != null)
            {
                _lobbyEffectRoot = changjing01_texiao.transform.parent;
            }
            else
            {
                Canvas selfCanvas = GetComponentInParent<Canvas>();
                _lobbyEffectRoot = selfCanvas != null ? selfCanvas.transform : transform;
            }

            // 场景里预摆的 changjing01 摆件不再使用，统一走动态加载，避免同屏两份
            if (changjing01_texiao != null) changjing01_texiao.SetActive(false);
        }

        _lobbyEffect = Instantiate(prefab, _lobbyEffectRoot);
        _lobbyEffect.name = prefabName;

        // 沿用场景里那个摆件的位置/尺寸/层级，保证美术调好的效果不被重置
        RectTransform srcRT = changjing01_texiao != null ? changjing01_texiao.transform as RectTransform : null;
        RectTransform dstRT = _lobbyEffect.transform as RectTransform;
        if (srcRT != null && dstRT != null)
        {
            dstRT.anchorMin = srcRT.anchorMin;
            dstRT.anchorMax = srcRT.anchorMax;
            dstRT.pivot = srcRT.pivot;
            dstRT.anchoredPosition = srcRT.anchoredPosition;
            dstRT.sizeDelta = srcRT.sizeDelta;
            dstRT.localScale = srcRT.localScale;
        }

        // 层级：紧跟在 Background 后面 —— 压在背景之上、UI 面板之下
        if (dstRT != null)
        {
            RectTransform bgRT = backgroundImage != null ? backgroundImage.rectTransform : null;
            if (bgRT != null && bgRT.parent == dstRT.parent)
                dstRT.SetSiblingIndex(bgRT.GetSiblingIndex() + 1);
            else
                dstRT.SetAsLastSibling();
        }

        _lobbyEffect.SetActive(true);

        // 沿用原逻辑：收集粒子系统、预分配数组、记录原始发射率
        allPs.AddRange(_lobbyEffect.GetComponentsInChildren<ParticleSystem>());
        foreach (var ps in allPs)
        {
            allParticleArrays.Add(new ParticleSystem.Particle[ps.main.maxParticles]);
            originalRates.Add(ps.emission.rateOverTime.constant);
        }

        Debug.Log($"[LobbyEffect] 主题 {themeId} → {prefabName}，粒子系统 {allPs.Count} 个");

        // 进场直接淡入，和原来 changjing01 的表现一致
        FadeIn(0.5f);
    }

    /// <summary>
    /// 让所有子粒子系统从透明淡入到不透明（同时恢复发射）
    /// </summary>
    public void FadeIn(float duration = 0.5f)
    {
        // 该主题没有场景特效，直接跳过
        if (_lobbyEffect == null) return;

        // 如果已经处于淡入完成状态，跳过（可根据需要调整）
        if (isFadeInComplete && _lobbyEffect.activeSelf) return;

        // 停止正在进行的淡入淡出协程
        if (fadeInCoroutine != null) StopCoroutine(fadeInCoroutine);
        if (fadeOutCoroutine != null) StopCoroutine(fadeOutCoroutine);

        // 确保物体激活
        if (!_lobbyEffect.activeSelf)
            _lobbyEffect.SetActive(true);

        fadeInCoroutine = StartCoroutine(FadeInCoroutine(duration));
    }

    // 淡入协程
    private IEnumerator FadeInCoroutine(float duration)
    {
        // 1. 恢复所有粒子系统的发射率（并确保发射开启）
        for (int i = 0; i < allPs.Count; i++)
        {
            var emission = allPs[i].emission;
            emission.rateOverTime = originalRates[i];
            emission.rateOverDistance = 0; // 可根据需要恢复
            // 如果之前被Stop了，重新Play
            allPs[i].Play();
        }

        float elapsed = 0f;
        // 先清除掉之前可能残留的粒子（透明度为0也可以，但最好清空重新产生）
        // 这里选择不清除，而是将所有现存粒子alpha置0再淡入，更平滑
        // 获取所有粒子，先设为0
        foreach (var ps in allPs)
        {
            int count = ps.GetParticles(allParticleArrays[allPs.IndexOf(ps)]);
            var particles = allParticleArrays[allPs.IndexOf(ps)];
            for (int i = 0; i < count; i++)
            {
                Color c = particles[i].startColor;
                c.a = 0f;
                particles[i].startColor = c;
            }

            ps.SetParticles(particles, count);
        }

        // 2. 逐渐增加透明度
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / duration);

            for (int i = 0; i < allPs.Count; i++)
            {
                int count = allPs[i].GetParticles(allParticleArrays[i]);
                var particles = allParticleArrays[i];
                for (int j = 0; j < count; j++)
                {
                    Color c = particles[j].startColor;
                    c.a = alpha;
                    particles[j].startColor = c;
                }

                allPs[i].SetParticles(particles, count);
            }

            yield return null;
        }

        // 3. 确保所有粒子alpha = 1（最后微调）
        for (int i = 0; i < allPs.Count; i++)
        {
            int count = allPs[i].GetParticles(allParticleArrays[i]);
            var particles = allParticleArrays[i];
            for (int j = 0; j < count; j++)
            {
                Color c = particles[j].startColor;
                c.a = 1f;
                particles[j].startColor = c;
            }

            allPs[i].SetParticles(particles, count);
        }

        isFadeInComplete = true;
        fadeInCoroutine = null;
    }


    // 原 FadeOut 方法改为调用下面的协程
    public void FadeOut(float duration = 0.5f)
    {
        FadeOut(duration, null);
    }

    // 新增重载：支持完成回调（比如隐藏物体）
    public void FadeOut(float duration, Action onComplete)
    {
        // 该主题没有场景特效，直接走完成回调
        if (_lobbyEffect == null || allPs.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        if (fadeOutCoroutine != null) StopCoroutine(fadeOutCoroutine);
        if (fadeInCoroutine != null) StopCoroutine(fadeInCoroutine);

        fadeOutCoroutine = StartCoroutine(FadeOutCoroutine(duration, onComplete));
    }

    private IEnumerator FadeOutCoroutine(float duration, Action onComplete)
    {
        // 1. 停止发射新粒子
        foreach (var ps in allPs)
        {
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
        }

        float elapsed = 0f;

        // 2. 将现有粒子透明度从1降到0
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);

            for (int i = 0; i < allPs.Count; i++)
            {
                int count = allPs[i].GetParticles(allParticleArrays[i]);
                var particles = allParticleArrays[i];
                for (int j = 0; j < count; j++)
                {
                    Color c = particles[j].startColor;
                    c.a = alpha;
                    particles[j].startColor = c;
                }

                allPs[i].SetParticles(particles, count);
            }

            yield return null;
        }

        // 3. 彻底清除所有粒子
        foreach (var ps in allPs)
        {
            ps.Clear();
        }

        isFadeInComplete = false;
        fadeOutCoroutine = null;

        // 4. 执行回调（例如隐藏物体）
        onComplete?.Invoke();
    }

    public void SetBackgroundImage(Color color)
    {
        if (PanelState == PanelState.GamePingPanel || PanelState == PanelState.GameXiaoPanel)
        {
            FadeOut(0.5f);
        }
        else if (_lobbyEffect != null)
        {
            // 回到大厅/主界面：把当前主题的特效淡回来（不再限定主题 0）
            if (!_lobbyEffect.activeSelf) _lobbyEffect.SetActive(true);
            FadeIn(0.5f);
        }

        backgroundImage.color = Color.Lerp(backgroundImage.color, color, 2f);
    }


    /// <summary>
    /// 开启或关闭背景模糊
    /// </summary>
    /// <param name="isOn">true为开启模糊，false为恢复原图</param>
    public void ToggleBackgroundBlur(bool isOn)
    {
        if (backgroundImage == null) return;
        _isBlurred = isOn;

        if (isOn)
        {
            if (blurMaterial == null)
            {
                Debug.LogWarning("未分配模糊材质球！");
                return;
            }

            // 1. 备份原图
            if (_originalBgSprite == null)
            {
                _originalBgSprite = backgroundImage.sprite;
            }

            // 2. 如果还没生成过模糊图，就通过代码渲染一张
            if (_blurredBgSprite == null)
            {
                _blurredBgSprite = CreateBlurredSprite(_originalBgSprite, blurMaterial, downSample, blurIterations);
            }

            // 3. 替换为模糊图
            backgroundImage.sprite = _blurredBgSprite;
        }
        else
        {
            // 恢复原图
            if (_originalBgSprite != null)
            {
                backgroundImage.sprite = _originalBgSprite;
            }
        }
    }

    /// <summary>
    /// 核心：通过C#代码进行降采样和多Pass交替模糊渲染 (Ping-Pong)
    /// </summary>
    private Sprite CreateBlurredSprite(Sprite sourceSprite, Material blurMat, int doSample, int iterations)
    {
        if (sourceSprite == null || sourceSprite.texture == null) return null;

        Texture2D sourceTex = sourceSprite.texture;

        // 1. 获取该 Sprite 在图集（或原图）中的真实像素尺寸
        int spriteWidth = Mathf.CeilToInt(sourceSprite.textureRect.width);
        int spriteHeight = Mathf.CeilToInt(sourceSprite.textureRect.height);

        // 2. 根据 Sprite 的真实尺寸计算降采样后的分辨率
        int width = Mathf.Max(1, spriteWidth / doSample);
        int height = Mathf.Max(1, spriteHeight / doSample);
        // 申请两张临时的 RenderTexture 用于交替渲染
        RenderTexture rt1 = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture rt2 = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        // 关键：确保双线性滤波，这样放大时才会有平滑的晕开效果
        rt1.filterMode = FilterMode.Bilinear;
        rt2.filterMode = FilterMode.Bilinear;

        // 3. 【关键修复】计算该 Sprite 在整张 Texture (图集) 中的 UV 缩放和偏移
        Vector2 scale = new Vector2(
            sourceSprite.textureRect.width / sourceTex.width,
            sourceSprite.textureRect.height / sourceTex.height
        );
        Vector2 offset = new Vector2(
            sourceSprite.textureRect.x / sourceTex.width,
            sourceSprite.textureRect.y / sourceTex.height
        );
        // 将原图拷贝到 rt1，完成第一次降采样， 使用带有 scale 和 offset 的 Blit，只把图集中属于这个 Sprite 的区域画入 rt1
        Graphics.Blit(sourceTex, rt1, scale, offset);

        // 4. 多次迭代模糊 (Kawase 思想)
        for (int i = 0; i < iterations; i++)
        {
            // 每次迭代稍微扩大一点采样半径
            blurMat.SetFloat("_BlurRadius", 1.0f + i * 0.5f);
            // rt1 模糊渲染到 rt2
            Graphics.Blit(rt1, rt2, blurMat);
            // rt2 模糊渲染回 rt1
            Graphics.Blit(rt2, rt1, blurMat);
        }

        // 5. 将处理好的 RenderTexture 读回到 Texture2D (因为 UI Image 需要 Sprite)
        Texture2D blurredTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture.active = rt1;
        blurredTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        blurredTex.Apply();
        RenderTexture.active = null;

        // 6. 释放临时内存
        RenderTexture.ReleaseTemporary(rt1);
        RenderTexture.ReleaseTemporary(rt2);

        // 7. 生成并返回新的 Sprite
        return Sprite.Create(blurredTex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }

    private int _loginRetryCount = 0;
    private const int MAX_RETRY = 5;

    private IEnumerator TryBackgroundLoginAndSync()
    {
        _loginRetryCount = 0;
        while (_loginRetryCount <= MAX_RETRY)
        {
            // 开启同步锁，阻止后台保存时覆盖服务器数据
            if (GameDataManager.Instance != null)
                GameDataManager.Instance.IsWaitingForHistoryResolution = true;
            
            bool loginRequestCompleted = false;
            LoginResponse loginResponse = null;
            StartCoroutine(APIGateway.Instance.LoginApi.Login((res) =>
            {
                if (res != null) loginResponse = res as LoginResponse;
                loginRequestCompleted = true;
            }));

            yield return new WaitUntil(() => loginRequestCompleted);

            if (HTTPClient.Instance.IsTokenValid())
            {
                bool fetchCompleted = false;
                StartCoroutine(APIGateway.Instance.LoginApi.GetUserData((response) =>
                {
                    if (response != null && !string.IsNullOrEmpty(response.UserData))
                    {
                        // 解析服务端数据
                        UserData serverUserData = null;
                        try
                        {
                            // 尝试解析数据
                            serverUserData = JsonConvert.DeserializeObject<UserData>(response.UserData);
                        }
                        catch (Exception ex)
                        {
                            // 如果 JSON 格式错误，捕获异常，防止协程崩溃
                            Debug.LogError($"[GameCoreManager] 解析服务端 UserData 异常: {ex.Message} \n原始数据: {response.UserData}");
                        }
                        if(serverUserData != null)
                            ShowGlobalHistoryPanel(serverUserData, response.ExtraData, loginResponse);
                        else
                        {
                            // 【关键防御】如果解析失败，或者解析出来就是 null，必须立刻解锁！
                            Debug.LogWarning("[GameCoreManager] 服务端数据无效或解析失败，取消历史记录比对并解锁。");
                            if (GameDataManager.Instance != null)
                                GameDataManager.Instance.IsWaitingForHistoryResolution = false;
                        }
                    }
                    else
                    {
                        // 如果服务器没有数据或拉取失败，解除锁
                        if (GameDataManager.Instance != null)
                            GameDataManager.Instance.IsWaitingForHistoryResolution = false;
                    }
                    fetchCompleted = true;
                }));
                yield return new WaitUntil(() => fetchCompleted);
                // 成功后跳出整个重试循环
                break;
            }
            else
            {
                Debug.LogWarning("后台登录失败，保持单机模式。");
                // 【关键】无论协程如何结束（正常/中断/异常），如果登录没成功，必须解锁
                if (GameDataManager.Instance != null)
                {
                    GameDataManager.Instance.IsWaitingForHistoryResolution = false;
                }
                // 【可选】：指数退避重试
                if (_loginRetryCount < MAX_RETRY)
                {
                    _loginRetryCount++;
                    // 退避时间：30s, 60s, 120s
                    float delay = 30f * Mathf.Pow(2, _loginRetryCount - 1);
                    Debug.Log($"后台登录失败，{delay}秒后进行第 {_loginRetryCount} 次重试...");

                    yield return new WaitForSeconds(delay);
                }
                else
                {
                    Debug.LogWarning("后台登录重试次数已用完，等待下次断网重连。");
                    _loginRetryCount = 0; // 重置计数
                    break;
                }
            }
        }
        // 无论成功还是重试用尽，协程结束时注销句柄
        _bgLoginCoroutine = null;
    }

    private void ShowGlobalHistoryPanel(UserData serverUserData, ExtraDataDto serverExtraData, LoginResponse loginResponse)
    {
        if (serverUserData.overallZenScore <= GameDataManager.Instance.UserData.overallZenScore)
        {
            // 无变化
            GameDataManager.Instance.IsWaitingForHistoryResolution = false;
            return;
        }
        // 1. 解析最高连胜天数 (对应 JSON 里的 _signSaveData.historyWinDayTimes)
        int maxStreak = 0;
        if (serverUserData._signSaveData != null) 
        {
            // 假设你在 UserData 中定义了这个字段，具体名称请对应你的 C# 数据结构
            maxStreak = serverUserData._signSaveData.historyWinDayTimes; 
        }
        // 构造数据
        HistoryData dataToDisplay = new HistoryData
        {
            playerName = string.IsNullOrEmpty(serverUserData.UserName) ? "历史玩家" : serverUserData.UserName,
            zenScore = serverUserData.overallZenScore,
            zenLevel = OverallRankingManager.Instance.GetZenLevelByScore(serverUserData.zenCount),
            crosswordProgress = serverUserData.CurrentChessStage,
            registerDate = string.IsNullOrEmpty(serverUserData.firstLoginTime) ? "未知" : serverUserData.firstLoginTime,
            coins = serverUserData.Gold,
            maxStreakDays = maxStreak,
            avatar = serverUserData.UserHeadId
        };
        UIWindow uiWindow = SystemManager.Instance.ShowPanel(PanelType.HistoryDataPanel);
        var historyPanel = uiWindow?.GetComponent<HistoryDataPanel>();
        if (historyPanel != null)
        {
            historyPanel.ShowPanel(dataToDisplay,
                onDiscard: () =>
                {
                    Debug.Log("中途恢复网络：玩家放弃服务器数据，继续使用本地。");
                    // 可以调用接口把本地数据强行覆写到服务器，保证后续一致性
                    if (GameDataManager.Instance != null)
                    {
                        GameDataManager.Instance.IsWaitingForHistoryResolution = false;
                        GameDataManager.HasSyncedThisSession = true;
                        // 玩家选择保留本地，此时把本地数据强行推送到服务器，保证后续一致性
                        GameDataManager.Instance.CommitGameData();
                    }
                },
                onApply: (appliedData) =>
                {
                    Debug.Log("中途恢复网络：玩家应用服务器数据，覆盖本地。");
                    if (GameDataManager.Instance != null)
                    {
                        GameDataManager.Instance.IsWaitingForHistoryResolution = false;
                        GameDataManager.Instance.OverwriteLocalWithServerData(serverUserData, serverExtraData);
                    }
                    
                    SystemManager.Instance.HidePanel(PanelType.HeaderSection);
                    SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
                    
                    //SceneManager.UnloadSceneAsync(1);
                    
                    SceneManager.LoadScene(0);
                }
            );
        }
        else
        {
            // 容错处理：如果面板没找到，记得解锁
            if (GameDataManager.Instance != null)
                GameDataManager.Instance.IsWaitingForHistoryResolution = false;
        }
    }
}