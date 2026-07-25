using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.PlayerLoop;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class SignAwardScreen : UIWindow
{
   [SerializeField] private Button okBtn; 
 
   [SerializeField] private List<GameObject> rewardList; 
  
   List<RewardItem> rewardItems = null;
   
    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        InitAwardUI();
    }

    private void InitAwardUI()
    {

        VictoryRewardConfig rewardConfig = null;
        if (StreakManager.Instance.winType == WinType.SevenWin)
        {
            rewardConfig= StreakManager.Instance.GetSevenSignRewards();
        }
        
        if (StreakManager.Instance.winType == WinType.StreakWin)
        {
            rewardConfig= StreakManager.Instance.GetBoxSignRewards();
        }
        
        bool pupaFull = ButterfliesManager.Instance.IsPupaSufficientForAllRemaining(); // 蛹足够时就替换
        if (!pupaFull)
        {
            rewardItems = rewardConfig.normalRewards;
        }
        else
        {
            rewardItems = rewardConfig.alternativeRewards;
        }
        
        if (rewardItems.Count <= 2)
        {
            rewardList[0].gameObject.SetActive(false);
        }
        else
        {
            rewardList[0].gameObject.SetActive(true);
        }
        
        for (int i = 0; i < rewardItems.Count; i++)
        {
            RewardItem rewardItem = rewardItems[i];
            int rewardid = i;

            if (rewardItems.Count <= 2)
            {
                rewardid = i+1;
            }
            
            ShowItemUI(rewardItem,rewardid);
        }
    }
    
    
    private void ShowItemUI(RewardItem rlist,int rewardid)
    {
        LimitRewordType type = (LimitRewordType)rlist.type;
        Image icon=rewardList[rewardid].transform.GetChild(0).GetComponent<Image>();
        Text count=rewardList[rewardid].GetComponentInChildren<Text>();
       
        icon.preserveAspect = true;
        string message = "获得连胜奖励";
       
        switch (type)
        {
            case LimitRewordType.Coins:
                count.text="x"+rlist.amount;
                icon.sprite = GetSprite(type);
                GameDataManager.Instance.UserData.UpdateGold(rlist.amount,true,true,message);
                break;
            case LimitRewordType.Butterfly:
               count.text="x"+rlist.amount;
               icon.sprite = GetSprite(type);
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly, rlist.amount,message);
                break;
            case LimitRewordType.HeadIcon:
                if (GameDataManager.Instance.UserData.TryGetRandomUnlockedAnimal(out int id))
                    Debug.Log($"获得头像ID：{id}");
                else
                {
                    rewardList[rewardid].gameObject.SetActive(false);
                    Debug.Log("已集齐全部头像");
                }
                string name = "head"+id;
                icon.sprite = GetSprite(type,name);
                GameDataManager.Instance.UserData.AddHeadIcon(id);
                GameDataManager.Instance.UserData.SendCurrencyEvent(id,"特殊头像",message);
                break;
            case LimitRewordType.Pupas:
                count.text="x"+rlist.amount;
                icon.sprite = GetSprite(type);
                int value = rlist.amount;
                GameDataManager.Instance.ButterflyData.AddPupa(value);
                GameDataManager.Instance.UserData.SendCurrencyEvent(value,"蚕蛹",message);
                break;
            case LimitRewordType.Tipstool:
                count.text="x"+rlist.amount;
                icon.sprite = GetSprite(type);
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, rlist.amount,message);
                break;
        }
        
        
        GameDataManager.Instance.UserData._signSaveData.AddWinClaim();
    }
    
    
    private Sprite GetSprite(LimitRewordType type,string headIconstr=null)
    {
        switch (type)
        {
            case LimitRewordType.Coins:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("gold0");
            case LimitRewordType.Butterfly:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_Butterfly");
            case LimitRewordType.Tipstool:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("tipicon");
            case LimitRewordType.Resettool:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Reset");
            case LimitRewordType.HeadIcon:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(headIconstr);
            case LimitRewordType.Pupas:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Pupas");
        }
        return null;
    }
    
    protected override void InitializeUIComponents()
    {
        okBtn.AddVibraClickAction(ClickOKBtn); // 绑定关闭按钮事件
    }

    private void ClickOKBtn()
    {
        OnCloseBtn();
    }

   
    private void OnCloseBtn()
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
