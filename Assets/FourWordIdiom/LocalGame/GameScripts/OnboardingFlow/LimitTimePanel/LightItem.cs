using System;
using System.Collections.Generic;
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
    private LimitDataItem Curlimitdata;
    
    public void SetUI(LimitDataItem limitdata)
    {
        Curlimitdata=limitdata;
        lightImage.gameObject.SetActive(false);
        if(limitdata == null) return;
        
        if (Curlimitdata.id <GameDataManager.instance.UserData.timerePuzzleid)
        {
            ShowComplete(limitdata.rewardContent.Count);
        }
        else
        {
            for (int i = 0; i < limitdata.rewardContent.Count; i++)
            {
                List<int> rlist = limitdata.rewardContent[i];
                ShowLighItemUI(rlist, limitdata.id, i);
            }
        }
    }

    private void ShowLighItemUI(List<int> rlist,int id,int rewardid)
    {
        LimitRewordType type = (LimitRewordType)rlist[0];
        Image icon=rewardList[rewardid].GetComponentInChildren<Image>();
        Text count=rewardList[rewardid].GetComponentInChildren<Text>();
        icon.sprite = GetSprite(type,id>=LimitTimeManager.instance.GetLimitItems().Count-1);
        //icon.SetNativeSize();
        rewardList[rewardid].transform.localScale = Vector3.one;
        gouImage.transform.localScale=Vector3.zero;
        if (Curlimitdata.id >= 1)
        {
            jianImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("jian");
            jianImage.SetNativeSize();
        }
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
            case LimitRewordType.Resettool:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Reset");
            case LimitRewordType.Min5Double:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Mintool");
            case LimitRewordType.Min15Double:
                return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Mintool");
               
        }

        return null;
    }
    
    public void ShowComplete(int childcount)
    {
        for (int i = 0; i < childcount; i++)
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

    public void ShowReward(bool isPlaySound=true, Action callback=null)
    {
        for (int i = 0; i < rewardList.Count; i++)
        {
            ShowRewardAnim(i, callback,isPlaySound);
        }
    }

    private void ShowRewardAnim(int index,Action callback,bool isPlaySound=true)
    {
        LimitRewordType type = (LimitRewordType)Curlimitdata.rewardContent[index][0];
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
        for (int i = 0; i < Curlimitdata.rewardContent.Count; i++)
        {
            List<int> rlist = Curlimitdata.rewardContent[i];
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
                    GameDataManager.instance.UserData.UpdateGold(rlist[1],true,true,message);
                    //NextLevelBtn.gameObject.SetActive(true);
                });
                break;
            case LimitRewordType.Butterfly:
                //GameDataManager.instance.UserData.toolInfo[103].count+=rlist[1];
                GameDataManager.instance.UserData.UpdateTool(LimitRewordType.Butterfly, rlist[1],message);
                break;
            case LimitRewordType.Tipstool:
                //GameDataManager.instance.UserData.toolInfo[102].count+=rlist[1];
                GameDataManager.instance.UserData.UpdateTool(LimitRewordType.Tipstool, rlist[1],message);
                break;
            case LimitRewordType.Resettool:
                //GameDataManager.instance.UserData.toolInfo[101].count+=rlist[1];
                GameDataManager.instance.UserData.UpdateTool(LimitRewordType.Resettool, rlist[1],message);
                break;
            case LimitRewordType.Min5Double:
                GameDataManager.instance.UserData.UpdateLimitEndTime(5);
                //GameDataManager.instance.UserData.SendCurrencyEvent(1,"限时奖励5分钟翻倍",message);
                break;
            case LimitRewordType.Min15Double:
                GameDataManager.instance.UserData.UpdateLimitEndTime(15);
                //GameDataManager.instance.UserData.SendCurrencyEvent(1,"限时奖励15分钟翻倍",message);
                break;
            default:
                break;
        }
    }
    
}
