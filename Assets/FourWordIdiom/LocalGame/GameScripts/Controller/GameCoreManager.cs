using System;
using System.Collections;
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
   Null,MainMenuPanel,FinishHexPanel,GameHexPanel,GamePingPanel,FinishPingPanel
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
        
        ThemeDataItem curDataItem=ThemeManager.Instance.GetThemeDataItem(GameDataManager.Instance.UserData.userthemeid);
        Sprite sprite = GetSprite(curDataItem.iconName);
        ChangeBackgroundImage(sprite);
    }
    
    
    private Sprite GetSprite(string spriteName)
    {
        return AssetBundleLoader.SharedInstance.GetSpriteFromAtlas(spriteName,"UI_Theme");
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

    public void SetBackgroundImage(Color color)
    {
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
