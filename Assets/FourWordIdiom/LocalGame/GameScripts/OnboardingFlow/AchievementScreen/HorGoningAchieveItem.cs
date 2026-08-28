using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class HorGoningAchieveItem : MonoBehaviour
{
    [SerializeField] private Image progressImage;
    [SerializeField] private Image achieveicon;
    [SerializeField] private Image achievetag;
    
    [SerializeField] private Text achieveTitle;
    [SerializeField] private Text achieveDes;
    [SerializeField] private Text achieveProgress;
    [SerializeField] private Text achievejiliText;
    [SerializeField] private Text achievetagText;

    public AchieveSaveData achieveSaveData;
    public AchieveDataItem achieveDataItem;
    
    // Start is called before the first frame update
    private void Start()
    {
        // mullitterTitleText.text=MultilingualManager.Instance.GetString("FillIdioms","hudie");
        // mulcurlevelText.text=MultilingualManager.Instance.GetString("LongestCombo","hudie");
        // mulmaxlevelText.text=MultilingualManager.Instance.GetString("LearnedIdioms","hudie");
    }
    
    public void SetTaskData(AchieveSaveData data)
    {
        achieveSaveData = data;
        achieveDataItem = AchievementManager.Instance.GetAchieveItemById((AchieveType)data.achieveTypeId);
        InitUI();
    }


    public void InitUI()
    {
        if(achieveDataItem==null) return;
        
        achieveTitle.text=MultilingualManager.Instance.GetString(achieveDataItem.achieveName,"hudie");
        achieveDes.text =
            string.Format(MultilingualManager.Instance.GetString(achieveDataItem.achieveTip, "hudie"),
                achieveDataItem.needValue);
        
        float progress = (float)achieveSaveData.progressvalue/achieveDataItem.needValue;
        int curprogress = (achieveSaveData.progressvalue * 100) / achieveDataItem.needValue;
        
        achieveProgress.text="已完成"+curprogress+"%";
        progressImage.fillAmount = progress;
        achieveicon.sprite =LoadheadIcon(achieveDataItem.achieveIcon);
        
        char str = achieveDataItem.achieveIcon[achieveDataItem.achieveIcon.Length - 1];
        int index=int.Parse(str.ToString());
        string tag = "achievetag" + index;
        
        achievetag.sprite =LoadheadIcon(tag);
        
        string labelkey = "Ordinary";

        switch (index)
        {
            case 1:
                labelkey="Ordinary";
                break;
            case 2:
                labelkey="Rare";
                break;
            case 3:
                labelkey="Infrequent";
                break;
        }
        
        ThreelevelTagItem threelevelTagItem = AchievementManager.Instance.GetIncentiveThreeLevelData(labelkey);
        float value = AchievementManager.Instance.GetIncentiveValueByCurrentDate(labelkey);
        string des = MultilingualManager.Instance.GetString(threelevelTagItem.longText,"hudie");
        
        achievejiliText.text =string.Format(des,value);
        achievetagText.text =MultilingualManager.Instance.GetString(tag,"hudie");
    }
    
    
    private Sprite LoadheadIcon(string showIcon)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon,"UserHeadIcons");
    }
   
}
