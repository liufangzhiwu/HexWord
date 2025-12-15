using System;
using System.Collections;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RewardAdsScreen : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Text title; // 音效文本显示
    [SerializeField] private Text count; // 语言选择文本显示
    [SerializeField] private Image AwardIcon; // 语言选择文本显示
    [SerializeField] private Image adsloading;
    [SerializeField] private Image adsIcon;
    [SerializeField] private Button ClaimBtn;
    [SerializeField] private Button ClaimAdsBtn;
 
    private bool isCanClaim = false;

    protected override void OnEnable()
    {
        base.OnEnable();
        UpdateCliamBtn(false);
        InitUI();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        //EventDispatcher.instance.TriggerUpdateLayerCoin(true,false);
    }

    private void InitUI()
    {
        title.text = MultilingualManager.Instance.GetString("ADPopTitle");
        ClaimAdsBtn.GetComponentInChildren<Text>().text= MultilingualManager.Instance.GetString("ADPopWatch");
        ClaimBtn.GetComponentInChildren<Text>().text= MultilingualManager.Instance.GetString("ADPopReceive");
        //count.text = "x"+GameDataManager.instance.UserData.ABseeAdsRewardCoins;
        adsloading.gameObject.SetActive(false);
        adsIcon.gameObject.SetActive(true);
        //StartCoroutine(CheckIsReadyToShowAd());
        
        AnalyticMgr.VideoAdShow("金币弹窗广告");
    }

    // IEnumerator CheckIsReadyToShowAd()
    // {
    //     const float checkInterval = 2f;
    //     const int maxAttempts = 10; // 防止无限循环
    //
    //     // 初始状态检查
    //     bool isReady = true;
    //     bool isConnected = GameCoreManager.Instance.IsNetworkActive;
    //
    //     // 立即更新UI状态
    //     adsIcon.gameObject.SetActive(isReady);
    //     adsloading.gameObject.SetActive(!isReady);
    //
    //     // 如果没有网络连接，直接退出
    //     if (!isConnected)
    //     {
    //         yield break;
    //     }
    //
    //     // 轮询检查
    //     int attempt = 0;
    //
    //     // 立即更新UI状态
    //     adsIcon.gameObject.SetActive(isReady);
    //     adsloading.gameObject.SetActive(!isReady);
    //     
    // }

    protected override void InitializeUIComponents()
    {
        closeBtn.AddClickAction(GetRewardClose); // 绑定关闭按钮事件
        ClaimAdsBtn.AddClickAction(ClickClaimBtn);
        ClaimBtn.AddClickAction(GetRewardClose);
    }

    private void ClickClaimBtn()
    {
        //FirebaseManager.Instance.VideoAdClick("商店弹窗",SaveSystem.Instance.UserData.CurrentStage.ToString());
        
        
#if UNITY_OPENHARMONY
        AnalyticMgr.VideoAdClick("金币弹窗广告");
        Game.Ads.ShowReward(Define.AdKey.RewardAdIdStoreGold,UpdateAdsRewardUI);
#elif Unity_ShowLog
        UpdateAdsRewardUI(true);
#endif
    }

    private void UpdateAdsRewardUI(bool isClaimed)
    {
        if (isClaimed)
        {
            UpdateCliamBtn(true);
            AnalyticMgr.VideoAdSuccess("金币弹窗广告");
            GameDataManager.Instance.UserData.totalSeeAds++;
            int coins=30;
            GameDataManager.Instance.UserData.UpdateGold(coins,false,false,"金币广告弹窗获得");
           
            //DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedSeeAds,1);
        }
        else
        {
            AnalyticMgr.VideoAdFail("金币弹窗广告");
        }
    }

    private void UpdateCliamBtn(bool canClaimed)
    {
        isCanClaim = canClaimed;
        ClaimAdsBtn.gameObject.SetActive(!isCanClaim);
        ClaimBtn.gameObject.SetActive(isCanClaim);
    }
    
    IEnumerator GetAdsReward()
    {
        ClaimBtn.interactable = false;
        closeBtn.interactable = false;
        if (isCanClaim)
        {
            CustomFlyInManager.Instance.FlyInGold(AwardIcon.transform, () =>
            {
                int coins=30;
                EventDispatcher.instance.TriggerChangeGoldUI(coins, true);
            });
            isCanClaim = false;
            yield return new WaitForSeconds(0.8f);
        }
        base.Close(); // 隐藏面板
    }
    
    
    private void GetRewardClose()
    {
        StartCoroutine(GetAdsReward());
    }
    
    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ClaimBtn.interactable = true;
        closeBtn.interactable = true;
    }
}
