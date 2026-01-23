using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class Launch : MonoBehaviour
{
    [SerializeField] private Button _ageTip;

    private float timer = 0f;
    public bool isTiming = false;

    private async void Awake()
    {
        await AssetBundleLoader.SharedInstance.PreloadSingleBundle("gameinfo");
    }

    // Start is called before the first frame update
    private IEnumerator Start()
    {
        Debug.Log("游戏启动了...");
        transform.parent.GetChild(transform.GetSiblingIndex() + 1).gameObject.SetActive(false);
        _ageTip.AddClickAction(OnAgeTipClick);
    
        // 1. 初始化数据
        // GameDataManager.Instance.Init();
    
        // 2. 隐藏进度条/Loading图（？）
        if (transform.parent.childCount > transform.GetSiblingIndex() + 1)
        {
            transform.parent.GetChild(transform.GetSiblingIndex() + 1).gameObject.SetActive(false);
        }
    
        _ageTip.AddClickAction(OnAgeTipClick);
        isTiming = true;
        
        yield return new WaitUntil(() => AssetBundleLoader.SharedInstance.IsManifestLoaded);
        // 3. 🔥 并行下载（速度更快）
        try 
        {
            var task2 = AssetBundleLoader.SharedInstance.PreloadBundles(new string[]
            {
                 // 注意改成小写
                "stagefonts",   // 字体包
                "effect_sprite",
                "effectsitemmats",
                "objects",
                "musics",
            }.ToList());
            Task taskCommon = AssetBundleLoader.SharedInstance.PreloadBundles(new string[]
            {
                "ui_universal","commonitem","rootcanvas","onboardingflow","ui_mainbase","useritems", "butterfly_ui",
            }.ToList()); // 替换为真实的包名
            // 等待所有任务完成
            Task.WhenAll(task2, taskCommon);
        
            Debug.Log("所有核心资源（含字体）预加载完毕！");
        
            // 4. 在这里可以触发进入下一个流程
            // DoSomethingNext();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"资源加载炸了: {e.Message}");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!isTiming) return;
        timer += Time.deltaTime;
        if (timer >= 3f)
        {
            isTiming = false;
            OpenNextPage();
        }
    }

    public void OpenNextPage()
    {
        gameObject.SetActive(false);
        transform.parent.GetChild(transform.GetSiblingIndex() + 1).gameObject.SetActive(true);
    }
    
    private void OnAgeTipClick()
    {
        GameObject go = Resources.Load<GameObject>("Privacy/AgeWindow");
        GameObject aw = Instantiate(go, transform);
        aw.SetActive(true);
    }
}