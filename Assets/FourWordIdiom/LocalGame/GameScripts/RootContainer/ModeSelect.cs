using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ModeSelect : UIWindow
{
    [Header("弹出窗组件")] 
    [SerializeField] private RectTransform popupPanel;
    [SerializeField] private Button backBtn;
    [SerializeField] private Button wordBtn;
    [SerializeField] private Button chessBtn;
    [SerializeField] private Button hexaBtn;
    [SerializeField] private Text titleText;
    //[SerializeField] private RectTransform content;

    private float slideDuration = 0.3f;
    private AnimationCurve sliderCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    private Vector2 hiddenPosition;
    private Vector2 shownPosition;
    // Start is called before the first frame update
    void Start()
    {
        titleText.text = MultilingualManager.Instance.GetString("EntranceUITitle");
    }

    protected override void InitializeUIComponents()
    {
        base.InitializeUIComponents();
        hiddenPosition = new Vector2(0, -2254f);
        shownPosition =  new Vector2(0f, -1000f);
        
        // 设置初始状态
        popupPanel.anchoredPosition = hiddenPosition;
        backBtn.gameObject.SetActive(false);
        
        //绑定背景点击事件
        backBtn.AddClickAction(ClosePopup);
        
        wordBtn.AddClickAction(()=> SelectMode((int)LevelType.BlockWord));
        chessBtn.AddClickAction(()=> SelectMode((int)LevelType.ChessWord));
        hexaBtn.AddClickAction(()=> SelectMode((int)LevelType.HexWord));

        InitDefaulLevelType();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
        OpenPopup();
    }

    private void InitDefaulLevelType()
    {
        wordBtn.transform.GetChild(1).gameObject.SetActive(false);
        chessBtn.transform.GetChild(1).gameObject.SetActive(false);
        hexaBtn.transform.GetChild(1).gameObject.SetActive(false);
        
        if (GameDataManager.Instance.UserData.levelMode == (int)LevelType.BlockWord)
        {
            hexaBtn.transform.SetAsFirstSibling();
        }
        else if (GameDataManager.Instance.UserData.levelMode == (int)LevelType.ChessWord)
        {
            chessBtn.transform.SetAsFirstSibling();
        }
        else if (GameDataManager.Instance.UserData.levelMode == (int)LevelType.HexWord)
        {
            hexaBtn.transform.SetAsFirstSibling();
        }
        
        int levelmode = GameDataManager.Instance.UserData.levelMode;
        
        switch ((LevelType)levelmode)
        {
            case LevelType.BlockWord:
            case LevelType.HexWord:
                hexaBtn.transform.GetChild(1).gameObject.SetActive(true);
                break;
            case LevelType.ChessWord:
                chessBtn.transform.GetChild(1).gameObject.SetActive(true);
                break;
        }
    }

    private void InitUI()
    {
        InitLevelType(LevelType.BlockWord);
        InitLevelType(LevelType.ChessWord);
        InitLevelType(LevelType.HexWord);
    }

    private void InitLevelType(LevelType levelType)
    {
        Button LevelBtn = null;
        int stage = 0;
        
        switch (levelType)
        {
            case LevelType.BlockWord:
            case LevelType.HexWord:
                LevelBtn = hexaBtn;
                stage = GameDataManager.Instance.UserData.CurrentHexStage;
                break;
            case LevelType.ChessWord:
                LevelBtn = chessBtn;
                stage = GameDataManager.Instance.UserData.CurrentChessStage;
                break;
        }
      
       
        Transform modeName = LevelBtn.transform.GetChild(0);
        Transform stageText = modeName.GetChild(0);
        
        string levelName = MultilingualManager.Instance.GetString("EntranceUIName0"+(int)levelType);
        
        // 填文字
        modeName.GetComponent<Text>().text = levelName;
        if (levelType != LevelType.HexWord)
        {
            stageText.GetComponent<Text>().text =
                $"{MultilingualManager.Instance.GetString("Level")} {stage}";
        }
    }

    private void SelectMode(int mode)
    {
        
            Button LevelBtn = null;
            Transform select = null;
            int stage = 0;
           
            hexaBtn.transform.GetChild(1).gameObject.SetActive(false);
            chessBtn.transform.GetChild(1).gameObject.SetActive(false);
        
            switch ((LevelType)mode)
            {
                case LevelType.BlockWord:
                case LevelType.HexWord:
                    LevelBtn = hexaBtn;
                    break;
                case LevelType.ChessWord:
                    LevelBtn = chessBtn;
                    break;
            }
            select= LevelBtn.transform.GetChild(1);
            // Debug.Log("当前选择的模式" + (mode+1));
            GameDataManager.Instance.UserData.levelMode = mode;
            select.gameObject.SetActive(true);
            GameDataManager.Instance.UserData.SaveData();
      
       
        ClosePopup();
    }

    private void OpenPopup()
    {
        backBtn.gameObject.SetActive(true);
        StartCoroutine(SlidePopup(true));
    }

    private void ClosePopup()
    {
        StartCoroutine(SlidePopup(false));
    }

    private IEnumerator SlidePopup(bool isOpen)
    {
        Vector2 startPos = popupPanel.anchoredPosition;
        Vector2 targetPos = isOpen ? shownPosition : hiddenPosition;

        float elapsedTime = 0f;

        while (elapsedTime < slideDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / slideDuration;
            float curveValue = sliderCurve.Evaluate(t);

            popupPanel.anchoredPosition = Vector2.Lerp(startPos, targetPos, curveValue);
            yield return null;
        }

        popupPanel.anchoredPosition = targetPos;

        if (!isOpen)
        {
            SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
            PrimaryInterface uiWindow = (PrimaryInterface)SystemManager.Instance.ShowPanel(PanelType.PrimaryInterface);
            uiWindow.PlayNameAnimationBool("IsCollapse", false);
            // uiWindow.PlayNameAnimationBool("");
            uiWindow.InitUI();
            SystemManager.Instance.HidePanel(PanelType.SelectMode);
        }
    }
}
