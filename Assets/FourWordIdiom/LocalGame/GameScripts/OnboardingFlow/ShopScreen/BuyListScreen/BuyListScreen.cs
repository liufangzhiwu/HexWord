using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuyListScreen : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    
    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
    }

    protected override void InitializeUIComponents()
    {
        base.InitializeUIComponents();
        closeBtn.AddClickAction(Close);
    }

    private void InitUI()
    {
        
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
