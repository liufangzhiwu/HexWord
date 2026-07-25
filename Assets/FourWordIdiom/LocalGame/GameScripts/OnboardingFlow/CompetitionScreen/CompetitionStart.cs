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
        int round= GameDataManager.Instance.FishUserSave.curround;
        FishaiInfoItem infoItem = FishInfoController.Instance.GetCurrFishItem(round);
        wordtips.text = string.Format(MultilingualManager.Instance.GetString("CarpMatchStartDes"), infoItem?.wordCount ?? 100 );
        startBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("CarpMatchStart");
    }
   
    protected void InitButton()
    {
        closeBtn.AddVibraClickAction(OnCloseBtn); // 绑定关闭按钮事件
        startBtn.AddVibraClickAction(ClickStartBtn); // 绑定关闭按钮事件
    }

    private void ClickStartBtn()
    {
        GameDataManager.Instance.FishUserSave.OpenRoundTime();
        SystemManager.Instance.ShowPanel(PanelType.DashCompetition);
        
        AnalyticMgr.ActivityBegin("竞速活动");
        
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



