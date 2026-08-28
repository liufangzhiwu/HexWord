using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PersonInfoScreen : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
   
    [SerializeField] private Text HeaderText; //标题文本
    [SerializeField] private NameInfoTable nameInfoTable; //标题文本
    [SerializeField] private FillWordTable fillWordTable; //标题文本
    [SerializeField] private flowerWordTable nflowerWordTable; //标题文本
    [SerializeField] private MonthRankTable monthRankTable; //标题文本
    
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
        
        nameInfoTable.InitMyUI();
        fillWordTable.InitMyUI();
        nflowerWordTable.InitMyUI();
        monthRankTable.InitMyUI();
    }

    protected override void InitializeUIComponents()
    {
        closeBtn.AddVibraClickAction(OnCloseBtn); // 绑定关闭按钮事件
    }

    
    private void OnCloseBtn()
    {
        base.Close(); // 隐藏面板
        // SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
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
