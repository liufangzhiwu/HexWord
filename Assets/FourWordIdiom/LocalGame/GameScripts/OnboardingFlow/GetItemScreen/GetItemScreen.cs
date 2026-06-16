using System;
using System.Collections;
using DG.Tweening;
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
    public static string targetWord; // 🌟 新增：用于存储当前目标词组
    //bool isshowpupa;
    private ShopDataItem shopDataItem;
 
    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        //EventDispatcher.instance.TriggerUpdateLayerCoin(true,false);
        
        // if (SystemManager.Instance.PanelIsShowing(PanelType.GamePlayArea))
        // {
        //     isshowpupa=StageController.Instance.CurStageData.PupaDatas!=null;
        // }
        // else if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
        // {
        //     isshowpupa=ChessStageController.Instance.CurrStageData.PupaDatas!=null;
        // }
        
        EventDispatcher.instance.TriggerUpdateLayerCoin(true,false,false);
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
                title.text = MultilingualManager.Instance.GetString("ItemName01","pingzi");
                UpdateCliamBtn(true);
                ClaimGoldBtn.GetComponentInChildren<Text>().text=GameDataManager.Instance.UserData.toolInfo[102].cost.ToString();
                tips.text = MultilingualManager.Instance.GetString("ItemDes01","pingzi");
                if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
                {
                    AwardIcon.sprite= AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("shop_tip");
                }
                if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
                {
                    AwardIcon.sprite= AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("shop_tip");
                }
                shopDataItem = ShopManager.shopManager.GetProduct("ItemBox02");
                eventDes=title.text+"弹窗广告";
                break;
            case LimitRewordType.SingleWordTipsttool:
                UpdateCliamBtn(false);
                title.text = MultilingualManager.Instance.GetString("ItemName01","pingzi");
                tips.text = MultilingualManager.Instance.GetString("ItemDes01","pingzi");
                ClaimGoldBtn.GetComponentInChildren<Text>().text=GameDataManager.Instance.UserData.toolInfo[101].cost.ToString();
                AwardIcon.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("shop_reset");
                shopDataItem = ShopManager.shopManager.GetProduct("ItemBox01");
                eventDes=title.text+"弹窗广告";
                AnalyticMgr.VideoAdShow(eventDes);
                break;
            case LimitRewordType.AutoComplete:
                UpdateCliamBtn(false);
                title.text = MultilingualManager.Instance.GetString("ItemName02","pingzi");
                tips.text = MultilingualManager.Instance.GetString("ItemDes02","pingzi");
                ClaimGoldBtn.GetComponentInChildren<Text>().text=GameDataManager.Instance.UserData.toolInfo[104].cost.ToString();
                AwardIcon.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("rocket");
                //shopDataItem = ShopManager.shopManager.GetProduct("ItemBox02");
                shopDataItem = null;
                eventDes=title.text+"弹窗广告";
                AnalyticMgr.VideoAdShow(eventDes);
                break;
            // case LimitRewordType.Min15Double:
            //     return AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("Mintool");
        }

        if (shopDataItem != null)
        {
            giftTable.gameObject.SetActive(true);
            giftTable.InitUI(limitRewordType,shopDataItem);
        }
        else
        {
            giftTable.gameObject.SetActive(false);
        }
       
        
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
            StartCoroutine(ShowAdsRewardUI());
        }
    }

    IEnumerator ShowAdsRewardUI()
    {
        yield return new WaitForSeconds(0.05f);
        AdRuleManager.Instance.TryShowRewardVideo(Define.AdKey.RewardAdIdStoreGold,UpdateAdsRewardUI);
    }

    private void ClickClaimGoldBtn()
    {
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[101];
        if (limitRewordType == LimitRewordType.SingleWordTipsttool)
        {
            toolInfo = GameDataManager.Instance.UserData.toolInfo[101];
        }else if (limitRewordType == LimitRewordType.Tipstool)
        {
            toolInfo = GameDataManager.Instance.UserData.toolInfo[102];
        }else if (limitRewordType == LimitRewordType.AutoComplete)
        {
            toolInfo = GameDataManager.Instance.UserData.toolInfo[104];
        }
        
       
        if (toolInfo.cost > GameDataManager.Instance.UserData.Gold)
        {        
            MessageSystem.Instance.ShowTip("TipGoldInsufficient");
            return;
        }
        
        GameDataManager.Instance.UserData.UpdateTool(limitRewordType, 1,"金币购买道具");
        GameDataManager.Instance.UserData.UpdateGold(-toolInfo.cost,true,true,"金币购买道具");
        
        
        if (limitRewordType == LimitRewordType.SingleWordTipsttool)
        {
            if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
            {
                SystemManager.Instance.GetPanel(PanelType.HexGamePlayArea)?.GetComponent<HexGamePlayArea>()
                    ?.ToolItemFirstLetter();
            }
            
            if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
            {
                SystemManager.Instance.GetPanel(PanelType.ChessPlayArea)?.GetComponent<ChessPlayArea>()
                    ?.UseTips();
            }
          
        }else if (limitRewordType == LimitRewordType.Tipstool)
        {
            if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
            {
                SystemManager.Instance.GetPanel(PanelType.HexGamePlayArea)?.GetComponent<HexGamePlayArea>()
                    ?.UseTips();
            }
            
        }else if (limitRewordType == LimitRewordType.AutoComplete)
        {
            if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
            {
                SystemManager.Instance.GetPanel(PanelType.ChessPlayArea)?.GetComponent<ChessPlayArea>()
                    ?.UseComplete();
            }
        }
       
        Close();
    }

    private void UpdateAdsRewardUI(bool isShow)
    {
        MessageSystem.Instance.HideLoadingAnimation();
        if (isShow)
        {
            Debug.LogError("上报的词语2" + targetWord);
            GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, 1,"看广告获取"+title.text+"道具");
            
            if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
            {
                SystemManager.Instance.GetPanel(PanelType.HexGamePlayArea)?.GetComponent<HexGamePlayArea>()
                    ?.UseTips();
            }

            if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
            {
                SystemManager.Instance.GetPanel(PanelType.ChessPlayArea)?.GetComponent<ChessPlayArea>()
                    ?.UseTips();
            }
            AnalyticMgr.VideoAdSuccess(eventDes);
            
            //EventDispatcher.instance.TriggerChangeGoldUI(0, false);
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
        targetWord = ""; // 🌟 每次关闭弹窗清空目标词
        ClaimGoldBtn.interactable = true;
        closeBtn.interactable = true;
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,true,false);
    }
}
