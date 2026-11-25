using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class LightItem : MonoBehaviour
{
    [SerializeField] private List<GameObject> rewardList;
    [SerializeField] private Image lightImage;
    [SerializeField] private Image gouImage;
    [SerializeField] private Image jianImage;
    [SerializeField] private GameObject effect;
    [SerializeField] private GameObject huanObject;
    private LimitDataItem Curlimitdata;
    
    public void SetUI(LimitDataItem limitdata)
    {
        Curlimitdata=limitdata;
        lightImage.gameObject.SetActive(false);
        if(limitdata == null) return;
        
        var rewards = LimitTimeManager.Instance.GetEffectiveRewards(Curlimitdata); // 获取有效奖励

        if (Curlimitdata.id <GameDataManager.Instance.UserData.timerePuzzleid)
        {
            ShowComplete(rewards.Count);
        }
        else
        {
            for (int i = 0; i < rewards.Count; i++)
            {
                ShowLighItemUI(rewards[i], limitdata.id, i);
            }
        }
    }

    private void ShowLighItemUI(List<int> rlist,int id,int rewardid)
    {
        LimitRewordType type = (LimitRewordType)rlist[0];
        Image icon=rewardList[rewardid].GetComponentInChildren<Image>();
        Text count=rewardList[rewardid].GetComponentInChildren<Text>();
        icon.sprite = GetSprite(type,id>=LimitTimeManager.Instance.GetLimitItems().Count-1);
        //icon.SetNativeSize();
        rewardList[rewardid].transform.localScale = Vector3.one;
        gouImage.transform.localScale=Vector3.zero;
        if (Curlimitdata.id >= 1)
        {
            jianImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("jian");
            jianImage.SetNativeSize();
        }

        // 最后一段只有 2 份奖励，第三个位置让给「幸运循环」标识
        bool isLastStage = IsLastLanternItem() && id >= LimitTimeManager.Instance.GetLastLimitId();
        if (isLastStage)
        {
            if (rewardList.Count > 2) rewardList[2].gameObject.SetActive(false);
        }
        else if (rewardid < rewardList.Count)
        {
            rewardList[rewardid].gameObject.SetActive(true);
        }

        // 幸运循环标识只在「最后一段 + 已进入循环」时显示，普通段/其它灯笼一律隐藏
        RefreshLuckyLoopSign(isLastStage);
        
        count.fontSize = 70;
        switch (type)
        {
            case LimitRewordType.Coins:
                count.text=rlist[1].ToString();
                count.GetComponent<RectTransform>().sizeDelta = new Vector2(130,83);
                break;
            case LimitRewordType.Butterfly:
                icon.GetComponent<RectTransform>().sizeDelta = new Vector2(135,118);
                count.text=rlist[1].ToString();
                break;
            case LimitRewordType.Min5Double:
                count.text="<size=50>x<size=60>2</size></size>\n5分钟";
                count.fontSize = 35;
                count.GetComponent<RectTransform>().sizeDelta = new Vector2(130,124);
                break;
            case LimitRewordType.Min15Double:
                count.text = "<size=50>x<size=60>2</size></size>\n15分钟";
                count.GetComponent<RectTransform>().sizeDelta = new Vector2(130,124);
                count.fontSize = 35;
                break;
            case LimitRewordType.Pupas:
                icon.GetComponent<RectTransform>().sizeDelta = new Vector2(135,118);
                count.text=rlist[1].ToString();
                break;
            default:
                count.text=rlist[1].ToString();
                count.GetComponent<RectTransform>().sizeDelta = new Vector2(130,83);
                break;
        }
    }

    private Sprite GetSprite(LimitRewordType type,bool max)
    {
        switch (type)
        {
            case LimitRewordType.Coins:
                if(max)
                    return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Coin2");
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Coin1");
            case LimitRewordType.Butterfly:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_Butterfly");
            case LimitRewordType.Tipstool:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Tips");
            case LimitRewordType.AutoComplete:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("rocket");
            case LimitRewordType.Min5Double:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Mintool");
            case LimitRewordType.Min15Double:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Mintool");
            case LimitRewordType.Pupas:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("limit_pupas");
               
        }

        return null;
    }
    
    public void ShowComplete(int childcount)
    {
        if (Curlimitdata == null) return;

        // 最后一个灯笼永远不打勾：它承载最后两段，最后一段是「幸运循环」，
        // 完成后只重置该段进度继续可领（状态机见 LimitTimeManager.EnterLuckyLoop）。
        if (IsLastLanternItem())
        {
            LimitDataItem lastItem = LimitTimeManager.Instance.GetLimitItem(LimitTimeManager.Instance.GetLastLimitId());
            // 倒数第二段（id 10）完成 → 这个灯笼换成最后一段的奖励显示
            if (lastItem != null && lastItem.id != Curlimitdata.id)
            {
                SetUI(lastItem);
            }
            return;
        }

        for (int i = 0; i < childcount && i < rewardList.Count; i++)
        {
            rewardList[i].transform.DOScale(Vector3.zero, 0.4f).OnComplete(() =>
            {
                //if (i ==1)
                {
                    lightImage.gameObject.SetActive(true);
                    gouImage.transform.DOScale(Vector3.one, 0.4f);
                    if (Curlimitdata.id >= 1)
                    {
                        jianImage.sprite =  AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("anjian");
                        jianImage.SetNativeSize();
                    }
                }
            });
        }
        
    }

    /// <summary>
    /// 是否为「最后一个灯笼」：它承载倒数第二段 + 最后一段（如 id 10 → 11）两段配置，
    /// 因此判定用 lastId - 1（id 10 与 11 都算）。
    /// </summary>
    private bool IsLastLanternItem()
    {
        if (Curlimitdata == null) return false;
        var items = LimitTimeManager.Instance.GetLimitItems();
        if (items == null || items.Count == 0) return false;
        return Curlimitdata.id >= items[items.Count - 1].id - 1;
    }

    /// <summary>
    /// 同步最后一个灯笼上的「幸运循环」标识（prefab 里的 huanObject / Text_huan）。
    /// 只有「最后一段 + 已进入幸运循环」时显示，其余情况隐藏。
    /// </summary>
    private void RefreshLuckyLoopSign(bool isLastStage)
    {
        if (huanObject == null) return;

        bool show = isLastStage && LimitTimeManager.Instance.IsLuckyLoop();
        huanObject.SetActive(show);
        if (!show) return;

        Text label = huanObject.GetComponentInChildren<Text>(true);
        if (label == null) return;

        string text = MultilingualManager.Instance.GetString("luckyLoop");
        // 多语言表没有该 key 时 GetString 会原样返回 key，此时保留 prefab 内置的「幸运循环」
        if (!string.IsNullOrEmpty(text) && text != "luckyLoop")
        {
            label.text = text;
        }
    }

    public void ShowReward(bool isPlaySound=true, Action callback=null)
    {
        var rewards = LimitTimeManager.Instance.GetEffectiveRewards(Curlimitdata);

        for (int i = 0; i < rewards.Count; i++)
        {
            ShowRewardAnim(i, callback,isPlaySound);
        }
    }

    private void ShowRewardAnim(int index,Action callback,bool isPlaySound=true)
    {
        var rewards = LimitTimeManager.Instance.GetEffectiveRewards(Curlimitdata);
        
        LimitRewordType type = (LimitRewordType)rewards[index][0];
        if (type == LimitRewordType.Coins)
        {
            UpdateRewardValue();
            callback?.Invoke();
            return;
        }
        
        rewardList[index].transform.localScale = Vector3.one;
        GameObject rewardObj = Instantiate(rewardList[index],transform);
        gouImage.transform.localScale=Vector3.zero;
        rewardObj.transform.SetAsLastSibling();
        rewardObj.transform.localPosition=new Vector3(0f,rewardObj.transform.localPosition.y+100f,0f);
        CanvasGroup canvas = rewardObj.GetComponent<CanvasGroup>();
        if (canvas == null)
        {
            canvas = rewardObj.AddComponent<CanvasGroup>();
        }
        canvas.alpha = 0f;
        rewardObj.transform.localScale = Vector3.zero;
        
        if (index == 0)
        {
            lightImage.gameObject.SetActive(true);
        }
        
        if(isPlaySound)
            AudioManager.Instance.PlaySoundEffect("limitGetReward");
        
        canvas.DOFade(1, 0.4f).OnComplete(() =>
        {
            rewardObj.transform.DOScale(new Vector3(1.1f,1.1f,1.1f), 0.4f).OnComplete(() =>
            {
                rewardObj.transform.DOScale(Vector3.one, 0.2f);
            });
            
            rewardList[index].transform.DOScale(Vector3.zero, 0.3f).OnComplete(() =>
            {
                if (index == 0)
                {
                    //AudioManager.Instance.PlaySoundEffect("limitTimeOver");
                    callback?.Invoke();
                }
                gouImage.transform.DOScale(Vector3.one, 0.3f);
                if (index == 0)
                {
                    //lightImage.gameObject.SetActive(true);
                    UpdateRewardValue();
                    if (Curlimitdata.id >= 1)
                    {
                        jianImage.sprite =  AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("anjian");
                        jianImage.SetNativeSize();
                    }
                }
            });
            
            canvas.DOFade(1, 0.6f).OnComplete(() =>
            {
                canvas.DOFade(0, 0.3f);
                rewardObj.transform.DOLocalMoveY(rewardObj.transform.localPosition.y+100, 0.3f);
            });
        });
    }
    

    public void UpdateRewardValue()
    {
        
        var rewards = LimitTimeManager.Instance.GetEffectiveRewards(Curlimitdata);

        for (int i = 0; i < rewards.Count; i++)
        {
            List<int> rlist = rewards[i];
            AddRewardValue(rlist,i);
        }
    }
    
    private void AddRewardValue(List<int> rlist,int rewardid)
    {
        LimitRewordType type = (LimitRewordType)rlist[0];
        string message = "限时奖励获得";
        switch (type)
        {
            case LimitRewordType.Coins:
                if (Curlimitdata.rewardContent.Count == 1)
                    lightImage.gameObject.SetActive(true);
                Image icon= rewardList[rewardid].GetComponentInChildren<Image>();
                CustomFlyInManager.Instance.FlyInGold(icon.transform ,() =>
                {
                    //if (Curlimitdata.rewardContent.Count == 1)
                    //{
                       ShowComplete(Curlimitdata.rewardContent.Count);
                        //AudioManager.Instance.PlaySoundEffect("limitTimeOver");
                    //}
                    GameDataManager.Instance.UserData.UpdateGold(rlist[1],true,true,message);
                    //NextLevelBtn.gameObject.SetActive(true);
                });
                break;
            case LimitRewordType.Butterfly:
                //GameDataManager.instance.UserData.toolInfo[103].count+=rlist[1];
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly, rlist[1],message);
                break;
            case LimitRewordType.Tipstool:
                //GameDataManager.instance.UserData.toolInfo[102].count+=rlist[1];
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, rlist[1],message);
                break;
            case LimitRewordType.AutoComplete:
                //GameDataManager.instance.UserData.toolInfo[101].count+=rlist[1];
                GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, rlist[1],message);
                break;
            case LimitRewordType.Min5Double:
                GameDataManager.Instance.UserData.UpdateLimitEndTime(5);
                GameDataManager.Instance.UserData.SendCurrencyEvent(1,"限时奖励5分钟翻倍",message);
                break;
            case LimitRewordType.Min15Double:
                GameDataManager.Instance.UserData.UpdateLimitEndTime(15);
                GameDataManager.Instance.UserData.SendCurrencyEvent(1,"限时奖励15分钟翻倍",message);
                break;
            case LimitRewordType.Pupas:
                int value = rlist[1];
            
                Image pupaicon= rewardList[rewardid].GetComponentInChildren<Image>();
                ButterfliesManager.Instance.AddObtainedPupaOnGamePanel(pupaicon.transform,value);
                GameDataManager.Instance.UserData.SendCurrencyEvent(value,"签到奖励蚕蛹",message);
                break;
            default:
                break;
        }
        
        ShowComplete(Curlimitdata.rewardContent.Count);
       
        EventDispatcher.instance.TriggerChangeGoldUI(0, false);
    }
    
}
