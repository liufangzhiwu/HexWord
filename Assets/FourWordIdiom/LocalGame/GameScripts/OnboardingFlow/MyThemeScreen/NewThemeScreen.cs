using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Middleware;
using UnityEngine.UIElements;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;
using Toggle = UnityEngine.UI.Toggle;

public class NewThemeScreen : UIWindow
{
    [SerializeField] private Button useButton; // 帮助按钮
    [SerializeField] private Button HideButton; // 关闭按钮
   
    [SerializeField] private Image themeImage;
    [SerializeField] private Text themeNameText;
    //[SerializeField] private Text tipsText; 
 
    public ThemeSaveItem curSaveItem;
    public ThemeDataItem curDataItem;

    protected void Start()
    {
       
    }

    protected override void OnEnable()
    {
        AudioManager.Instance.PlaySoundEffect("ShowUI");
      
        InitUI();
    }
    


    private void InitUI()
    {
        curSaveItem = GameDataManager.Instance.UserData.ThemeSaveItems[GameDataManager.Instance.UserData.ThemeSaveItems.Count-1];   
        curDataItem=ThemeManager.Instance.GetThemeDataItem(curSaveItem.id);
        
        string titleName= MultilingualManager.Instance.GetString( curDataItem.themeName, "hudie");
        themeNameText.text = titleName;
        themeImage.sprite = GetSprite(curDataItem.iconName);
    }
    
    protected override void InitializeUIComponents()
    {
        HideButton.AddClickAction(OnHideButton); 
        useButton.AddClickAction(OnClickUseButton);
    }

    private void OnClickUseButton()
    {
        GameDataManager.Instance.UserData.userthemeid = curSaveItem.id;
        GameDataManager.Instance.UserData.UpdateThemeUseTimes(curSaveItem.id);
        GameCoreManager.Instance.ChangeBackgroundImage(themeImage.sprite);
        GameDataManager.Instance.UserData.ischangetheme=true;
        int times=GameDataManager.Instance.UserData.ThemeItemUses[curSaveItem.id];
        AnalyticMgr.ThemeUse(GameDataManager.Instance.UserData.userthemeid,times);
        OnHideButton();
    }
    
    private Sprite GetSprite(string spriteName)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(spriteName,"UI_Theme");
    }



    private void OnHideButton()
    {
        ThemeManager.Instance.TriggerOnShowNewThemeBtnUI();
        
        base.Close(); // 隐藏面板
    }

}