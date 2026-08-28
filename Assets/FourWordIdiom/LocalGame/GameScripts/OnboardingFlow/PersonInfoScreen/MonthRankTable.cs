using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class MonthRankTable : MonoBehaviour
{
    [SerializeField] private Text mullitterTitleText;

    [SerializeField] private Text goldGetCountText;
    [SerializeField] private Text silverGetCountText;
    [SerializeField] private Text bronzeGetCountText;
    
    // Start is called before the first frame update
    private void Start()
    {
        // mullitterTitleText.text=MultilingualManager.Instance.GetString("FillIdioms","hudie");
        // mulcurlevelText.text=MultilingualManager.Instance.GetString("LongestCombo","hudie");
        // mulmaxlevelText.text=MultilingualManager.Instance.GetString("LearnedIdioms","hudie");
    }

    private void OnEnable()
    {
       
    }

    public void InitMyUI()
    {
        goldGetCountText.text=GameCoreManager.Instance.userProfile.hof_awards.gold.ToString();
        silverGetCountText.text=GameCoreManager.Instance.userProfile.hof_awards.silver.ToString();
        
        bronzeGetCountText.text=GameCoreManager.Instance.userProfile.hof_awards.bronze.ToString();
     
    }
    
    public void InitOtherUI()
    {
        goldGetCountText.text=GameCoreManager.Instance.otherPersonProfile.hof_awards.gold.ToString();
        silverGetCountText.text=GameCoreManager.Instance.otherPersonProfile.hof_awards.silver.ToString();
        
        bronzeGetCountText.text=GameCoreManager.Instance.otherPersonProfile.hof_awards.bronze.ToString();
     
    }
   
}
