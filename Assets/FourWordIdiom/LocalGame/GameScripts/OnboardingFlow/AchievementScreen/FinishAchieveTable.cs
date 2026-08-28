using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class FinishAchieveTable : MonoBehaviour
{
    [SerializeField] private Button finishButton;
    [SerializeField] private Image achieveicon;
    [SerializeField] private Image achieveProgressBar;
    [SerializeField] private Image gou;
   
    [SerializeField] private Text achieveDes;
    [SerializeField] private Text progressText;

    public AchieveSaveData achieveSaveData;
    public AchieveDataItem achieveDataItem;
    private AchieveDataItem OldachieveDataItem;
    
    // Start is called before the first frame update
    private void Start()
    {
        // mullitterTitleText.text=MultilingualManager.Instance.GetString("FillIdioms","hudie");
        // mulcurlevelText.text=MultilingualManager.Instance.GetString("LongestCombo","hudie");
        // mulmaxlevelText.text=MultilingualManager.Instance.GetString("LearnedIdioms","hudie");
        finishButton.AddClickAction(ClickFinishButton);
    }
    
    public void SetTaskData(AchieveSaveData data)
    {
        achieveSaveData = data;
        achieveDataItem = AchievementManager.Instance.GetAchieveItemById((AchieveType)data.achieveTypeId);
        InitUI();
    }

    private void ClickFinishButton()
    {
        SystemManager.Instance.ShowPanel(PanelType.AchievementScreen);
        
        AchievementManager.Instance.DisableFinishAchieveTable();
    }

    public void InitUI()
    {
        if(achieveDataItem==null) return;
      
        achieveDes.text =
            string.Format(MultilingualManager.Instance.GetString(achieveDataItem.achieveTip, "hudie"),
                achieveDataItem.needValue);
      
        achieveicon.sprite =LoadheadIcon(achieveDataItem.achieveIcon);

        StartCoroutine(LoadProgressAnimation());
    }

    IEnumerator LoadProgressAnimation()
    {
        achieveProgressBar.fillAmount = 0;
        OldachieveDataItem = AchievementManager.Instance.GetAchieveItemById((AchieveType)achieveDataItem.id-1);
        int startValue = 0;
        if (OldachieveDataItem != null && OldachieveDataItem.achieveTip == achieveDataItem.achieveTip)
        {
            startValue = achieveDataItem.needValue-OldachieveDataItem.needValue;
        }

        yield return new WaitForSeconds(0.5f);
        progressText.text = startValue + "/"+achieveDataItem.needValue;
        achieveProgressBar.DOFillAmount(1f, 0.5f);
        
        int targetValue = achieveDataItem.needValue;
        float duration = 0.35f; // 动画持续时间
        float elapsed = 0f;
        // 记录原本的缩放大小（防止多次调用导致大小错乱）
        // Vector3 originalScale = Goldtxt.transform.localScale;
        // 设置最大放大倍数（比如 0.5f 代表最大会放大到 1.5 倍）
        float maxScaleAmount = 0.5f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration); // 归一化
            // 1. 处理数字滚动
            int currentValue = Mathf.RoundToInt(Mathf.Lerp(startValue, targetValue, t));
            progressText.text = currentValue+ "/"+achieveDataItem.needValue;
            yield return null;
        }
        progressText.text = achieveDataItem.needValue + "/"+achieveDataItem.needValue;
    }
    
    private Sprite LoadheadIcon(string showIcon)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon,"UserHeadIcons");
    }
   
}
