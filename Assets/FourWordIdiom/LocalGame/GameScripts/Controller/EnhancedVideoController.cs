using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using DG.Tweening;
using System.Collections;

/// <summary>
/// 增强版视频播放控制器（单例模式）
/// 功能：安全播放控制、准备状态检测、错误处理、UI过渡效果
/// </summary>
public class EnhancedVideoController : MonoBehaviour
{
    // 单例实例（只读属性）
    public static EnhancedVideoController Instance { get; private set; }

    [Header("核心组件")]
    [SerializeField] private VideoPlayer videoPlayer;      // Unity视频播放组件
    [SerializeField] private Image loadingOverlay;         // 加载遮罩UI

    [Header("播放设置")]
    [SerializeField] private float fadeDuration = 1f;    // 淡入淡出动画时长（秒）
    [SerializeField] private float preparationTimeout = 5f; // 视频准备超时时间（秒）

    private bool isPreparing;                              // 视频准备状态标志

    #region Unity生命周期
    private void Awake()
    {
        InitializeSingleton();    // 初始化单例
        ConfigureVideoPlayer();   // 配置播放器参数
    }

    // 启用时注册事件
    private void OnEnable() => RegisterEvents();

    // 禁用时注销事件
    private void OnDisable() => UnregisterEvents();

    // 销毁时清理资源
    private void OnDestroy() => CleanupResources();
    #endregion

    #region 公共接口
    /// <summary>
    /// 播放当前设置的视频剪辑
    /// </summary>
    public void PlayVideo()
    {
         StopAllPlayback();       // 先停止当前播放
        StartCoroutine(PlayRoutine(videoPlayer.clip)); // 启动播放协程
    }

    /// <summary>
    /// 切换暂停/播放状态
    /// </summary>
    public void TogglePause()
    {
        if (videoPlayer.isPlaying)
        {
            videoPlayer.playbackSpeed = 0;           // 暂停播放
            ShowLoadingOverlay();             // 显示加载遮罩
        }
        else
        {
            videoPlayer.playbackSpeed = 1;             // 继续播放
            HideLoadingOverlay();             // 隐藏加载遮罩
        }
    }

    /// <summary>
    /// 完全停止视频播放
    /// </summary>
    public void StopAllPlayback()
    {
        videoPlayer.Stop();                  // 停止播放器
        ShowLoadingOverlay();                 // 强制显示加载遮罩
    }
    #endregion

    #region 核心逻辑
    /// <summary>
    /// 视频播放协程（处理准备和播放流程）
    /// </summary>
    private IEnumerator PlayRoutine(VideoClip clip)
    {
        ShowLoadingOverlay();                 // 显示加载动画
        videoPlayer.Prepare();               // 开始准备视频

        // 启动并等待准备监控协程
        yield return new WaitForSeconds(1f);

        // 准备完成后开始播放
        if (videoPlayer.isPrepared)
        {
            videoPlayer.Play();               // 开始播放
            videoPlayer.playbackSpeed = 1;        // 开始播放
            HideLoadingOverlay();             // 隐藏加载动画
        }
    }
    #endregion

    #region 事件处理
    /// <summary>
    /// 视频错误事件处理
    /// </summary>
    private void HandleVideoError(VideoPlayer source, string message)
    {
        Debug.LogError($"视频播放错误: {message}");
        StopAllPlayback();  // 出错时停止播放
    }

    /// <summary>
    /// 视频播放结束事件处理
    /// </summary>
    private void HandleVideoEnd(VideoPlayer source)
    {
        //Debug.Log("视频自然播放结束");
        // 可在此处添加循环播放或触发结束事件
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
        DontDestroyOnLoad(gameObject);  // 跨场景保持
    }

    /// <summary>
    /// 配置视频播放器基础参数
    /// </summary>
    private void ConfigureVideoPlayer()
    {
        videoPlayer.playOnAwake = false;    // 禁用自动播放
        videoPlayer.waitForFirstFrame = true; // 等待首帧
    }

    /// <summary>
    /// 注册视频事件监听
    /// </summary>
    private void RegisterEvents()
    {
        videoPlayer.errorReceived += HandleVideoError;          // 错误事件
        videoPlayer.loopPointReached += HandleVideoEnd;        // 结束事件
    }

    /// <summary>
    /// 注销视频事件监听
    /// </summary>
    private void UnregisterEvents()
    {
        videoPlayer.errorReceived -= HandleVideoError;
        videoPlayer.loopPointReached -= HandleVideoEnd;
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    private void CleanupResources()
    {
        StopAllPlayback();       // 停止播放
        videoPlayer.clip = null; // 释放视频资源
    }

    /// <summary>
    /// 显示加载遮罩（渐入动画）
    /// </summary>
    private void ShowLoadingOverlay() => loadingOverlay.DOFade(1, fadeDuration);

    /// <summary>
    /// 隐藏加载遮罩（渐出动画）
    /// </summary>
    private void HideLoadingOverlay() => loadingOverlay.DOFade(0, fadeDuration);
    #endregion
}