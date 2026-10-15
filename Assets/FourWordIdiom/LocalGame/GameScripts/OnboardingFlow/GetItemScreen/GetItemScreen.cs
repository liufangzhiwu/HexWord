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
    [SerializeField] private ScrollRect scrollRect;
 
    public static LimitRewordType limitRewordType;
    
    [Header("Settings")]
    public float snapSpeed = 15f;
    public float snapThreshold = 0.9f;
    public float stopVelocityThreshold = 50f;
    
    private float[] pagePositions;
    private float targetPosition;
    private bool isSnapping;
    private bool wasDragging;
    private int pageCount;
    private float pageWidth;
    
    private float dragStartPosition; // 记录拖动开始时的位置
    private bool isDragStartRecorded = false; // 是否已记录拖动起始点
    private int currentPageIndex; // 当前页面索引


    private string eventDes;
    public static string targetWord; // 🌟 新增：用于存储当前目标词组
    //bool isshowpupa;
    private ShopDataItem shopDataItem;
 
    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
      
        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
        {
            SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        }
        
      
        EventDispatcher.instance.TriggerUpdateLayerCoin(true,true,false,true);
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
                AwardIcon.sprite= AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_Butterfly");
                break;
            case LimitRewordType.Tipstool:
                title.text = MultilingualManager.Instance.GetString("ItemName01","pingzi");
                tips.text = MultilingualManager.Instance.GetString("ItemDes01","pingzi");
                UpdateCliamBtn(true);
                ClaimGoldBtn.GetComponentInChildren<Text>().text=GameDataManager.Instance.UserData.toolInfo[102].cost.ToString();
                AwardIcon.sprite= AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("tipicon");
             
                shopDataItem = ShopManager.shopManager.GetProduct("ItemBox01");
                eventDes=title.text+"弹窗广告";
                break;
            case LimitRewordType.AutoComplete:
                UpdateCliamBtn(false);
                title.text = MultilingualManager.Instance.GetString("ItemName02","pingzi");
                tips.text = MultilingualManager.Instance.GetString("ItemDes02","pingzi");
                ClaimGoldBtn.GetComponentInChildren<Text>().text=GameDataManager.Instance.UserData.toolInfo[104].cost.ToString();
                AwardIcon.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("rocket");
                shopDataItem = ShopManager.shopManager.GetProduct("ItemBox02");
                eventDes=title.text+"弹窗广告";
                break;
            // case LimitRewordType.AutoComplete:
            //     UpdateCliamBtn(false);
            //     title.text = MultilingualManager.Instance.GetString("ItemName02","pingzi");
            //     tips.text = MultilingualManager.Instance.GetString("ItemDes02","pingzi");
            //     ClaimGoldBtn.GetComponentInChildren<Text>().text=GameDataManager.Instance.UserData.toolInfo[104].cost.ToString();
            //     AwardIcon.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("rocket");
            //     //shopDataItem = ShopManager.shopManager.GetProduct("ItemBox02");
            //     shopDataItem = null;
            //     eventDes=title.text+"弹窗广告";
            //     AnalyticMgr.VideoAdShow(eventDes);
            //     break;
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
    
    private void Update()
    {
        HandleAutoSnapping();
    }
    
    
    // 新增方法：获取当前最近页面的索引
    private int GetCurrentPageIndex()
    {
        int nearestPage = 0;
        float minDistance = float.MaxValue;

        for (int i = 0; i < pageCount; i++)
        {
            float distance = Mathf.Abs(scrollRect.horizontalNormalizedPosition - pagePositions[i]);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestPage = i;
            }
        }
        return nearestPage;
    }

    
     private void HandleAutoSnapping()
    {
        // 自动对齐逻辑
        if (isSnapping)
        {
            scrollRect.horizontalNormalizedPosition = Mathf.Lerp(
                scrollRect.horizontalNormalizedPosition,
                targetPosition,
                snapSpeed * Time.deltaTime);

            if (Mathf.Abs(scrollRect.horizontalNormalizedPosition - targetPosition) < snapThreshold)
            {
                scrollRect.horizontalNormalizedPosition = targetPosition;
                isSnapping = false;
                // 更新当前页面索引
                currentPageIndex = GetCurrentPageIndex();
            }
        }

        // 检测拖动状态变化
        bool isDraggingNow = Mathf.Abs(scrollRect.velocity.x) > stopVelocityThreshold;

        // 拖动开始时记录初始位置
        if (!wasDragging && isDraggingNow)
        {
            dragStartPosition = scrollRect.horizontalNormalizedPosition;
            isDragStartRecorded = true;
            // 记录拖动开始时的页面索引
            currentPageIndex = GetCurrentPageIndex();
        }

        // 拖动结束时判断方向（最终优化版）
        if (wasDragging && !isDraggingNow && isDragStartRecorded)
        {
            float dragEndPosition = scrollRect.horizontalNormalizedPosition;
    
            // 计算实际像素位移（使用完整内容宽度）
            float pixelDelta = (dragEndPosition - dragStartPosition) * pageWidth;
            float absPixelDelta = Mathf.Abs(pixelDelta);
            const float pixelThreshold = 10f;

            // 仅当有效拖动距离超过阈值时执行方向判断
            if (absPixelDelta > pixelThreshold)
            {
                // 直接基于像素位移方向判断（避免归一化精度问题）
                if (pixelDelta > 0) 
                {
                    SnapToNextPage();
                    Debug.Log($"右滑翻页 位移:{pixelDelta:F2}px");
                }
                else 
                {
                    SnapToPreviousPage();
                    Debug.Log($"左滑翻页 位移:{pixelDelta:F2}px");
                }
            }
            else
            {
                // 微移情况：恢复原始位置
                SnapToNearestPage();
                Debug.Log($"微调吸附 位移:{pixelDelta:F2}px");
            }
    
            // 重置拖动标记
            isDragStartRecorded = false;
        }

        wasDragging = isDraggingNow;
    }
    

    protected override void InitializeUIComponents()
    {
        closeBtn.AddClickAction(Close); // 绑定关闭按钮事件
        ClaimAdsBtn.AddClickAction(ClickClaimBtn);
        ClaimGoldBtn.AddClickAction(ClickClaimGoldBtn);
    }

    private void ClickClaimBtn()
    {
        AnalyticMgr.VideoAdClick(eventDes);
        StartCoroutine(ShowAdsRewardUI());
    }

    IEnumerator ShowAdsRewardUI()
    {
        yield return new WaitForSeconds(0.05f);
        AdRuleManager.Instance.TryShowTipToolRewardVideo(Define.AdKey.RewardAdIdStoreGold,UpdateAdsRewardUI);
    }

    private void ClickClaimGoldBtn()
    {
        ToolInfo toolInfo = GameDataManager.Instance.UserData.toolInfo[102];
        if (limitRewordType == LimitRewordType.Tipstool)
        {
            toolInfo = GameDataManager.Instance.UserData.toolInfo[102];
        }else if (limitRewordType == LimitRewordType.AutoComplete)
        {
            toolInfo = GameDataManager.Instance.UserData.toolInfo[104];
        }
        
       
        if (toolInfo.cost > GameDataManager.Instance.UserData.Gold)
        {        
            //MessageSystem.Instance.ShowTip("TipGoldInsufficient");
            // 直接跳转到商店
            SystemManager.Instance.ShowPanel(PanelType.ShopScreen);
            Close();
            return;
        }
        
        GameDataManager.Instance.UserData.UpdateTool(limitRewordType, 1,"金币购买道具");
        GameDataManager.Instance.UserData.UpdateGold(-toolInfo.cost,true,true,"金币购买道具");
        
       
        if (limitRewordType == LimitRewordType.Tipstool)
        {
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
            
            
        }else if (limitRewordType == LimitRewordType.AutoComplete)
        {
            
            if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
            {
                SystemManager.Instance.GetPanel(PanelType.HexGamePlayArea)?.GetComponent<HexGamePlayArea>()
                    ?.UseAutoTips();
            }
            
            if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
            {
                SystemManager.Instance.GetPanel(PanelType.ChessPlayArea)?.GetComponent<ChessPlayArea>()
                    ?.UseComplete();
            }
        }
       
        Close();
    }
    
    
    // 新增方法：对齐到上一页
    private void SnapToPreviousPage()
    {
        int targetPage = Mathf.Max(0, currentPageIndex - 1);
        ScrollToPage(targetPage);
    }

    // 新增方法：对齐到下一页
    private void SnapToNextPage()
    {
        int targetPage = Mathf.Min(pageCount - 1, currentPageIndex + 1);
        ScrollToPage(targetPage);
    }


    private void SnapToNearestPage()
    {
        ScrollToPage(GetCurrentPageIndex());
    }

    public void ScrollToPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= pageCount) return;

        targetPosition = pagePositions[pageIndex];
        isSnapping = true;
        currentPageIndex = pageIndex; // 更新当前页面索引
    }
    

    private void UpdateAdsRewardUI(bool isShow)
    {
        MessageSystem.Instance.HideLoadingAnimation();
        if (isShow)
        {
            Debug.Log("上报的词语2" + targetWord);
            
            if (SystemManager.Instance.PanelIsShowing(PanelType.HexGamePlayArea))
            {
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, 1,"看广告获取"+title.text+"道具");
                
                SystemManager.Instance.GetPanel(PanelType.HexGamePlayArea)?.GetComponent<HexGamePlayArea>()
                    ?.UseTips();
            }

            if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
            {
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, 1,"看广告获取"+title.text+"道具");
                
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
        AudioManager.Instance.TriggerVibration(10, 200);
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
        EventDispatcher.instance.TriggerChangeGoldUI(0, true);
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,true,false);
        if (SystemManager.Instance.PanelIsShowing(PanelType.ChessPlayArea))
        {
            SystemManager.Instance.HidePanel(PanelType.HeaderSection);
        }
       
    }
}
