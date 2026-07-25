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
    [SerializeField] private Button hideBtn; // 关闭按钮
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
        LimitTimeManager.Instance.OnLimitTimeUpdated += UpdateTimeDisplay; // 订阅事件
        InitLightItems();
        StartCoroutine(InitUI());
        slider.transform.parent.gameObject.SetActive(!LimitTimeManager.Instance.IsComplete());
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
        
        EventDispatcher.instance.TriggerUpdateLayerCoin(true,false,true);
    }

    IEnumerator InitUI()
    {
        int wordcount = LimitTimeManager.Instance.GetCurWordCount();
        limitData = LimitTimeManager.Instance.CurlimitData;

        if (limitData == null) yield break;        

        //进入游戏后首次开启界面
        if (firstenter||wordcount <= 0)
        {
            txtprogress.text = "0/" + limitData.num;
            slider.value = 0;
            txttips.text = string.Format(MultilingualManager.Instance.GetString("limitedRewardsDes06"), limitData.num - wordcount);
        }
        
        // if (GameDataManager.MainInstance.UserData.isNeedShowHelp)
        //     closeBtn.enabled = false;
        
        UpdateMinTimeDisplay();
        yield return new WaitForSeconds(1.2f);
        
        UpdateProgress();
        
        yield return new WaitForSeconds(0.5f);
        
        if (GameCoreManager.Instance.IsTrueAuto)
        {
            OnCloseBtn();
        }
    }

    private void InitLightItems()
    {
        List<LimitDataItem> items = LimitTimeManager.Instance.GetLimitItems();
        for (int i = 0; i < LightItems.Count; i++)
        {
            LimitDataItem tDataItem=items[i];
            LightItem lightItem = LightItems[i];
            lightItem.SetUI(tDataItem);
        }

        if (GameDataManager.Instance.UserData.timerePuzzleid > 10)
        {
            LimitDataItem tDataItem=items[11];
            LightItem lightItem = LightItems[10];
            lightItem.SetUI(tDataItem);
        }
    }

    private void UpdateProgress(bool isreset=false)
    {
        if (LimitTimeManager.Instance.IsComplete()) return;
        int wordcount = LimitTimeManager.Instance.GetCurWordCount();
        limitData = LimitTimeManager.Instance.CurlimitData;
        float durtime = wordcount==0?0.1f:0.5f;
        if(isreset) slider.value = 0;
        txttips.text = string.Format(MultilingualManager.Instance.GetString("limitedRewardsDes06"), limitData.num- wordcount);
        
        float progress = (float)wordcount/limitData.num;
        
        slider.DOValue(progress,durtime).OnComplete(() =>
        {
            //重新获取一下当前限时奖励id对应的限时任务；（避免重复叠加数值）
            wordcount = LimitTimeManager.Instance.GetCurWordCount();
            if (wordcount >= limitData.num)
            {
                
                if (GameDataManager.Instance.UserData.timerePuzzleid >= LightItems.Count)
                {
                    int index = LightItems.Count-1;
                    LightItems[index].UpdateRewardValue();
                }
                else
                {
                    LightItems[GameDataManager.Instance.UserData.timerePuzzleid].UpdateRewardValue();
                }
                slider.transform.DOScaleZ(1, 0.2f).OnComplete(() =>
                {
                    UpdateProgress(true);
                });
                GameDataManager.Instance.UserData.UpdateLImitid();
                LimitTimeManager.Instance.ClaimCurrentReward();
                DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedLightLimit,1);
            }
          
            txtprogress.text = wordcount + "/" + limitData.num;
        });

        UpdateMinTimeDisplay();
    }

    private void QuickComplete()
    {
        if (LimitTimeManager.Instance.IsComplete()) return;
        
        int wordcount = LimitTimeManager.Instance.GetCurWordCount();
        limitData = LimitTimeManager.Instance.CurlimitData;
        // slider.DOValue((float)wordcount / limitData.num, 0);
        if (limitData == null) return;
        
        if (wordcount >= limitData.num)
        {
            if (GameDataManager.Instance.UserData.timerePuzzleid >= LightItems.Count)
            {
                int index = LightItems.Count-1;
                LightItems[index].UpdateRewardValue();
            }
            else
            {
                LightItems[GameDataManager.Instance.UserData.timerePuzzleid].UpdateRewardValue();
            }
           
            GameDataManager.Instance.UserData.UpdateLImitid();
            DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedLightLimit,1);
            
            QuickComplete();
        }
        //txtprogress.text = wordcount + "/" + limitData.num;
    }

    private void UpdateMinTimeDisplay()
    {
        bool canshow = LimitTimeManager.Instance.LimitTimeCanShow();
      
        if (canshow&&!LimitTimeManager.Instance.IsComplete())
        {
            minTimeObj.gameObject.SetActive(true);
            int min = LimitTimeManager.Instance.GetLimitWordMinTime();
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
        hideBtn.onClick.AddListener(OnCloseBtn); // 绑定关闭按钮事件
        closeBtn.onClick.AddListener(OnCloseBtn); // 绑定关闭按钮事件
        helpBtn.AddClickAction(OnHelpBtn);
    }
    
    private void OnHelpBtn()
    {
        SystemManager.Instance.ShowPanel(PanelType.LimitHelpScreen);
    }

    private void OnCloseBtn()
    {
        AudioManager.Instance.TriggerVibration(10, 200);
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
        base.OnDisable();
        LimitTimeManager.Instance.UpdateLimitTimeBtnUI();
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,true,false);

        LimitTimeManager.Instance.OnLimitTimeUpdated -= UpdateTimeDisplay; // 订阅事件
    }
}



