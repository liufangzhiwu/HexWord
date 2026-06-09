using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ThemeItem : MonoBehaviour
{
    [SerializeField] private GameObject LockObject;
    [SerializeField] private GameObject unLockObject;
    [SerializeField] private Button themeButton;
    [SerializeField] private Text themeNameText;
    [SerializeField] private Image themeLight;
    [SerializeField] private Image themeIcon;
    public ThemeSaveItem curSaveItem;
    public ThemeDataItem curDataItem;
    
    private MyThemeScreen _myThemeScreen;

    private void Start()
    {
        InitButtonEvent();
    }

    private void InitButtonEvent()
    {
        themeButton.AddClickAction(OnClickThemeButton);
    }

    private void OnClickThemeButton()
    {
        foreach (var theme in _myThemeScreen._themeItems)
        {
            if (theme.curSaveItem != null)
            {
                theme.themeLight.gameObject.SetActive(false);
            }
        }
        
        themeLight.gameObject.SetActive(true);
        GameDataManager.Instance.UserData.userthemeid = curSaveItem.id;

        GameDataManager.Instance.UserData.UpdateThemeUseTimes(curSaveItem.id);

        GameCoreManager.Instance.ChangeBackgroundImage(themeIcon.sprite);
        
        SystemManager.Instance.HidePanel(PanelType.MyThemeScreen);
        GameDataManager.Instance.UserData.ischangetheme=true;
        int times=GameDataManager.Instance.UserData.ThemeItemUses[curSaveItem.id];
        AnalyticMgr.ThemeUse(GameDataManager.Instance.UserData.userthemeid,times);
    }

    public void SetUnlockUI(ThemeSaveItem themeSaveItem,MyThemeScreen myThemeScreen)
    {
        LockObject.SetActive(false);
        unLockObject.SetActive(true);
        
        curSaveItem=themeSaveItem;
        _myThemeScreen=myThemeScreen;
        if(themeSaveItem == null) return;
        curDataItem=ThemeManager.Instance.GetThemeDataItem(curSaveItem.id);
        
        string titleName= MultilingualManager.Instance.GetString( curDataItem.themeName, "hudie");
        themeNameText.text = titleName;
        themeIcon.sprite = GetSprite(curDataItem.iconName);
    }
    
    public void SetlockUI(MyThemeScreen myThemeScreen)
    {
        _myThemeScreen=myThemeScreen;
        LockObject.SetActive(true);
        unLockObject.SetActive(false);
    }
    

    private Sprite GetSprite(string spriteName)
    {
        return AssetBundleLoader.SharedInstance.GetSpriteFromAtlas(spriteName,"UI_Theme");
    }
   
    
}
