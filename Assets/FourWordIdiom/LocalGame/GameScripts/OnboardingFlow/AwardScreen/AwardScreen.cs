using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AwardScreen : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Text title; // 音效文本显示
    [SerializeField] private List<GameObject> rewardList; // 语言选择文本显示

    [SerializeField] private Button ClaimBtn;

    public List<RewardItem> rewardItems;
    
    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
        AudioManager.Instance.PlaySoundEffect("hudieReward");
    }

    private void InitUI()
    {
        ClaimBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ADPopReceive");
        //title.text = MultilingualManager.Instance.GetString("FirstWin");
        //tips.text = MultilingualManager.Instance.GetString("FirstWinRewards");


        InitAwardUI();
    }
    
    
     private void InitAwardUI()
    {

        foreach (GameObject rGameObject in rewardList)
        {
            rGameObject.gameObject.SetActive(false);
        }
        
        //var items = new List<AnalyticMgr.Item>();
        if (Game.self.Shop.CurrentShopDataItem!=null)
        {
            for (int i = 0; i <  Game.self.Shop.CurrentShopDataItem.productContent.Count; i++)
            {
                var dataitem = Game.self.Shop.CurrentShopDataItem.productContent[i];

                RewardItem rewardItem = new RewardItem()
                {
                    type = int.Parse(dataitem[0]),
                    amount = int.Parse(dataitem[1])
                };
                int reward = i;
                ShowItemUI(rewardItem,reward);
            }
        }
       
    }
    
    
    private void ShowItemUI(RewardItem rlist,int rewardid)
    {
        LimitRewordType type = (LimitRewordType)rlist.type;
        Image icon=rewardList[rewardid].GetComponentInChildren<Image>();
        Text count=rewardList[rewardid].GetComponentInChildren<Text>();
        rewardList[rewardid].SetActive(true);
       
        icon.preserveAspect = true;
        //string message = "回归首胜奖励";
       
        switch (type)
        {
            case LimitRewordType.Coins:
                count.text="\u00d7"+rlist.amount;
                icon.sprite = GetSprite(type);
                //GameDataManager.Instance.UserData.UpdateGold(rlist.amount,true,true,message);
                break;
            case LimitRewordType.Butterfly:
               count.text="\u00d7"+rlist.amount;
               icon.sprite = GetSprite(type);
                //GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly, rlist.amount,message);
                break;
            case LimitRewordType.AutoComplete:
                count.text="\u00d7"+rlist.amount;
                icon.sprite = GetSprite(type);
                //GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, rlist.amount,message);
                break;
            case LimitRewordType.Tipstool:
                count.text="\u00d7"+rlist.amount;
                icon.sprite = GetSprite(type);
                //GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, rlist.amount,message);
                break;
        }
    }
    
    
    private Sprite GetSprite(LimitRewordType type)
    {
        switch (type)
        {
            case LimitRewordType.Coins:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("gold0");
            case LimitRewordType.Butterfly:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_Butterfly");
            case LimitRewordType.Tipstool:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("tipicon");
            case LimitRewordType.AutoComplete:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("rocket");
            case LimitRewordType.Pupas:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Pupas");
        }
        return null;
    }

    protected override void InitializeUIComponents()
    {
        closeBtn.AddVibraClickAction(ClickClaimBtn); // 绑定关闭按钮事件
        ClaimBtn.AddVibraClickAction(ClickClaimBtn); // 绑定关闭按钮事件
    }

    private void ClickClaimBtn()
    {

        if (SystemManager.Instance.PanelIsShowing(PanelType.ShopScreen))
        {
            StartCoroutine(ShowRewardAnim());
        }
        else
        {
            EventDispatcher.instance.TriggerChangeGoldUI(0, false);
            Close();
        }
    }

    IEnumerator ShowRewardAnim()
    {
        
        //var items = new List<AnalyticMgr.Item>();
        if (Game.self.Shop.CurrentShopDataItem!=null)
        {
            int rewardid = 0;
            foreach (var dataitem in Game.self.Shop.CurrentShopDataItem.productContent)
            {
                Image icon=rewardList[rewardid].GetComponentInChildren<Image>();
                GameObject endposObj=CustomFlyInManager.Instance.ShopGoldObj;
                int count = int.Parse(dataitem[1]);
                int type = int.Parse(dataitem[0]);
                //items.Add(new AnalyticMgr.Item { item_name = type.ToString(), quantity = count });
                switch (type)
                {
                    case (int)LimitRewordType.Coins:
                        endposObj=CustomFlyInManager.Instance.ShopGoldObj;
                        break;
                    case (int)LimitRewordType.Butterfly:
                        endposObj=CustomFlyInManager.Instance.ShopButterflyObj;
                        break;
                    case (int)LimitRewordType.Tipstool://放大镜道具，整个词语提示
                        endposObj=CustomFlyInManager.Instance.ShopTipObj;
                        break;
                    case (int)LimitRewordType.AutoComplete://提示灯道具，单个字符提示
                        endposObj=CustomFlyInManager.Instance.ShopAutoObj;
                        break;
                    case (int)LimitRewordType.RemoveAds:
                    case (int)LimitRewordType.Remove7DayAds:
                        //BuyRemoveAdsEvent(type);
                        break;
                }

               
                CustomFlyInManager.Instance.FlyAwardIn(icon.gameObject.transform.position,endposObj,icon.gameObject,
                    () =>
                    {
                        AudioManager.Instance.PlaySoundEffect("BtnUnlock");
                        EventDispatcher.instance.TriggerChangeGoldUI(0, false);
                    });
                
                rewardid++;
                
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        Close();

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
