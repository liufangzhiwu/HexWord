using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using DG.Tweening;
using System.Collections;
using System.IO;

/// <summary>
/// 增强版视频播放控制器（单例模式）
/// 功能：安全播放控制、准备状态检测、错误处理、UI过渡效果
/// </summary>
public class EnhancedVideoController : MonoBehaviour
{
    // 单例实例（只读属性）
    public static EnhancedVideoController Instance { get; private set; }

    [Header("核心组件")]
    // [SerializeField] private VideoPlayer videoPlayer;      // Unity视频播放组件
    [SerializeField] private Image loadingOverlay;         // 加载遮罩UI

    [Header("播放设置")]
    [SerializeField] private float fadeDuration = 1f;    // 淡入淡出动画时长（秒）
    [SerializeField] private float preparationTimeout = 5f; // 视频准备超时时间（秒）
    
    [SerializeField] private string videoRelPath = "Video/flower.mp4";
    
    private bool isPrepared;                              // 视频准备状态标志

    #region Unity生命周期
    private void Awake()
    {
        InitializeSingleton();    // 初始化单例
        // ConfigureVideoPlayer();   // 配置播放器参数
    }

    private void Start()
    {
        // string finalUrl = GetPlatformSpecificPath();
        // Debug.Log($"[VideoController] 最终播放地址: {finalUrl}");
        //
        // videoPlayer.source = VideoSource.Url;
        // videoPlayer.url = finalUrl;
        // PrepareVideo();
    }


    // 销毁时清理资源
    private void OnDestroy() => CleanupResources();
    private string GetPlatformSpecificPath()
    {
        string finalUrl = "";

#if UNITY_EDITOR
        // ==========================================
        // 编辑器模式：本地绝对路径 + file:// 协议
        // ==========================================
        string localPath = Path.Combine(Application.streamingAssetsPath, videoRelPath);
        localPath = localPath.Replace("\\", "/");
        // Windows 编辑器必须加 file:// 否则会报 empty file 错误
        finalUrl = "file://" + localPath; 
        string basePath = "https://zen.test.mindwordplay.cn/hex";
#else
        // ==========================================
        // 微信/WebGL模式：CDN 绝对路径
        // ==========================================
        string basePath = "https://zen.test.mindwordplay.cn/hex";
        finalUrl = $"{basePath}/{videoRelPath}";
#endif
        
        finalUrl = $"{basePath}/{videoRelPath}";
        return finalUrl;
    }
    #endregion

    #region 公共接口
    /// <summary>
    /// 播放当前设置的视频剪辑
    /// </summary>
    public void PlayVideo()
    {
        // if (isPrepared)
        // {
        //     DoPlay();
        // }
        // else
        // {
        //     ShowLoadingOverlay();
        //     videoPlayer.Prepare();
        // }
    }

    /// <summary>
    /// 切换暂停/播放状态
    /// </summary>
    public void TogglePause()
    {
        // if (videoPlayer.isPlaying)
        // {
        //     videoPlayer.Pause();           // 暂停播放
        //     // ShowLoadingOverlay();             // 显示加载遮罩
        // }
        // else
        // {
        //     videoPlayer.Play();          // 继续播放
        //     HideLoadingOverlay();             // 隐藏加载遮罩
        // }
    }

    /// <summary>
    /// 完全停止视频播放
    /// </summary>
    public void StopAllPlayback()
    {
        // videoPlayer.Stop();                  // 停止播放器
        ShowLoadingOverlay();                 // 强制显示加载遮罩
    }
    #endregion

    #region 核心逻辑
    /// <summary>
    /// 视频播放协程（处理准备和播放流程）
    /// </summary>
    private void PrepareVideo()
    {
        ShowLoadingOverlay();                 // 显示加载动画
    
        
        // videoPlayer.prepareCompleted += OnPrepareCompleted;
        // videoPlayer.errorReceived += HandleVideoError;
       
        // videoPlayer.Prepare();               // 开始准备视频
        StartCoroutine(CheckPreparationTimeout());
    }
    private IEnumerator CheckPreparationTimeout()
    {
        float timer = 0f;
        while (!isPrepared && timer < preparationTimeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // 如果时间到了还没准备好
        if (!isPrepared)
        {
            Debug.LogError("[Video] 视频加载超时！");
            // 停止尝试
            // videoPlayer.Stop(); 
            // 隐藏 Loading，甚至可以弹个提示说“网络不佳”
            HideLoadingOverlay(); 
            // 可选：显示重试按钮
        }
    }
    private void OnPrepareCompleted(object source)
    {
        Debug.Log("[Video] 准备完成");
        isPrepared = true;
        
        // 移除事件防止重复调用
        // videoPlayer.prepareCompleted -= OnPrepareCompleted;
        
        // 这里可以选择直接播放，或者等待外部调用 PlayVideo
        // 如果想要自动播放：
        DoPlay(); 
    }
    private void DoPlay()
    {
        // HideLoadingOverlay();
        // videoPlayer.Play();
    }
    #endregion

    #region 事件处理
    /// <summary>
    /// 视频错误事件处理
    /// </summary>
    private void HandleVideoError(object source, string message)
    {
        Debug.LogError($"视频播放错误: {message}");
        loadingOverlay.DOFade(1, 0);
    }
    
    #endregion

    #region 辅助方法
    /// <summary>
    /// 初始化单例模式
    /// </summary>
    private void InitializeSingleton()
    {
        // 防止重复实例
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // DontDestroyOnLoad(gameObject);  // 跨场景保持
    }

    /// <summary>
    /// 配置视频播放器基础参数
    /// </summary>
    private void ConfigureVideoPlayer()
    {
        // videoPlayer.playOnAwake = false;    // 禁用自动播放
        // videoPlayer.waitForFirstFrame = true; // 等待首帧
        // // 🔥 微信小游戏重要设置：静音以允许自动播放
        // videoPlayer.SetDirectAudioMute(0, true);
        // videoPlayer.aspectRatio = VideoAspectRatio.FitOutside; // 推荐：填满屏幕但裁切边缘
    }
    
    /// <summary>
    /// 清理资源
    /// </summary>
    private void CleanupResources()
    {
        // videoPlayer.prepareCompleted -= OnPrepareCompleted;
        // videoPlayer.errorReceived -= HandleVideoError;
        // if (videoPlayer.isPlaying)
        // {
        //     videoPlayer.Stop();
        // }
        // if (videoPlayer.targetTexture != null)
        // {
        //     videoPlayer.targetTexture.Release();
        // }
    
        Debug.Log("[Video] 资源已彻底释放");
    }

    /// <summary>
    /// 显示加载遮罩（渐入动画）
    /// </summary>
    private void ShowLoadingOverlay()
    {
        loadingOverlay.gameObject.SetActive(true);
        loadingOverlay.DOFade(1, fadeDuration);
    }

    /// <summary>
    /// 隐藏加载遮罩（渐出动画）
    /// </summary>
    private void HideLoadingOverlay()
    {
        loadingOverlay.DOFade(0, fadeDuration).OnComplete(() =>
        {
            loadingOverlay.gameObject.SetActive(false);
        });
    }

    #endregion
}