using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class CompetitionStart : UIWindow
{        
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Button startBtn; // 关闭按钮
    [SerializeField] private Text wordtips;
    [SerializeField] private Image titleImage;


    protected void Start()
    {
        // switch (GameDataManager.MainInstance.UserData.LanguageCode)
        // {
        //     case "Japanese":
        //         titleImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("dashJantitle");
        //         break;  
        //     case "ChineseTraditional":
        //         titleImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fanDashTitle");
        //         break;
        // }
        InitButton();
    }
    
    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
    }

    private void InitUI()
    {
        wordtips.text = MultilingualManager.Instance.GetString("CarpMatchStartDes");
        startBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("CarpMatchStart");
    }
   
    protected void InitButton()
    {
        closeBtn.AddClickAction(OnCloseBtn); // 绑定关闭按钮事件
        startBtn.AddClickAction(ClickStartBtn); // 绑定关闭按钮事件
    }

    private void ClickStartBtn()
    {
        //GameDataManager.MainInstance.FishUserSave.OpenRoundTime();
        SystemManager.Instance.ShowPanel(PanelType.DashCompetition);
        
        //FishInfoController.Instance.UpdateFishTime();
       // DateTime dateTime = DateTime.Today;// 将字符串转换为 DateTime
        
        //if(GameDataManager.MainInstance.FishUserSave.matchCount<=0)
            //ThinkManager.instance.Event_ActivityBegin("竞速活动");
        //GameDataManager.MainInstance.UserData.EveryDayOpenLimit();
        
        OnCloseBtn();
    }
    
    private void OnCloseBtn()
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



