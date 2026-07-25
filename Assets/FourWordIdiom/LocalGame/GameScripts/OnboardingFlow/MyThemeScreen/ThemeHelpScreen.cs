using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;


public class ThemeHelpScreen : UIWindow
{        
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Text wordtips;
    [SerializeField] private Text HeaderText;
   
    [SerializeField] private Text goldTicket; 
    [SerializeField] private Text rewardtips;
   
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
        //EventDispatcher.instance.TriggerUpdateLayerCoin(false,true);
    }

    private void InitUI()
    {
        string titleName= MultilingualManager.Instance.GetString("MyTheme", "hudie");
        HeaderText.text = titleName;
        
        string wordtip= MultilingualManager.Instance.GetString("ConsecutiveIdiom", "hudie");
        wordtips.text = wordtip;
        
        string goldticket= MultilingualManager.Instance.GetString("GetTicket", "hudie");
        goldTicket.text = goldticket;
       
        string collect= MultilingualManager.Instance.GetString("CollectTheme", "hudie");
        rewardtips.text = collect;
        
        closetips.text = MultilingualManager.Instance.GetString("limitedRewardsDes05");
    }
   
    protected override void InitializeUIComponents()
    {
        closeBtn.AddVibraClickAction(OnCloseBtn); // 绑定关闭按钮事件
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
        //EventDispatcher.instance.TriggerUpdateLayerCoin(true,true);
    }
}



