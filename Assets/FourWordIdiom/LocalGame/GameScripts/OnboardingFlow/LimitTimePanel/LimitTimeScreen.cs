using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class LimitTimeScreen : UIWindow
{        
    [SerializeField] private GameObject minTimeObj; // 关闭按钮
    [SerializeField] private Button helpBtn; // 关闭按钮
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Slider slider; 
    [SerializeField] private Image titleImage; 
    [SerializeField] private Image fantitleImage;
    [SerializeField] private Text txttips;
    [SerializeField] private Text txtmintime; 
    [SerializeField] private Text txttime; 
    [SerializeField] private Text txtprogress;
    
    //当前限时奖励物品数据
    LimitDataItem limitData;
    public List<LightItem> LightItems;
    private bool firstenter =true;    
  
    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        //EventDispatcher.instance.TriggerUpdateLayerCoin(true,true);
        CheckLimtEvent();
        LimitTimeManager.instance.OnLimitTimeUpdated += UpdateTimeDisplay; // 订阅事件
        InitLightItems();
        StartCoroutine(InitUI());

        // if (SaveSystem.Instance.UserData.LanguageCode == "ChineseTraditional")
        // {
        //     fantitleImage.gameObject.SetActive(true);
        //     titleImage.gameObject.SetActive(false);
        // }
        // else
        // {
        //     fantitleImage.gameObject.SetActive(false);
        //     titleImage.gameObject.SetActive(true);
        // }           
    }

    private void CheckLimtEvent()
    {
        if (GameDataManager.instance.UserData.isDayEnterLimint)
        {      
            //ThinkManager.instance.Event_ActivityBegin("限时活动");
            //FirebaseManager.Instance.ActivityBegin("限时活动", DateTime.Today.ToString());
            GameDataManager.instance.UserData.EveryDayOpenLimit();
        }
    }

    IEnumerator InitUI()
    {
        int wordcount = LimitTimeManager.instance.GetCurWordCount();
        limitData = LimitTimeManager.instance.CurlimitData;

        if (limitData == null) yield break;        

        //进入游戏后首次开启界面
        if (firstenter||wordcount <= 0)
        {
            txtprogress.text = "0/" + limitData.num;
            slider.value = 0;
            txttips.text = string.Format(MultilingualManager.Instance.GetString("limitedRewardsDes06"), limitData.num - wordcount);
        }
        
        // if (GameDataManager.instance.UserData.isNeedShowHelp)
        //     closeBtn.enabled = false;
        
        UpdateMinTimeDisplay();
        yield return new WaitForSeconds(1.2f);
        UpdateProgress();
    }

    private void InitLightItems()
    {
        List<LimitDataItem> items = LimitTimeManager.instance.GetLimitItems();
        for (int i = 0; i < LightItems.Count; i++)
        {
            LimitDataItem tDataItem=items[i];
            LightItem lightItem = LightItems[i];
            lightItem.SetUI(tDataItem);
        }
    }

    private void UpdateProgress(bool isreset=false)
    {
        int wordcount = LimitTimeManager.instance.GetCurWordCount();
        limitData = LimitTimeManager.instance.CurlimitData;
        float durtime = wordcount==0?0.1f:0.5f;
        if(isreset) slider.value = 0;
        txttips.text = string.Format(MultilingualManager.Instance.GetString("limitedRewardsDes06"), limitData.num- wordcount);
        
        float progress = (float)wordcount/limitData.num;
        
        slider.DOValue(progress,durtime).OnComplete(() =>
        {
            if (wordcount >= limitData.num)
            {
                GameDataManager.instance.UserData.UpdateLImitid();
                DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedLightLimit,1);
            }
            
            slider.DOValue(progress, 0.35f).OnComplete(() =>
            {
                if (wordcount >= limitData.num)
                {
                    LightItems[GameDataManager.instance.UserData.timerePuzzleid-1].ShowReward(true,() =>
                    {
                        slider.transform.DOScaleZ(1, 0.2f).OnComplete(() =>
                        {
                            UpdateProgress(true);
                        });
                        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedLightLimit,1);
                    });

                    slider.transform.DOScaleZ(1, 0.3f).OnComplete(() =>
                    {
                        if (GameDataManager.instance.UserData.isNeedShowHelp)
                        {
                            SystemManager.Instance.ShowPanel(PanelType.LimitHelpScreen);
                            GameDataManager.instance.UserData.isNeedShowHelp = false;
                        }
                        //closeBtn.enabled = true;
                    });
                }
                else
                {
                    //closeBtn.enabled = true;
                }
            });
            txtprogress.text = wordcount + "/" + limitData.num;
        });

        UpdateMinTimeDisplay();
    }

    private void QuickComplete()
    {
        int wordcount = LimitTimeManager.instance.GetCurWordCount();
        limitData = LimitTimeManager.instance.CurlimitData;
        // slider.DOValue((float)wordcount / limitData.num, 0);
        
        if (wordcount >= limitData.num)
        {
            GameDataManager.instance.UserData.UpdateLImitid();
            DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedLightLimit,1);
        }
         
        if (wordcount >= limitData.num)
        {
            LightItems[GameDataManager.instance.UserData.timerePuzzleid-1].UpdateRewardValue();
            DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedLightLimit,1);
            
            QuickComplete();
        }
        //txtprogress.text = wordcount + "/" + limitData.num;
    }

    private void UpdateMinTimeDisplay()
    {
        bool canshow = LimitTimeManager.instance.LimitTimeCanShow();
      
        if (canshow&&!LimitTimeManager.instance.IsComplete())
        {
            minTimeObj.gameObject.SetActive(true);
            int min = LimitTimeManager.instance.GetLimitWordMinTime();
            txtmintime.text = $"<size=48>x2</size>\n{min}<size=30>分钟</size>";
        }
        else
        {
            minTimeObj.gameObject.SetActive(false);
        }
    }
    
    private void UpdateTimeDisplay(string time)
    {
        if (!string.IsNullOrEmpty(time))
        {
            txttime.text = time; // 更新文本
        }
    }
    
    protected override void InitializeUIComponents()
    {
        closeBtn.AddClickAction(OnCloseBtn); // 绑定关闭按钮事件
        helpBtn.AddClickAction(OnHelpBtn);
    }
    
    private void OnHelpBtn()
    {
        SystemManager.Instance.ShowPanel(PanelType.LimitHelpScreen);
    }

    private void OnCloseBtn()
    {
        QuickComplete();
        base.Close(); // 隐藏面板
        firstenter=false;
    }
    
    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
         LimitTimeManager.instance.UpdateLimitTimeBtnUI();
        //EventDispatcher.instance.TriggerUpdateLayerCoin(false, true);
        LimitTimeManager.instance.OnLimitTimeUpdated -= UpdateTimeDisplay; // 订阅事件
        base.OnDisable();
       
    }
}



