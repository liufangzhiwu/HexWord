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
        //GameDataManager.instance.UserData.showRateusCount++;
        InitToggles();
        ShowBtnsStatic(true);
        
        //ThinkManager.instance.Event_PopShow("评价");
    }

    private void InitToggles()
    {
        for (int i = 0; i < starToggles.Length; i++)
        {
            Toggle gToggle = starToggles[i];
            int index = i;
            //gToggle.enabled = true;
            gToggle.onValueChanged.AddListener((bool ison) =>
            {
                if(clickindex == -1)
                    clickindex = index;                   
                OnToggleValueChanged(ison,index);
                //gToggle.enabled = false;
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
        //closeBtn.gameObject.SetActive(!isshownext);
        //opinionBtn.gameObject.SetActive(!isshownext);
        //nextBtn.gameObject.SetActive(isshownext);
    }

    protected override void InitializeUIComponents()
    {      
        nextBtn.AddClickAction(OnNextBtn); // 绑定关闭按钮事件
        opinionBtn.AddClickAction(OnOpinionBtn); // 绑定关闭按钮事件
        closeBtn.AddClickAction(OnCloseBtn); // 绑定关闭按钮事件
    }
    
    private void OnOpinionBtn()
    {
       // GameDataManager.instance.UserData.showRateusCount = 3;
        //Application.OpenURL(ConfigManager.Instance.GetString("OpinionUrl"));
    }
    
    private void OnRateusBtn()
    {
        //GameDataManager.instance.UserData.showRateusCount = 3;
        //string appId = ConfigManager.Instance.GetString("Appid"); // 替换为你的App ID
        string url = $"https://appgallery.huawei.com/app/detail?id={Application.identifier}";
        Application.OpenURL(url);
        OnClosePanel();
        //ThinkManager.instance.Event_PopAccept("评价");
    }
    
    private void OnNextBtn()
    {
        //GameDataManager.instance.UserData.showRateusTime=DateTime.Now.ToString();
        OnClosePanel();
        //ThinkManager.instance.Event_PopRefuse("评价");
    }
    
    private void OnCloseBtn()
    {
        //GameDataManager.instance.UserData.showRateusCount = 3;
        OnClosePanel();
        //ThinkManager.instance.Event_PopRefuse("评价");
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
