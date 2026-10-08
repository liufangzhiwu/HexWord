using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class AdsAwardScreen : UIWindow
{
    [Header("UI Elements")]
    [SerializeField] private Button agreeButton;
    [SerializeField] private Text goldValueText;
    [SerializeField] private Text tipText;
    [SerializeField] private Button closeButton;

    private int _goldAmount;
    private string _tipContent;
    private Action _onClaimed;
    private bool _hasContent;

    private void Start()
    {
        // 关闭按钮和下方按钮走同一个回调：都会获得金币并关闭弹窗
        agreeButton.AddClickAction(OnCloseClicked);
        closeButton.AddVibraClickAction(OnCloseClicked);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ApplyContent();
    }

    /// <summary>
    /// 由 AdRuleManager 调用，注入本次弹窗内容
    /// </summary>
    public void SetContent(int gold, string tip, Action onClaimed)
    {
        _goldAmount = gold;
        _tipContent = tip;
        _onClaimed = onClaimed;
        _hasContent = true;
        ApplyContent();
    }

    private void ApplyContent()
    {
        if (!_hasContent) return;
        if (goldValueText != null) goldValueText.text = _goldAmount.ToString();
        if (tipText != null) tipText.text = _tipContent;
    }

    /// <summary>
    /// 关闭和下方按钮共用：发放金币 + 关闭
    /// </summary>
    private void OnCloseClicked()
    {
        var cb = _onClaimed;
        _onClaimed = null;  // 防止重复触发
        cb?.Invoke();
        Close();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        _onClaimed = null;
        _hasContent = false;
    }
}