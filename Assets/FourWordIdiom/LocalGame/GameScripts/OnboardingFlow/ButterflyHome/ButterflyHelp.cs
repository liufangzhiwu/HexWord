using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class ButterflyHelp : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Text wordtips;
    [SerializeField] private Image titleImage; 
    [SerializeField] private Text slidertips; 
    [SerializeField] private Text rewardtips; 
    [SerializeField] private Text closetips; 
    
    // Start is called before the first frame update
    void Start()
    {
        // titleImage.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromBundle(ToolUtil.GetLanguageBundle(),"ui_garden_title");
        closeBtn.AddClickAction(OnCloseBtn); // 绑定关闭按钮事件
    
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
    }
    
    private void InitUI()
    {
        wordtips.text = MultilingualManager.Instance.GetString("ButterflyUI05", "hudie");
        slidertips.text = MultilingualManager.Instance.GetString("ButterflyUI06", "hudie");
        rewardtips.text = MultilingualManager.Instance.GetString("ButterflyUI07", "hudie");
        // //mintips.text = MultilingualManager.Instance.GetString("limitedRewardsDes04");
        closetips.text = MultilingualManager.Instance.GetString("ButterflyUI05", "hudie");
    }
    
    private void OnCloseBtn()
    {
        base.Close(); // 隐藏面板
    }
}
