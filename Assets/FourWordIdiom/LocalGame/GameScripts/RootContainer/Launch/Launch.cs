using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class Launch : MonoBehaviour
{
    [SerializeField] private Button ageTip;

    private float _timer = 0f;
    public bool isTiming = false;

    public static Task ResourceLoadingTask { get; private set; }

    private List<string> activityBundles = new List<string> { "butterflyhome", "fishhomescreen", "shophomescreen" };

    public static Task FontTask { get; private set; }

    // Start is called before the first frame update
    private IEnumerator Start()
    {
        QualitySettings.asyncUploadTimeSlice = 4;
        QualitySettings.asyncUploadBufferSize = 16;
        yield return null;
        yield return null; // 保险起见等两帧
        Debug.Log("游戏启动了...");
        ShaderVariantCollection svc = Resources.Load<ShaderVariantCollection>("MyShaderVariants");
        if (svc != null)
        {
            // 2. 核心：启动预热！这会占用一点CPU时间，但能消除游戏中的卡顿
            // 建议在显示 Splash Logo 或者 Loading 进度条时执行
            svc.WarmUp(); 
            Debug.Log($"Shader预热完成，包含 {svc.variantCount} 个变体");
        }
        yield return null;
        isTiming = true;
        ResourceLoadingTask = null;

        if (transform.parent.childCount > transform.GetSiblingIndex() + 1)
        {
            // 隐藏loading页面
            transform.parent.GetChild(transform.GetSiblingIndex() + 1).gameObject.SetActive(false);
        }
        ageTip.AddClickAction(OnAgeTipClick);
        Task loadManifest = AssetBundleLoader.SharedInstance.LoadManifest();
        yield return new WaitUntil(() => loadManifest.IsCompleted);
        Task gameInfoTask = ConfigManager.Instance.CacheAllConfigs();
        FontTask = AssetBundleLoader.SharedInstance.PreloadSingleBundle("stagefonts");
        yield return new WaitUntil(() => gameInfoTask.IsCompleted);
        Debug.Log("配置文件和字体已经加载完成");
        List<Task> runningTasks = new List<Task>();
        List<string> gameplay = new List<string>
            { "rootcanvas", "gameplayarea", "effectsitemmats","useritems" };
        runningTasks.Add(AssetBundleLoader.SharedInstance.PreloadBundles(gameplay));
        yield return null;
        Debug.Log("开始加载场景bundle！");
        runningTasks.Add(AssetBundleLoader.SharedInstance.PreloadSingleBundle("scene_gamelobby"));
        yield return null;

        var common = new List<string> { "objects", "ui_universal", "commonitem", "onboardingflow", "musics"};
        foreach (var bundleName in common)
        {
            runningTasks.Add(AssetBundleLoader.SharedInstance.PreloadSingleBundle(bundleName));
            if (runningTasks.Count % 3 == 0) yield return null; // 喘口气
        }

        ResourceLoadingTask = Task.WhenAll(runningTasks);
    }

    // Update is called once per frame
    void Update()
    {
        if (!isTiming) return;
        _timer += Time.deltaTime;

        // 调试：如果超时 10秒 还没进，强制打印状态
        // if (_timer > 10f && isTiming)
        // {
        //     Debug.LogError($"启动卡死！Task状态: {(ResourceLoadingTask == null ? "NULL" : ResourceLoadingTask.Status.ToString())}");
        //     isTiming = false; // 停止计时防止刷屏
        // }

        if (_timer >= 3f && ResourceLoadingTask != null)
        {
            isTiming = false;
            OpenNextPage();
        }
    }

    public void OpenNextPage()
    {
        gameObject.SetActive(false);
        // 找到loading页面并展示
        transform.parent.GetChild(transform.GetSiblingIndex() + 1).gameObject.SetActive(true);
#if !UNITY_EDITOR
        DownloadActivityBundles();
#endif
    }

    private void OnAgeTipClick()
    {
        GameObject go = Resources.Load<GameObject>("Privacy/AgeWindow");
        GameObject aw = Instantiate(go, transform);
        aw.SetActive(true);
    }

    private async void DownloadActivityBundles()
    {
        // 使用 fire-and-forget 方式，不阻塞当前协程
        // 注意：这部分下载不会被计入 ResourceLoadingTask 的进度条
        // _ = Task.Run(async () => 
        {
            // 在Unity主线程外等待一秒 (Task.Delay)
            await Task.Delay(1000);

            foreach (var bundle in activityBundles)
            {
                await Task.Delay(200); // 间隔
                // 调用主线程无关的下载，或者确保DownloadToCacheOnly内部切回了主线程
                _ = AssetBundleLoader.SharedInstance.DownloadToCacheOnly(bundle);
            }
        }
        // });
    }
}