using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class FillWordTable : MonoBehaviour
{
    [SerializeField] private Text mullitterTitleText;
    [SerializeField] private Text mulLengthText;
    [SerializeField] private Text mulkonwText;
    [SerializeField] private Text mulFourWordText;
    [SerializeField] private Text mulNofourWordText;
    [SerializeField] public Text mullevelText;
    
    [SerializeField] private Text lengthCountText;
    [SerializeField] private Text fourWordText;
    [SerializeField] private Text nofourWordText;
    [SerializeField] private Text levelText;
    
    // Start is called before the first frame update
    private void Start()
    {
        mullitterTitleText.text=MultilingualManager.Instance.GetString("FillIdioms","hudie");
        mulLengthText.text=MultilingualManager.Instance.GetString("LongestCombo","hudie");
        mulkonwText.text=MultilingualManager.Instance.GetString("LearnedIdioms","hudie");
        
        if(mulFourWordText!=null)
            mulFourWordText.text=MultilingualManager.Instance.GetString("FourIdioms","hudie");
        if(mulNofourWordText!=null)
            mulNofourWordText.text=MultilingualManager.Instance.GetString("NonFourIdioms","hudie");
        if(mullevelText!=null)
            mullevelText.text=MultilingualManager.Instance.GetString("LevelProgress","hudie");
    }

    private void OnEnable()
    {
       
    }

    public void InitMyUI()
    {
        lengthCountText.text=GameDataManager.Instance.UserData.MaxComboCount.ToString();
        fourWordText.text=GameDataManager.Instance.UserData.fourWordCount.ToString();
        nofourWordText.text=GameDataManager.Instance.UserData.nofourWordCount.ToString();
    }
    
    public void InitOtherUI()
    {
        lengthCountText.text=GameCoreManager.Instance.otherPersonProfile.max_chess_combo.ToString();
        int findwordcount = GameCoreManager.Instance.otherPersonProfile.four_char_count+GameCoreManager.Instance.otherPersonProfile.other_char_count;
        fourWordText.text=findwordcount.ToString();
        levelText.text=GameCoreManager.Instance.otherPersonProfile.chess_stage;
    }
   
}
