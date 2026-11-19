using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class AdsAwardScreen : UIWindow, IBackGestureClosePanel
{
    [Header("UI Elements")]
    [SerializeField] private Image bImage;
    [SerializeField] private GameObject backUI;
    [SerializeField] private GameObject windowsUI;
    
    [SerializeField] private Button agreeButton;
    [SerializeField] private Text goldValueText;
    [SerializeField] private Text tipText;
    [SerializeField] private Button closeButton;
    
    [SerializeField] private Text _wgoldValueText;

    private int _goldAmount;
    private string _tipContent;
    private Action _onClaimed;
    private bool _hasContent;
    private bool _firstAds;
    private bool _closing;

    private void Start()
    {
        // 关闭按钮和下方按钮走同一个回调：都会获得金币并关闭弹窗
        agreeButton.AddClickAction(OnCloseClicked);
        closeButton.AddVibraClickAction(OnCloseClicked);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        _closing = false;   // 面板复用：每次打开复位关闭标记
        ApplyContent();
    }

    /// <summary>
    /// 由 AdRuleManager 调用，注入本次弹窗内容
    /// </summary>
    public void SetContent(bool firstAds,int gold, string tip, Action onClaimed)
    {
        _firstAds = firstAds;
        _goldAmount = gold;
        _tipContent = tip;
        _onClaimed = onClaimed;
        _hasContent = true;
        ApplyContent();
    }

    private void ApplyContent()
    {
        
        if (!SystemManager.Instance.PanelIsShowing(PanelType.HeaderSection))
        {
            SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        }
        
        EventDispatcher.instance.TriggerUpdateLayerCoin(true,true,false,true);
        
        if (!_hasContent) return;
        if (goldValueText != null) goldValueText.text = _goldAmount.ToString();
        if (_wgoldValueText != null) _wgoldValueText.text = _goldAmount.ToString();
        if (tipText != null) tipText.text = _tipContent;
        
        bImage.gameObject.SetActive(_firstAds);
        backUI.gameObject.SetActive(_firstAds);
        
        windowsUI.gameObject.SetActive(!_firstAds);

        if (!_firstAds)
        {
            StartCoroutine(AutoHide());
        }
        
        StartCoroutine(ShowGoldTable());
    }

    IEnumerator ShowGoldTable()
    {
        yield return new WaitForSeconds(0.01f);
        
        
        _onClaimed?.Invoke();
    }

    IEnumerator AutoHide()
    {
        yield return new WaitForSeconds(1.8f);
        OnCloseClicked();
    }

    /// <summary>
    /// 侧滑 = 「领取/关闭」按钮（两者本来就共用 OnCloseClicked）。
    ///
    /// 直接 HidePanel 会跳过金币飞入与 HeaderSection 的隐藏，留下一个常驻的顶栏，
    /// 并且下一次金币弹窗会叠在它上面，所以统一走按钮逻辑。
    /// </summary>
    public void CloseFromBackGesture()
    {
        OnCloseClicked();
    }

    /// <summary>
    /// 关闭和下方按钮共用：发放金币 + 关闭
    /// </summary>
    private void OnCloseClicked()
    {
        if (_closing) return;   // 侧滑与 AutoHide 协程可能同时到，防重复飞入/重复关面板
        _closing = true;

        //var cb = _onClaimed;
        _onClaimed = null;  // 防止重复触发
        //cb?.Invoke();
        Close();
        
        CustomFlyInManager.Instance.FlyInGold(windowsUI.transform ,() =>
        {
            EventDispatcher.instance.TriggerChangeGoldUI(0,true);
            
            SystemManager.Instance.HidePanel(PanelType.HeaderSection);
        });
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        _onClaimed = null;
        _hasContent = false;
    }
}