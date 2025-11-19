using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CompetitionHelp : UIWindow
{        
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Text wordtips;
    [SerializeField] private Image titleImage; 
    [SerializeField] private Text slidertips; 
    [SerializeField] private Text rewardtips; 
    [SerializeField] private Text closetips; 
    
    protected void Start()
    {
        switch (GameDataManager.instance.UserData.LanguageCode)
        {
            case "JS":
                titleImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("dashJantitle");
                break;  
            case "CT":
                titleImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fanDashTitle");
                break;
        }

        InitButton();
    }
    
    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
    }

    private void InitUI()
    {
        wordtips.text = MultilingualManager.Instance.GetString("limitedRewardsDes01");
        slidertips.text = MultilingualManager.Instance.GetString("CarpMatchDes02");
        rewardtips.text = MultilingualManager.Instance.GetString("limitedRewardsDes03");
        //mintips.text = MultilingualManager.Instance.GetString("limitedRewardsDes04");
        closetips.text = MultilingualManager.Instance.GetString("limitedRewardsDes05");
    }
   
    protected void InitButton()
    {
        closeBtn.AddClickAction(OnCloseBtn); // 绑定关闭按钮事件
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



