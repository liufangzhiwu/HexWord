using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PersonInfoScreen : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Button achieveBtn; // 成就按钮
   
    [SerializeField] private Text HeaderText; //标题文本
    
    protected void Start()
    {
       //InitHeadIconList();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
       
        //HeaderText.text = MultilingualManager.Instance.GetString("CharacterInfoTitle");
        
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,true);
    }

    protected override void InitializeUIComponents()
    {
        closeBtn.AddVibraClickAction(OnCloseBtn); // 绑定关闭按钮事件
        achieveBtn.AddClickAction(OnAchieveBtnBtn); // 绑定关闭按钮事件
    }
   
    private void OnAchieveBtnBtn()
    {
        SystemManager.Instance.ShowPanel(PanelType.AchievementScreen);
    }
    
    private void OnCloseBtn()
    {
        base.Close(); // 隐藏面板
    }
    
    private Sprite LoadheadIcon(string showIcon)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon);
    }
    
    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
    }
}
