using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ModeSelect : UIWindow
{
    [Header("弹出窗组件")] 
    [SerializeField] private RectTransform popupPanel;
    [SerializeField] private Button backBtn;
    [SerializeField] private Text titleText;
    [SerializeField] private RectTransform content;

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
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
        OpenPopup();
    }

    private void InitUI()
    {
        // 数据驱动：模式配置
        (string key, int stage)[] modes =
        {
            (MultilingualManager.Instance.GetString("EntranceUIName01"),        0),
            (MultilingualManager.Instance.GetString("EntranceUIName02"),       GameDataManager.Instance.UserData.CurrentChessStage),
            (MultilingualManager.Instance.GetString("EntranceUIName03"),        GameDataManager.Instance.UserData.CurrentHexStage)                           // 层层消无关卡号
        };
        
        int currentMode = GameDataManager.Instance.UserData.levelMode -1;
       
        for (int i = 0; i < content.childCount; i++)
        {
            Transform child = content.GetChild(i);
            if(i >= modes.Length) break;
            
            var (name,stage) = modes[i];

            Transform modeName = child.GetChild(0);
            Transform stageText = modeName.GetChild(0);
            Transform select = child.GetChild(1);
            
            // 填文字
            modeName.GetComponent<Text>().text = name;
            stageText.GetComponent<Text>().text =
                $"{MultilingualManager.Instance.GetString("Level")} {stage}";
            
            Button btn = child.GetComponent<Button>() ?? child.gameObject.AddComponent<Button>();
            int modeId = i;
            // Debug.Log("当前是第几个？" + modeId);
            btn.AddClickAction(()=> SelectMode(modeId));
            // 选中状态
            select.gameObject.SetActive(i == currentMode);
        }
    }

    private void SelectMode(int mode)
    {
        for (int i = 0; i < content.childCount; i++)
        {
            content.GetChild(i).GetChild(1).gameObject.SetActive(false);
        }
        content.GetChild(mode).GetChild(1).gameObject.SetActive(true);
        
        // Debug.Log("当前选择的模式" + (mode+1));
        GameDataManager.Instance.UserData.levelMode = mode + 1;
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
