using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class AchieveItem : MonoBehaviour
{
    [SerializeField] private Image progressImage;
    [SerializeField] private Image achieveicon;
    
    [SerializeField] private Text achieveTitle;
    [SerializeField] private Text achieveDes;
    [SerializeField] private Text achieveProgress;

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
        
        float progress = achieveSaveData.progressvalue/(float)achieveDataItem.needValue;
        int curprogress = (achieveSaveData.progressvalue * 100) / achieveDataItem.needValue;
        
        achieveProgress.text="已完成"+curprogress+"%";
        progressImage.fillAmount = progress;
        achieveicon.sprite =LoadheadIcon(achieveDataItem.achieveIcon);
    }
    
    
    private Sprite LoadheadIcon(string showIcon)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon,"UserHeadIcons");
    }
   
}
