using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.PlayerLoop;
using UnityEngine.UI;

public class SignWaterScreen : UIWindow
{
    [SerializeField] private Button AdsStartBtn; // 关闭按钮
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Button StartBtn; // 隐私条款按钮
    [SerializeField] private Button HideBtn; // 隐私条款按钮
    [SerializeField] private GameObject parent; // 隐私条款按钮
    [SerializeField] private GameObject[] CoinsObjs; // 隐私条款按钮
    [SerializeField] private Image huObj; // 隐私条款按钮
    [SerializeField] private Text title; // 隐私条款按钮
    [SerializeField] private Text SignOverText; // 隐私条款按钮
    [SerializeField] private Text WaitTimeText; // 等待时长文本
    [SerializeField] private Text closetips;
    [SerializeField] private Text AdsAnniuDes;
    [SerializeField] private Image adsloading;
    [SerializeField] private Image adsIcon;
    //[SerializeField] private Image coins; // 隐私条款按钮
    bool iswater = false;

    private int minutes = 3;

    private int[] AwardValues = { 15,35,50,100};
   
    void Start()
    {
        WaterManager.instance.ShowDaoWater(false);
    }


    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        //ShowStartWater();
        WaterManager.instance.OnWaterProgress += ShowWaterProgress;
        InitUI();           
        CheckSignEvent();
        StartCoroutine(ShowWaterAnim());

