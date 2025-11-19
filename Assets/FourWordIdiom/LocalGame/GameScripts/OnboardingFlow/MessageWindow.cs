using UnityEngine.UI;
using UnityEngine;
using System.Collections;

public class MessageWindow : UIWindow
{
    public Text StageText;
    public Text WindowsStageText;
    
    public Button OkButton;
    
    public GameObject ListObject;
    public GameObject WindowObject;
    
    
    protected override void InitializeUIComponents()
    {
        OkButton.AddClickAction(OnCloseBtn); // 绑定关闭按钮事件
        //adsbtn.AddClick(OnAdsBtn);
    }

    
    private void OnCloseBtn()
    {
        base.Close(); // 隐藏面板
        //UIManager.Instance.ShowPanel(PanelName.TopContainer);
    }
    
}