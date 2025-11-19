using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;


public class LimitHelpScreen : UIWindow
{        
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Text wordtips;
    [SerializeField] private Image titleImage; 
    [SerializeField] private Image fantitleImage; 
    [SerializeField] private Text slidertips; 
    [SerializeField] private Text rewardtips; 
    [SerializeField] private Text mintips; 
    [SerializeField] private Text closetips;

    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        InitUI();
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

    private void InitUI()
    {
        wordtips.text = MultilingualManager.Instance.GetString("limitedRewardsDes01");
        slidertips.text = MultilingualManager.Instance.GetString("limitedRewardsDes02");
        rewardtips.text = MultilingualManager.Instance.GetString("limitedRewardsDes03");
        mintips.text = MultilingualManager.Instance.GetString("limitedRewardsDes04");
        closetips.text = MultilingualManager.Instance.GetString("limitedRewardsDes05");
    }
   
    protected override void InitializeUIComponents()
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



