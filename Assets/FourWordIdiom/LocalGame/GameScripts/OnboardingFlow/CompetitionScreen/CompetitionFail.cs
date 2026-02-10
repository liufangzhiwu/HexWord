using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class CompetitionFail : UIWindow
{        
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Text wordtips;
    [SerializeField] private Image titleImage; 
    
    protected void Start()
    {
        //switch (GameDataManager.MainIntance.UserData.LanguageCode)
        //{
        //    case "Japanese":
        //        titleImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("riMatchFail");
        //        break;  
        //    case "ChineseTraditional":
        //        titleImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fanMatchFail");
        //        break;
        //}
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
        int index=Random.Range(1, 6);
        wordtips.text = MultilingualManager.Instance.GetString("CarpMatchFailDes0"+index);
    }
   
    protected void InitButton()
    {
        closeBtn.AddClickAction(OnCloseBtn); // 绑定关闭按钮事件
    }
    
    private void OnCloseBtn()
    {
        if (string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.roundstarttime)) 
            GameDataManager.Instance.FishUserSave.roundstarttime = DateTime.Now.ToString();
        
        TimeSpan ts = DateTime.Now.Subtract(DateTime.Parse(GameDataManager.Instance.FishUserSave.roundstarttime));
        AnalyticMgr.ActivityComplete("竞速活动",(int)ts.TotalSeconds);
       
        GameDataManager.Instance.FishUserSave.UpdateRound(-1);
        GameDataManager.Instance.FishUserSave.ResetFishData();
        

        // if (GameCoreManager.Instance.PanelState == PanelState.MainMenuPanel)
        // {
        //     SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
        // }else if (GameCoreManager.Instance.PanelState == PanelState.FinishPanel)
        // {
        //     //SystemManager.Instance.ShowPanel(PanelType.StageFinishView);
        // }
        //
        // SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        
        // base.Close(); // 隐藏面板
        FishInfoController.Instance.FishMatchOver();
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



