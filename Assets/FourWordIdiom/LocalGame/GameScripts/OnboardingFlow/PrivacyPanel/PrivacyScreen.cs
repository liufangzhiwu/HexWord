using System.Collections;
using Middleware;
using UnityEngine;
using UnityEngine.UI;


public class PrivacyScreen : UIWindow
{               
    [AutoAssign] private Button btn_next; // 关闭按钮    
    [AutoAssign] private HyperlinkText txt_link;
    [AutoAssign] private Text txt_tip;
    [AutoAssign] private Text txt_next;

    protected override void InitializeUIComponents()
    {
        AutoAssign.AutoInject(this);
        btn_next.AddClickAction(OnClosePanel); // 绑定关闭按钮事件
    }

    protected void Start()
    {       
        //设置点击回调
        txt_link.onHyperlinkClick = OnClickText;
        InitLanguage();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("Entergame",1,0.1f);
    }
    
    private void InitLanguage()
    {
        txt_tip.text = MultilingualManager.Instance.GetString("PrivacyAgreement02");
        //txt_tip.text ="Hello \u00A0 World";
      
        string tiplink= MultilingualManager.Instance.GetString("PrivacyAgreement01");
        if (tiplink.Contains(" "))
        {
            tiplink = tiplink.Replace(" ", "\u00A0");
        }
        txt_link.text = tiplink;
        txt_next.text = MultilingualManager.Instance.GetString("PrivacyAgreement03");
    }

    
    private void OnClickText(string url)
    {
        Debug.Log("点击"+url);
        Application.OpenURL(url);
    }

    private void OnClosePanel()
    {
        //GameCoreManager.Instance.ShowGamePanel();
        ShowGamePanel();
        // 标记非首次进入
        GameDataManager.Instance.UserData.IsFirstLaunch = false;
        base.Close(); // 隐藏面板
    }
    
    private void ShowGamePanel()
    {
        
        if (GameDataManager.Instance.UserData.levelMode == (int)LevelType.BlockWord)
        {
            StageHexController.Instance.SetStageData(GameDataManager.Instance.UserData.CurrentHexStage);
            SystemManager.Instance.ShowPanel(PanelType.GamePlayArea);
        }
        
        if (GameDataManager.Instance.UserData.levelMode == (int)LevelType.HexWord)
        {
            StageHexController.Instance.SetStageData(GameDataManager.Instance.UserData.CurrentHexStage);
            SystemManager.Instance.ShowPanel(PanelType.HexGamePlayArea);
        }
        
        if (GameDataManager.Instance.UserData.levelMode == (int)LevelType.ChessWord)
        {
            ChessStageController.Instance.SetStageData(GameDataManager.Instance.UserData.CurrentChessStage);
            SystemManager.Instance.ShowPanel(PanelType.ChessPlayArea);
        }
    
    }
}
