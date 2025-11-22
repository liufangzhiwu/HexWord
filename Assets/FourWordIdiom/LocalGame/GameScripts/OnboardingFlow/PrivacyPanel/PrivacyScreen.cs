using System.Collections;

using UnityEngine;
using UnityEngine.UI;


public class PrivacyScreen : UIWindow
{               
    [SerializeField] private Button nextBtn; // 关闭按钮    
    [SerializeField] private HyperlinkText linkText;
    [SerializeField] private Text tip_Text;        

    protected void Start()
    {       
        //设置点击回调
        linkText.onHyperlinkClick = OnClickText;
        //StartCoroutine(AddVisibleBound());
        //InitLanguage();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
    }

    //private void InitLanguage()
    //{
    //    tip_Text.text = LanguageManager.Instance.GetString("PrivacyAgreement02");
    //    linkText.text = LanguageManager.Instance.GetString("PrivacyAgreement01");
    //    nextBtn.GetComponentInChildren<Text>().text = LanguageManager.Instance.GetString("PrivacyAgreement03");
    //}

   

    protected override void InitializeUIComponents()
    {
        nextBtn.AddClickAction(OnClosePanel); // 绑定关闭按钮事件
    }

    IEnumerator AddVisibleBound()
    {
        yield return null;
        //linkText.AddVisibleBound();
    }

    void OnClickText(string url)
    {
        Debug.Log("点击"+url);
        Application.OpenURL(url);
    }

    private void OnClosePanel()
    {
        //GameCoreManager.Instance.ShowGamePanel();
        ShowGamePanel();
        base.Close(); // 隐藏面板
    }
    
    public void ShowGamePanel()
    {
        StageHexController.Instance.SetStageData(GameDataManager.Instance.UserData.CurrentHexStage);
        SystemManager.Instance.ShowPanel(PanelType.GamePlayArea);
    }

    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();      
        //AdjustManager.Instance.InitAdjust();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
    }
}
