using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class EnergyScreen : UIWindow
{
    [Header("UI Elements")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text nextEnergyTimeText;
    [SerializeField] private Button adsButton;
    [SerializeField] private Button buyButton;
    [SerializeField] private Text titleText;
    [SerializeField] private Text descriptionText;
    private Coroutine _timerCoroutine;
    // Start is called before the first frame update
    void Start()
    {
        backButton.AddClickAction(OnCloseClicked);
        closeButton.AddClickAction(OnCloseClicked);
        adsButton.AddClickAction(OnAdsClicked);
        buyButton.AddClickAction(OnGoldBuyClicked);

        // 加个安全判断，防止子节点改变报错
        if (buyButton.transform.childCount > 1)
        {
            buyButton.transform.GetChild(1).GetComponent<Text>().text = AppGameSettings.ShopItems.EnergyCost.ToString();
        }
        titleText.text = MultilingualManager.Instance.GetString("MorePower");
        descriptionText.text = MultilingualManager.Instance.GetString("TimeToPower");
        adsButton.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("FreePower").Substring(0,2);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        // EventDispatcher.instance.TriggerHighlightGoldAndEnergy(true);
        if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);
        _timerCoroutine = StartCoroutine(CheckShowNextEnergyTime());
    }

    private IEnumerator CheckShowNextEnergyTime()
    {
        WaitForSeconds wait = new WaitForSeconds(1f);
       
        while (true)
        {
            var userData = GameDataManager.Instance.UserData;
            // 顺手核算一下体力，防止玩家打开界面发呆跨越了30分钟恢复点
            userData.CalculateEnergyRegen();

            if (userData.Energy >= UserData.MAX_NATURAL_ENERGY)
            {
                // 👇 修复 1：体力满了不要显示 00:00，显示中文“已满”
                nextEnergyTimeText.text = "已满"; 
            }
            else
            {
                // 👇 修复 2：把 1799 这种纯秒数，格式化成 29:59 玩家才看得懂
                int remainSeconds = userData.GetNextEnergyRegenSeconds();
                int mins = remainSeconds / 60;
                int secs = remainSeconds % 60;
                nextEnergyTimeText.text = string.Format("{0:00}:{1:00}", mins, secs);
            }
            yield return wait;
        }
    }
    
    private void OnGoldBuyClicked()
    {
        if (AppGameSettings.ShopItems.EnergyCost > GameDataManager.Instance.UserData.Gold)
        {        
            MessageSystem.Instance.ShowTip("TipGoldInsufficient");
            return;
        }
        
        GameDataManager.Instance.UserData.UpdateGold(-AppGameSettings.ShopItems.EnergyCost,true,true,"金币购买体力");
        GameDataManager.Instance.UserData.AddBonusEnergy(1, "金币购买");
        MessageSystem.Instance.ShowTip("体力购买成功！");
        GameDataManager.Instance.CommitGameData();
    }

    private void OnAdsClicked()
    {
        AnalyticMgr.VideoAdClick("体力广告");
        AdRuleManager.Instance.TryShowRewardVideo(Define.AdKey.RewardAdIdStoreGold, call =>
        {
            MessageSystem.Instance.HideLoadingAnimation();
            if (call)
            {
                GameDataManager.Instance.UserData.AddBonusEnergy(1, "看广告获取");
                AnalyticMgr.VideoAdSuccess("体力广告");
                MessageSystem.Instance.ShowTip("体力恢复成功！");
                GameDataManager.Instance.UserData.totalSeeAds++;
            }
            else
            {
                MessageSystem.Instance.ShowTip("广告加载失败，请稍后重试。");
                AnalyticMgr.VideoAdFail("体力广告");
            }
        });
    }
    private void OnCloseClicked()
    {
        SystemManager.Instance.HidePanel(PanelType.EnergyScreen);
    }

    protected override void OnDisable()
    {
        // EventDispatcher.instance.TriggerHighlightGoldAndEnergy(false);
        if (_timerCoroutine != null)
        {
            StopCoroutine(_timerCoroutine);
            _timerCoroutine = null;
        }
        base.OnDisable();
    }
}
