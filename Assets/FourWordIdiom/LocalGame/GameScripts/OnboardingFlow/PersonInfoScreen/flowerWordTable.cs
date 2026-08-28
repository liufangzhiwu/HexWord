using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class flowerWordTable : MonoBehaviour
{
    [SerializeField] private Image curlevelImage;
    [SerializeField] private Image maxlevelImage;
    
    [SerializeField] private Text mullitterTitleText;
    [SerializeField] private Text mulcurlevelText;
    [SerializeField] private Text mulmaxlevelText;
    
    [SerializeField] private Text curlevelText;
    [SerializeField] private Text maxlevelText;
  
    
    // Start is called before the first frame update
    private void Start()
    {
        mullitterTitleText.text = MultilingualManager.Instance.GetString("MeditationList");
        mulcurlevelText.text=MultilingualManager.Instance.GetString("LevelReached","hudie");
        mulmaxlevelText.text=MultilingualManager.Instance.GetString("MaxLevel","hudie");
    }

    private void OnEnable()
    {
        
    }

    public void InitMyUI()
    {
        string zenName = MultilingualManager.Instance.GetString(GameDataManager.Instance.UserData.Zenlevel);
        string maxzenName = MultilingualManager.Instance.GetString(GameCoreManager.Instance.userProfile.highest_zen_level);
        
        string zenLevel = UIUtilities.ExtractNumber(GameDataManager.Instance.UserData.Zenlevel);
        Sprite zenIcon = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("zen"+zenLevel,"ZenRanks");
        
        string maxzenLevel = UIUtilities.ExtractNumber(GameCoreManager.Instance.userProfile.highest_zen_level);
        Sprite maxzenIcon = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("zen"+maxzenLevel,"ZenRanks");
       
         curlevelText.text = zenName;;
         maxlevelText.text=maxzenName;
        
         curlevelImage.sprite=zenIcon;
         maxlevelImage.sprite=maxzenIcon;
     
    }
    
    public void InitOtherUI()
    {
        string zenName = MultilingualManager.Instance.GetString(GameCoreManager.Instance.otherPersonProfile.zen_level);
        string maxzenName = MultilingualManager.Instance.GetString(GameCoreManager.Instance.otherPersonProfile.highest_zen_level);
        
        string zenLevel = UIUtilities.ExtractNumber(GameCoreManager.Instance.otherPersonProfile.zen_level);
        Sprite zenIcon = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("zen"+zenLevel,"ZenRanks");
        
        string maxzenLevel = UIUtilities.ExtractNumber(GameCoreManager.Instance.otherPersonProfile.highest_zen_level);
        Sprite maxzenIcon = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("zen"+maxzenLevel,"ZenRanks");
       
        curlevelText.text = zenName;;
        maxlevelText.text=maxzenName;
        
        curlevelImage.sprite=zenIcon;
        maxlevelImage.sprite=maxzenIcon;
     
    }
   
}