        EventDispatcher.instance.TriggerUpdateLayerCoin(true,false);
    }

    private void InitUI()
    {
        title.text = MultilingualManager.Instance.GetString("SignTile");
        int signid = GameDataManager.Instance.UserData.signid + 1;
        WaitTimeText.text = MultilingualManager.Instance.GetString("SignPourTea0" +signid);
        StartBtn.gameObject.SetActive(true);
        //AdsAnniuDes.text= MultilingualManager.Instance.GetString("SignPourTea0" +signid);
        //StartBtn.gameObject.SetActive(GameDataManager.Instance.UserData.signid==0);
        //AdsStartBtn.gameObject.SetActive(GameDataManager.Instance.UserData.signid>0&&GameDataManager.Instance.UserData.signid<4);
        SignOverText.gameObject.SetActive(GameDataManager.Instance.UserData.signid>3);
        SignOverText.text = MultilingualManager.Instance.GetString("SignPourTeaFinish");
        closetips.text = MultilingualManager.Instance.GetString("limitedRewardsDes05");
        StartBtn.interactable = true;
        //WaitTimeText.gameObject.SetActive(false);
        //adsIcon.gameObject.SetActive(true);
        adsloading.gameObject.SetActive(false);
        if(GameDataManager.Instance.UserData.signid > 0&&GameDataManager.Instance.UserData.signid <= 3)
        {
            StartBtn.interactable = false;
            //StartCoroutine(CheckIsReadyToShowAd());
            StartCoroutine(WaitTime());
        }

        if (GameDataManager.Instance.UserData.signid > 3)
        {
            StartBtn.gameObject.SetActive(false);
        }
    }
    
    private void CheckSignEvent()
    {
        if (GameDataManager.Instance.UserData.isDayEnterSign)
        {
            DateTime dateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);// 将字符串转换为 DateTime
            //FirebaseManager.Instance.ActivityBegin("签到活动", dateTime.ToString());
            GameDataManager.Instance.UserData.EveryDayOpenSign();
        }
    }

    IEnumerator WaitTime()
    {
        TimeSpan timeSpan=WaterManager.instance.StartTime.AddMinutes(minutes).Subtract(DateTime.Now);
        while (timeSpan.TotalSeconds > 0)
        {
            timeSpan = WaterManager.instance.StartTime.AddMinutes(minutes).Subtract(DateTime.Now);
          
            WaitTimeText.gameObject.SetActive(true);
            WaitTimeText.text = UIUtilities.GetDateMintueStyle(timeSpan);
            yield return new WaitForSeconds(1f);
    
            if (timeSpan.TotalSeconds <= 0)
            {
                int signid = GameDataManager.Instance.UserData.signid + 1;
                WaitTimeText.text = MultilingualManager.Instance.GetString("SignPourTea0" +signid);
                StartBtn.interactable = true;
                yield break;
            }
        }
    
        if (timeSpan.TotalSeconds <= 0)
        {
            int signid = GameDataManager.Instance.UserData.signid + 1;
            WaitTimeText.text = MultilingualManager.Instance.GetString("SignPourTea0" +signid);
            //WaitTimeText.gameObject.SetActive(false);
            StartBtn.interactable = true;
        }
    }

    IEnumerator ShowWaterAnim()
    {
        yield return new WaitForSeconds(0.25f);
        WaterManager.instance.WaterShow(true);
    }

    IEnumerator CheckIsReadyToShowAd()
    {
        const float checkInterval = 2f;
        const int maxAttempts = 10; // 防止无限循环
        string adskey = "checkingold" + GameDataManager.Instance.UserData.signid;
        //AdsManager.Instance.rewardedAdskey = adskey;
        // 初始状态检查
        bool isReady =false;
        bool isConnected = false;

        // 立即更新UI状态
        adsIcon.gameObject.SetActive(isReady);
        adsloading.gameObject.SetActive(!isReady);

        // 如果没有网络连接，直接退出
        if (!isConnected)
        {
            yield break;
        }

        // 轮询检查
        int attempt = 0;
        while (attempt < maxAttempts && isConnected&&!isReady)
        {
            yield return new WaitForSeconds(checkInterval);
    
            attempt++;
            //isReady = AdsManager.Instance.IsReadyToShowAd();
            isConnected = GameCoreManager.Instance.IsNetworkActive;
    
            // 状态变化处理
            if (isReady&&isConnected)
            {
                adsloading.gameObject.SetActive(false);
                adsIcon.gameObject.SetActive(true);                   
                yield break;
            }
        }

        // 立即更新UI状态
        adsIcon.gameObject.SetActive(isReady);
        adsloading.gameObject.SetActive(!isReady);

        // 可选：超过最大尝试次数的处理
        if (!isReady)
        {
            Debug.LogWarning($"广告加载超时，最大尝试次数 {maxAttempts} 次");
            // 可以在这里触发备用广告加载或错误处理
        }
    }
   
    protected override void InitializeUIComponents()
    {
        HideBtn.AddClickAction(OnCloseBtn);
        closeBtn.AddClickAction(OnCloseBtn); // 绑定关闭按钮事件
        StartBtn.AddClickAction(OnStartBtn);
        AdsStartBtn.AddClickAction(OnAdsStartBtn);
    }
    
    public void OnAdsStartBtn()
    {
        int sign = GameDataManager.Instance.UserData.signid;
        if (sign > 3||iswater)
        {
            return;
        }
        
        //FirebaseManager.Instance.VideoAdClick("签到"+sign,GameDataManager.Instance.UserData.CurrentStage.ToString());
        
        string adskey = "checkingold" + sign;
        // AdsManager.Instance.ShowRewardedAd((bool isover) =>
        // {
        //     if (isover)
        //     {
        //         iswater = true;
        //         AdsStartBtn.enabled = false;
        //         closeBtn.enabled = false;
        //         HideBtn.enabled = false;
        //         int value = AwardValues[sign];
        //         WaterManager.instance.PlayerWater(false, value);
        //         StartCoroutine(CheckIsReadyToShowAd());
        //         //AdjustManager.Instance.SendVIAdsSuccessEvent("签到"+sign);
        //         //FirebaseManager.Instance.VideoAdSuccess("签到"+sign,GameDataManager.Instance.UserData.CurrentStage.ToString());
        //      }
        //      else
        //      {
        //          //FirebaseManager.Instance.VideoFail("签到"+sign,GameDataManager.Instance.UserData.CurrentStage.ToString());
        //      }
        //  }, adskey, true);
    }

    public void OnStartBtn()
    {
        if (GameDataManager.Instance.UserData.signid > 3||iswater)
        {
            return;
        }
        WaterManager.instance.StartTime=DateTime.Now;
        iswater = true;
        StartBtn.enabled = false;
        closeBtn.enabled = false;
        HideBtn.enabled = false;
        int value= AwardValues[GameDataManager.Instance.UserData.signid];
        WaterManager.instance.PlayerWater(false,value);
    }
  
    private void ShowWaterProgress(int progressid)
    {
        int lineid = GameDataManager.Instance.UserData.signid;
        if (progressid ==lineid-1)
        {
            WaterPause(progressid);
            //StartBtn.gameObject.SetActive(lineid==0);
            //AdsStartBtn.gameObject.SetActive(lineid>0&&lineid<4);               
            int textid=GameDataManager.Instance.UserData.signid + 1;
            if (lineid > 3)
            {
                StartBtn.interactable = true;
                StartBtn.gameObject.SetActive(false);
                SignOverText.gameObject.SetActive(true);
            }
            else
            {
                AdsAnniuDes.text= MultilingualManager.Instance.GetString("SignPourTea0"+textid);
                //WaitTimeText.text = MultilingualManager.Instance.GetString("SignPourTea0"+textid);
                if(GameDataManager.Instance.UserData.signid > 0&&GameDataManager.Instance.UserData.signid <= 3)
                {
                    StartBtn.interactable = false;
                    StartCoroutine(WaitTime());
                    //StartCoroutine(CheckIsReadyToShowAd());
                }
            }
        }
    }

    private void GetAward(int id)
    {
        int value= AwardValues[id];
        CustomFlyInManager.Instance.FlyInGold(CoinsObjs[id].transform,() =>
        {
            EventDispatcher.instance.TriggerChangeGoldUI(value,true);
            //GameDataManager.Instance.UserData.UpdateGold(value,true);
            //NextLevelBtn.gameObject.SetActive(true);
        });
        DisCoinsTable(id);
        HideBtn.enabled = true;
        closeBtn.enabled = true;
    }

    private void DisCoinsTable(int id)
    {
        CoinsObjs[id].GetComponent<Animator>().enabled = false;
        CoinsObjs[id].GetComponent<CanvasGroup>().DOFade(0, 0.3f).OnComplete(() =>
        {
            CoinsObjs[id].gameObject.SetActive(false);
        });
    }

    private void WaterPause(int progressid)
    {
        WaterManager.instance.WaterPause();
        //huObj.transform.DOLocalRotate(new Vector3(0f, 0f, 0f), 0.2f, RotateMode.Fast).OnComplete(() =>
        //{
            GetAward(progressid);
        //});

        StartCoroutine(ReSet());
    }

    IEnumerator ReSet()
    {
        yield return new WaitForSeconds(1f);
        iswater = false;
        StartBtn.enabled = true;
        AdsStartBtn.enabled = true;
    }

    private void OnCloseBtn()
    {
        base.Close(); // 隐藏面板
        WaterManager.instance.WaterShow(false);
    }

    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,true);
        WaterManager.instance.OnWaterProgress -= ShowWaterProgress;
    }
}
