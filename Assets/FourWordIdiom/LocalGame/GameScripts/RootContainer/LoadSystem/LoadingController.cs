using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


/// <summary>
/// 游戏加载控制器
/// 主要功能：
/// 1. 管理游戏初始化加载流程
/// 2. 显示加载进度和提示信息
/// 3. 预加载关键游戏资源
/// 与原LoadPanel的主要差异：
/// - 完全重构的加载流程管理
/// - 新增资源依赖系统
/// - 改进进度反馈机制
/// </summary>
public class LoadingController : MonoBehaviour
{
    [Header("UI组件引用")]
    [SerializeField] private Text loadingHintText;    // 加载提示文本
    [SerializeField] private Slider progressSlider;   // 进度条组件
     [SerializeField] private GameObject Loading;   // 进度条组件
    // [SerializeField] private Button AccountQuitBtn;   // 进度条组件
    //[SerializeField] private RectTransform indicatorIcon; // 进度指示图标

    [Header("加载配置")]
    //[SerializeField] private float minLoadingTime = 5f; // 最小加载时间(秒)
    [SerializeField] private int randomHintCount = 20;    // 随机提示数量

    private AsyncOperation sceneLoadOperation;        // 场景加载操作
    private float loadStartTime;                      // 加载开始时间

    private void Start()
    {
        StartCoroutine(InitializeLoadingProcess());
    }


    /// <summary>
    /// 初始化加载流程
    /// </summary>
    IEnumerator InitializeLoadingProcess()
    {
        yield return new WaitForSeconds(0.05f);
        loadStartTime = Time.time;
        InitializeLocalization();
        //SetupRandomLoadingHint();
        LoadWordVocabulary();
        StartCoroutine(LoadingSequence());
    }

    /// <summary>
    /// 初始化本地化系统
    /// </summary>
    private void InitializeLocalization()
    {
        MultilingualManager.Instance.LoadLocalization();
        // MultilingualManager.Instance.LoadLocalizationNameTable();
        // MultilingualManager.Instance.InitbiddenWords();
    }
    
    public async void LoadWordVocabulary()
    {
        Debug.Log("开始加载词库资源");
        await WordVocabularyManager.Instance.LoadEntriesAsync();
        Debug.Log("完成加载词库资源");
    }

    /// <summary>
    /// 设置随机加载提示
    /// </summary>
    private void SetupRandomLoadingHint()
    {
        int id=Random.Range(1,21);
        string sid = id < 10 ? "0" + id : id.ToString();
        loadingHintText.text =MultilingualManager.Instance.GetString("Haiku"+ sid);    
    }

    /// <summary>
    /// 主加载序列协程
    /// </summary>
    private IEnumerator LoadingSequence()
    {
        // 并行执行模拟加载和实际加载
        yield return StartCoroutine(SimulateLoadingProgress());
        yield return StartCoroutine(LoadEssentialResources());
        //AudioManager.Instance.Initialize();
        GameDataManager.instance.LoadPlayerProfile();
        sceneLoadOperation.allowSceneActivation = true;
    }

    /// <summary>
    /// 模拟加载进度（确保最小加载时间）
    /// </summary>
    private IEnumerator SimulateLoadingProgress()
    {
        Loading.GetComponent<CanvasGroup>().DOFade(1, 0.1f);
        
        float elapsedTime = 0;
        float progress = 0;

        while (progress < 1f)
        {
            elapsedTime = Time.time - loadStartTime;
            progress = Mathf.Clamp01(elapsedTime / 5f);
            UpdateProgressDisplay(progress);
            yield return null;
        }
        Loading.GetComponent<CanvasGroup>().DOFade(0, 0.1f);
    }

    /// <summary>
    /// 加载核心游戏资源
    /// </summary>
    private IEnumerator LoadEssentialResources()
    {
        Debug.Log("开始预加载游戏资源");

        yield return AdvancedBundleLoader.SharedInstance.LoadAtlas(
           "ui_universal",
           "UI_Universal");

        //LoadFont();
        // 加载字体资源
        Font mainFont = AdvancedBundleLoader.SharedInstance.LoadFont(
             "stagefonts",
             "FZKTK");
        //loadingHintText.font = mainFont;

        // 并行加载其他关键资源
        yield return AdvancedBundleLoader.SharedInstance.LoadAtlas(
            "effect_sprite",
            "trailAltas");

        yield return AdvancedBundleLoader.SharedInstance.LoadMaterialResource(
            "effectsitemmats",
            "Circle");       
        
        //预加载关卡文件
        StageController.Instance.LoadPackInfos();

        // 开始场景加载
        yield return LoadMainSceneAsync();
    }

    private void LoadFont()
    {
        // 加载TMP字体资源
        // TMP_FontAsset tmpFont = AdvancedBundleLoader.SharedInstance.LoadTMPFont(
        //     "stagefonts",
        //     "FZKTKSDF"); // 注意资源名称包含"SDF"后缀
        //
        // TMP_FontAsset selecttmpFont =  AdvancedBundleLoader.SharedInstance.LoadTMPFont(
        //     "stagefonts",
        //     "FZKTKSDF_select"); // 注意资源名称包含"SDF"后缀

        // if (tmpFont != null)
        // {
        //    
        //     Shader shaderLoad = Resources.Load<Shader>("TMP_SDF Overlay");
        //
        //     if (shaderLoad == null)
        //     {
        //         Debug.LogError("Shader加载失败");
        //     }
        //     tmpFont.material.shader = shaderLoad;
        //     selecttmpFont.material.shader = shaderLoad;
        // }
        // else
        // {
        //     Debug.LogError("TMP字体资源加载失败");
        // }
    }

    /// <summary>
    /// 异步加载主场景
    /// </summary>
    private IEnumerator LoadMainSceneAsync()
    {
        sceneLoadOperation = SceneManager.LoadSceneAsync("GameLobby");
        sceneLoadOperation.allowSceneActivation = false;

        Debug.Log("开始加载主场景");
        yield return new WaitUntil(() => sceneLoadOperation.progress >= 0.9f);
        Debug.Log("主场景加载完成");
    }

    /// <summary>
    /// 更新进度显示
    /// </summary>
    private void UpdateProgressDisplay(float progress)
    {
        // 平滑更新进度条
        progressSlider.DOValue(progress, 0.1f);

        // 更新进度指示器位置
        // Vector2 sliderSize = progressSlider.GetComponent<RectTransform>().sizeDelta;
        // float xPos = progress * sliderSize.x - (sliderSize.x / 2);
        // indicatorIcon.anchoredPosition = new Vector2(xPos, 0);

        // 更新百分比文本
        // loadingHintText.text = $"{Mathf.FloorToInt(progress * 100)}%";
    }
}
