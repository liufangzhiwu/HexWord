using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;


public class RateUsScreen : UIWindow
{
    [SerializeField] private Button opinionBtn; // 关闭按钮
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Button nextBtn; // 关闭按钮
    [SerializeField] private Toggle[] starToggles; // 震动开关
    [SerializeField] private Text des_Text;
    private int clickindex;
    

    protected override void OnEnable()
    {
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        des_Text.text = MultilingualManager.Instance.GetString("EvaluateDes");
        nextBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("EvaluateButton01");
        opinionBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("EvaluateButton03");
        closeBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("EvaluateButton02");
        clickindex = -1;
        GameDataManager.Instance.UserData.showRateusCount++;
        InitToggles();
        ShowBtnsStatic(true);
        
        AnalyticMgr.PopShow("评价");
    }

    private void InitToggles()
    {
        for (int i = 0; i < starToggles.Length; i++)
        {
            Toggle gToggle = starToggles[i];
            int index = i;
            gToggle.onValueChanged.AddListener((bool ison) =>
            {
                if(clickindex == -1)
                    clickindex = index;                   
                OnToggleValueChanged(ison,index);
            });
        }
    }
    
    private void OnToggleValueChanged(bool ison,int index)
    {
        if (index == clickindex)
        {
            if (clickindex == 4)
            {
                OnRateusBtn();                    
            }
            else
            {
                ShowBtnsStatic(false);
            }
            EnableToggle();
        }
        
        for (int i = index-1; i >=0; i--)
        {
            clickindex = i;
            Toggle gToggle = starToggles[i];
            gToggle.isOn = ison;
        }
    }

    private void EnableToggle()
    {
        for (int i = 0; i < starToggles.Length; i++)
        {
            Toggle gToggle = starToggles[i];
            gToggle.enabled = false;
        }
    }

    private void ShowBtnsStatic(bool isshownext)
    {
        closeBtn.gameObject.SetActive(true);
        opinionBtn.gameObject.SetActive(!isshownext);
        nextBtn.gameObject.SetActive(isshownext);
    }

    protected override void InitializeUIComponents()
    {      
        nextBtn.AddClickAction(OnNextBtn); // 绑定关闭按钮事件
        opinionBtn.AddClickAction(OnOpinionBtn); // 绑定关闭按钮事件
        closeBtn.AddClickAction(OnCloseBtn); // 绑定关闭按钮事件
    }
    
    private void OnOpinionBtn()
    {
       GameDataManager.Instance.UserData.showRateusCount = 3;
       Application.OpenURL(ConfigManager.Instance.GetString("OpinionUrl"));
    }
    
    private void OnRateusBtn()
    {
        GameDataManager.Instance.UserData.showRateusCount = 3;

#if UNITY_ANDROID
        // 检测设备制造商
        AndroidJavaClass build = new AndroidJavaClass("android.os.Build");
        string manufacturer = build.GetStatic<string>("MANUFACTURER");
        if (manufacturer.Equals("Xiaomi", StringComparison.OrdinalIgnoreCase))
        {
            // 小米设备 → 跳转小米应用商店
            OpenXiaomiStoreWithIntent();
            OnClosePanel();
            AnalyticMgr.PopAccept("评价");
            return; // 直接结束，不再执行后续 Application.OpenURL
        }
        else
        {
            // 非小米 Android（例如华为）
            string url = "https://appgallery.huawei.com/#/app/C116093983";
            Application.OpenURL(url);
        }
#elif UNITY_IOS
        string appId = "6764502146";
        string url = $"itms-apps://itunes.apple.com/app/id{appId}?action=write-review";
        Application.OpenURL(url);
#elif UNITY_OPENHARMONY 
        string url = $"https://appgallery.huawei.com/app/detail?id={Application.identifier}";
        Application.OpenURL(url);
#endif

        OnClosePanel();
        AnalyticMgr.PopAccept("评价");
    }
    
    /// <summary>
    /// 使用 Android Intent 直接打开小米应用商店详情页
    /// </summary>
    private void OpenXiaomiStoreWithIntent()
    {
#if UNITY_ANDROID
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        string packageName = Application.identifier; // 当前应用的包名
        string url = $"mimarket://details?id={packageName}";

        AndroidJavaObject intent = new AndroidJavaObject(
            "android.content.Intent",
            new AndroidJavaObject("android.net.Uri").CallStatic<AndroidJavaObject>("parse", url)
        );
        // 指定由小米应用商店（GetApps）处理
        intent.Call<AndroidJavaObject>("setPackage", "com.xiaomi.market");

        currentActivity.Call("startActivity", intent);
#else
        Debug.Log("非 Android 平台，无法使用小米商店 Intent");
#endif
    }
    
    private void OnNextBtn()
    {
        GameDataManager.Instance.UserData.showRateusTime=DateTime.Now.ToString();
        OnClosePanel();
        AnalyticMgr.PopRefuse("评价");
    }
    
    private void OnCloseBtn()
    {
        GameDataManager.Instance.UserData.showRateusCount = 3;
        OnClosePanel();
        AnalyticMgr.PopRefuse("评价");
    }
    
    private void OnClosePanel()
    {
        base.Close(); // 隐藏面板
    }
    
    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
    }
}
