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
        // if (string.IsNullOrEmpty(GameDataManager.instance.FishUserSave.roundstarttime)) 
        //     GameDataManager.instance.FishUserSave.roundstarttime = DateTime.Now.ToString();
        
        // TimeSpan ts = DateTime.Now.Subtract(DateTime.Parse(GameDataManager.instance.FishUserSave.roundstarttime));
        // //int progress = GameDataManager.instance.FishUserSave.matchCount;
        // ThinkManager.instance.Event_ActivityComplete("竞速活动",(int)ts.TotalSeconds);
        // //FirebaseManager.Instance.ActivityProgress("竞速活动",progress,(int)ts.TotalSeconds);
        //
        // GameDataManager.instance.FishUserSave.UpdateRound(-1);
        // GameDataManager.instance.FishUserSave.ResetFishData();
        //FishInfoController.Instance.FishMatchOver();
        //UIManager.Instance.HidePanel(PanelName.DashCompetition);
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



