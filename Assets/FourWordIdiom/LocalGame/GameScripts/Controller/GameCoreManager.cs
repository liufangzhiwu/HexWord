using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.HuaweiAppGallery;
using UnityEngine.Rendering;
using UnityEngine.UI;
#if UNITY_IOS
using UnityEngine.iOS;
#endif

public enum PanelState
{
    Null,MainMenuPanel,FinishXiaoPanel,FinishPingPanel,GameXiaoPanel,GamePingPanel,FinishHexPanel,GameHexPanel
}

/// <summary>
/// 游戏核心管理器（单例模式）
/// 功能：
/// 1. 游戏全局初始化
/// 2. 隐私协议处理
/// 3. 设备信息检测
/// 4. 游戏流程控制
/// </summary>
public sealed class GameCoreManager: MonoBehaviour
{
    #region 单例实现
    public static GameCoreManager Instance;
    
    #endregion
    
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject changjing01_texiao;
    [Header("Blur Settings")]
    [SerializeField] private Material blurMaterial; // 挂载你第一版写的3x3模糊Shader的材质球
    [Range(1, 8)] 
    [SerializeField] private int downSample = 4; // 降采样倍数（建议4，越大越平滑且计算越快）
    [Range(1, 5)] 
    [SerializeField] private int blurIterations = 3; // 模糊迭代次数（建议3次）

    private Sprite _originalBgSprite;
    private Sprite _blurredBgSprite;
    private bool _isBlurred = false; // 当前模糊状态
    
    
    public bool IsTrueAuto;
    public GameObject AutoLevelTalbe;
    public PanelState PanelState=PanelState.Null;
    
    
    // 原 ps 变量删除，换成以下：
    private List<ParticleSystem> allPs = new List<ParticleSystem>();
    private List<ParticleSystem.Particle[]> allParticleArrays = new List<ParticleSystem.Particle[]>();
    private List<float> originalRates = new List<float>();

    // 协程管理
    private Coroutine fadeInCoroutine;
    private Coroutine fadeOutCoroutine;

    // 新增：淡入时是否已经完成（避免重复）
    private bool isFadeInComplete = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 保持广告管理器在场景切换时不销毁
        }
    }

    private void Start()
    {
        
        ChessDynamicHardManager.Instance.Initialized();
        ChessStageController.Instance.Initialized();
        
#if UNITY_huawei && !UNITY_EDITOR
        HuaweiGameService.ShowFloatWindow();
        StartCoroutine(CheckOrderShipmentCompleted());
#endif
        Game.self._uiRoot=SystemManager.Instance._uiRoot;
        StartCoroutine(InitializeGameRoutine());
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
        
        ThemeDataItem curDataItem=ThemeManager.Instance.GetThemeDataItem(GameDataManager.Instance.UserData.userthemeid);
        Sprite sprite = GetSprite(curDataItem.iconName);
        
        ChangeBackgroundImage(sprite);
        
        // ===== 替换原来获取 ps 的代码 =====
        allPs.Clear();
        allParticleArrays.Clear();
        originalRates.Clear();

        // 获取所有子物体上的粒子系统（包括自身若有）
        allPs.AddRange(changjing01_texiao.GetComponentsInChildren<ParticleSystem>());

        foreach (var ps in allPs)
        {
            // 预分配每个粒子系统的粒子数组
            int maxParticles = ps.main.maxParticles;
            allParticleArrays.Add(new ParticleSystem.Particle[maxParticles]);
            // 保存原始发射率（用于淡入时恢复）
            originalRates.Add(ps.emission.rateOverTime.constant);
        }
    }
    
    
    private Sprite GetSprite(string spriteName)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(spriteName,"UI_Theme");
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

    #region 私有方法
    /// <summary>
    /// 初始化多语言字符串
    /// </summary>
    private void InitializeLanguageStrings()
    {
        //string TimeHourText = _languageManager.GetString("TimeH") + " ";
        //string TimeMinuteText = _languageManager.GetString("TimeM");
    }

    /// <summary>
    /// 游戏初始化协程
    /// </summary>
    private IEnumerator InitializeGameRoutine()
    {
        yield return new WaitForSeconds(0.2f);
        
        if (GameDataManager.Instance.UserData.IsFirstLaunch)
        {
            ChessStageController.Instance.SetStageData(GameDataManager.Instance.UserData.CurrentChessStage);
            yield return new WaitForSeconds(0.2f);
            SystemManager.Instance.ShowPanel(PanelType.ChessPlayArea);
            // 标记非首次进入
            GameDataManager.Instance.UserData.IsFirstLaunch = false;
            
            AnalyticMgr.ActivityBegin("蝶园活动");
        }
        else
        {
            SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
        }
    }

    /// <summary>
    /// 显示隐私协议界面
    /// </summary>
    private void ShowPrivacyScreen()
    {
        SystemManager.Instance.ShowPanel(PanelType.PolicyView);
    }

    // 检查发货是否完成
    private IEnumerator CheckOrderShipmentCompleted()
    {
        yield return new WaitForSeconds(2f);
        Game.self.Shop.Restore((ok, items) =>
        {
            foreach (ProductItem item in items)
            {
                ShopManager.shopManager.OnPurchaseSuccess(item);
            }
        });
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
    }

       /// <summary>
     /// 让所有子粒子系统从透明淡入到不透明（同时恢复发射）
     /// </summary>
     public void FadeIn(float duration = 0.5f)
     {
         // 如果已经处于淡入完成状态，跳过（可根据需要调整）
         if (isFadeInComplete && changjing01_texiao.activeSelf) return;
 
         // 停止正在进行的淡入淡出协程
         if (fadeInCoroutine != null) StopCoroutine(fadeInCoroutine);
         if (fadeOutCoroutine != null) StopCoroutine(fadeOutCoroutine);
 
         // 确保物体激活
         if (!changjing01_texiao.activeSelf)
             changjing01_texiao.SetActive(true);
 
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
        if (PanelState == PanelState.GamePingPanel || PanelState == PanelState.GameHexPanel)
        {
            //changjing01_texiao.gameObject.SetActive(false);
            FadeOut(0.5f);

        }else
        {
            if (GameDataManager.Instance.UserData.userthemeid == 0)
            {
                FadeIn(0.5f);
            }
            
            changjing01_texiao.gameObject.SetActive(GameDataManager.Instance.UserData.userthemeid==0);
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

    private void OnDisable()
    {
        #if UNITY_huawei && !UNITY_EDITOR
        HuaweiGameService.HideFloatWindow();
        #endif
    }
}
