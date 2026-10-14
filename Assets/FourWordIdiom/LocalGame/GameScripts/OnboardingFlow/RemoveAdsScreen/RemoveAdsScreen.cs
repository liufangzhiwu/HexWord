using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RemoveAdsScreen : UIWindow
{
    [SerializeField] private Button ClaimBtn; // 按钮
    [SerializeField] private Text title; // 标题文本显示
    [SerializeField] private Text timeText; // 时间文本显示
   
    
    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
    }

    private void InitUI()
    {
        ClaimBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ADPopReceive");
        //title.text = MultilingualManager.Instance.GetString("FirstWin");
        //tips.text = MultilingualManager.Instance.GetString("FirstWinRewards");


        //InitAwardUI();
    }
    
    
    private void InitAwardUI()
    {

    
       
    }
    
    protected override void InitializeUIComponents()
    {
        ClaimBtn.AddVibraClickAction(ClickClaimBtn); // 绑定关闭按钮事件
    }

    private void ClickClaimBtn()
    {
        Close();
    }

    
    private void Close()
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
