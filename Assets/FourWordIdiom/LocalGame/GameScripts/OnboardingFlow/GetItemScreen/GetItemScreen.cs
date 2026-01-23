using System;
using System.Collections;
using DG.Tweening;
using HuaweiService.CloudStorage;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GetItemScreen : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Text title; // 音效文本显示
    [SerializeField] private Text tips; // 语言选择文本显示
    [SerializeField] private Image AwardIcon; // 语言选择文本显示
    [SerializeField] private Image adsloading;
    [SerializeField] private Image adsIcon;
    [SerializeField] private Button ClaimGoldBtn;
    [SerializeField] private Button ClaimAdsBtn;
    [SerializeField] private GiftTable giftTable;
 
    public static LimitRewordType limitRewordType;

    private string eventDes;

    private ShopDataItem shopDataItem;
 
    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        //EventDispatcher.instance.TriggerUpdateLayerCoin(true,false);
        EventDispatcher.instance.TriggerUpdateLayerCoin(true,false);
    }

    private void InitUI()
    {
        //ClaimAdsBtn.GetComponentInChildren<Text>().text= MultilingualManager.Instance.GetString("ADPopWatch");
        ClaimAdsBtn.GetComponentInChildren<Text>().text= "免费";
        adsloading.gameObject.SetActive(false);
        adsIcon.gameObject.SetActive(true);
        //StartCoroutine(CheckIsReadyToShowAd());
        
        ShopDataItem shopDataItem=null;
        
        switch (limitRewordType)
        {
            case LimitRewordType.Coins:
                // if(max)
                //     AwardIcon.sprite= AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Coin2");
                //AwardIcon.sprite= AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Coin1");
                break;
            case LimitRewordType.Butterfly:
                AwardIcon.sprite= AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_Butterfly");
                break;
            case LimitRewordType.Tipstool:
                title.text = "放大镜";
                UpdateCliamBtn(false);
                ClaimGoldBtn.GetComponentInChildren<Text>().text=GameDataManager.Instance.UserData.toolInfo[102].cost.ToString();
                tips.text = "提示一个成语中的所有字";
                AwardIcon.sprite= AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("shop_tip");
                shopDataItem = ShopManager.shopManager.GetProduct("ItemBox01");
                break;
            case LimitRewordType.SingleTipsttool:
                UpdateCliamBtn(true);
                title.text = "提示灯";
                tips.text = "提示一个成语的首字";
                ClaimGoldBtn.GetComponentInChildren<Text>().text=GameDataManager.Instance.UserData.toolInfo[101].cost.ToString();
                AwardIcon.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("shop_reset");
                shopDataItem = ShopManager.shopManager.GetProduct("ItemBox02");
                break;
            // case LimitRewordType.Min5Double:
            //     return AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Mintool");
            // case LimitRewordType.Min15Double:
            //     return AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Mintool");
        }
        eventDes=title.text+"弹窗广告";
        giftTable.InitUI(limitRewordType,shopDataItem);
        AnalyticMgr.VideoAdShow(eventDes);
    }

    protected override void InitializeUIComponents()
    {
        closeBtn.AddClickAction(Close); // 绑定关闭按钮事件
        ClaimAdsBtn.AddClickAction(ClickClaimBtn);
        ClaimGoldBtn.AddClickAction(ClickClaimGoldBtn);
    }

    private void ClickClaimBtn()
    {
        if (UIUtilities.isEditMode)
        {
            UpdateAdsRewardUI(true);
        }
        else
        {
            AnalyticMgr.VideoAdClick(eventDes);
            Game.self.Ads.ShowReward(Define.AdKey.RewardAdIdStoreGold,UpdateAdsRewardUI);
        }
    }

    private void ClickClaimGoldBtn()
    {
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[101];
        if (limitRewordType == LimitRewordType.SingleTipsttool)
        {
            toolInfo = GameDataManager.Instance.UserData.toolInfo[101];
        }else if (limitRewordType == LimitRewordType.Tipstool)
        {
            toolInfo = GameDataManager.Instance.UserData.toolInfo[102];
        }
        
       
        if (toolInfo.cost > GameDataManager.Instance.UserData.Gold)
        {        
            MessageSystem.Instance.ShowTip("TipGoldInsufficient");
            return;
        }
        
        GameDataManager.Instance.UserData.UpdateTool(limitRewordType, 1,"金币购买道具");
        GameDataManager.Instance.UserData.UpdateGold(-toolInfo.cost,false,true,"金币购买道具");
        
        
        if (limitRewordType == LimitRewordType.SingleTipsttool)
        {
            SystemManager.Instance.GetPanel(PanelType.HexGamePlayArea)?.GetComponent<HexGamePlayArea>()
                ?.ToolItemFirstLetter();
        }else if (limitRewordType == LimitRewordType.Tipstool)
        {
            SystemManager.Instance.GetPanel(PanelType.HexGamePlayArea)?.GetComponent<HexGamePlayArea>()
                ?.UseTips();
        }
        
       
        Close();
    }

    private void UpdateAdsRewardUI(bool isShow)
    {
        if (isShow)
        {
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.SingleTipsttool, 1,"看广告获取"+title.text+"道具");
            SystemManager.Instance.GetPanel(PanelType.HexGamePlayArea)?.GetComponent<HexGamePlayArea>()
                ?.ToolItemFirstLetter();
            
            AnalyticMgr.VideoAdSuccess(eventDes);
            Close();
        }
        else
        {
            MessageSystem.Instance.ShowTip("广告加载失败，请稍后重试。");
            AnalyticMgr.VideoAdFail(eventDes);
        }
    }

    private void UpdateCliamBtn(bool canClaimed)
    {
        ClaimAdsBtn.gameObject.SetActive(canClaimed);
        ClaimGoldBtn.gameObject.SetActive(true);
    }
    
    public void Close()
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
        ClaimGoldBtn.interactable = true;
        closeBtn.interactable = true;
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,false);
    }
}
