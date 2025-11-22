using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ChessLearningGuide : UIWindow
{
    [SerializeField] private GameObject Background;  // 背景 
    [SerializeField] private GameObject DianShouTable; // 点击的手
    [SerializeField] private GameObject TipText;           // 提示的文本
    [SerializeField] private GameObject PropText;          //  道具文本

    [SerializeField] private List<ChessView> chessViews = new List<ChessView>();
    [SerializeField] private List<BowlView> bowlViews = new List<BowlView>();
    // Start is called before the first frame update
    void Start()
    {
        // 初始化文本
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        RectTransform tool =  PropText.transform.parent.GetComponent<RectTransform>();
        if (UIUtilities.IsiPad())
        {
            tool.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UIUtilities.REFERENCE_WIDTH); // 1242px
        }
        else
        {
            float scale = UIUtilities.GetScreenRatio();
            if (scale < 0.85f)
            {
                tool.localScale = new Vector3(scale,scale,scale);
            }
            else if(scale > 1f)
            {
                tool.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UIUtilities.REFERENCE_WIDTH); // 1242px
            }
        }
        StartCoroutine(ShowPuzzle());
        //AudioManager.Instance.PlaySoundEffect("ShowUI");

        ShowUIStyle();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
    }

    private void ShowUIStyle()
    {
        switch(ChessGuideSystem.Instance.toolSourceName)
        {
            case "FirstStage":
                TipText.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString($"GuidingTips0" + 1 , "pingzi");
                TipText.SetActive(true);
                Background.SetActive(true);
                PropText.SetActive(false);
                break;
            case "SetChess":
                TipText.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString($"GuidingTips0" + 2 , "pingzi");
                Background.SetActive(false);
                break;
            case "UseTips":
                PropText.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString($"GuidingTips0" + 3, "pingzi");
                PropText.SetActive(true);
                DianShouTable.SetActive(false);
                Background.SetActive(false);
                TipText.SetActive(false);
                break;
            case "UseComplete":
                PropText.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString($"GuidingTips0" + 3,"pingzi");
                PropText.SetActive(true);
                DianShouTable.SetActive(false);
                Background.SetActive(false);
                TipText.SetActive(false);
                break;
            case "ChessError":
                TipText.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString($"GuidingTips0" + 4,"pingzi");
                TipText.SetActive(true);
                Background.SetActive(true);
                DianShouTable.SetActive(true);
                PropText.SetActive(false);
                break;
            default:
                break;
        }
    }      

    /// <summary>
    /// 处理提示词
    /// </summary>
    private IEnumerator ShowPuzzle()
    {
        foreach (ChessView chessView in ChessGuideSystem.Instance.ChesspieceList)
        {
            
            Canvas canvas = chessView.GetComponent<Canvas>();
            if(canvas == null)
            {
                canvas = chessView.AddComponent<Canvas>();
            }
            GraphicRaycaster graphicRaycaster = chessView.GetComponent<GraphicRaycaster>();
            if (graphicRaycaster == null)
            {
                graphicRaycaster = chessView.AddComponent<GraphicRaycaster>();
            }
            canvas.overrideSorting = true;
            canvas.sortingLayerName = UIPanelLayer.TipsPanel;
            canvas.sortingOrder = 1;
            if (chessView.CurrState == TileState.Check)
            {
                chessView.SetChoose(true, UIPanelLayer.TipsPanel);
            }
            if(ChessGuideSystem.Instance.toolSourceName != "ChessError")
                graphicRaycaster.enabled = false;
            else
                graphicRaycaster.enabled = true;

            chessViews.Add(chessView);
        }
        yield return null;

        int index = 0;
        foreach (BowlView bowlView in ChessGuideSystem.Instance.TargetPuzzle)
        {
            if (ChessGuideSystem.Instance.currentTutorial == 1 && index == 0)
            {
                Canvas canvas = bowlView.GetComponent<Canvas>();
                if (canvas == null)
                    canvas = bowlView.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingLayerName = UIPanelLayer.TipsPanel;
                canvas.sortingOrder = 1;
                if(bowlView.GetComponent<GraphicRaycaster>() == null)
                    bowlView.AddComponent<GraphicRaycaster>();
            }
            bowlViews.Add(bowlView);
            index++;
        }
        yield return null;

        // if(bowlViews.Count > 0)
        //     MoveHandToTile(bowlViews[0].transform);
        
        MoveHandToTile(ChessGuideSystem.Instance.activeToolObject.transform);
    }

    public void SetClickCallback()
    {
        if (ChessGuideSystem.Instance.toolSourceName == "SetChess" )
        { // 正确点击了
            if(ChessGuideSystem.Instance.currentTutorial == 1)
            {
                GameDataManager.Instance.UserData.ChessTutorialProgress[1] = true;
                AnalyticMgr.GuideComplete();
                ChessGuideSystem.Instance.currentTutorial = 2;
                AnalyticMgr.GuideBegin();
                Background.SetActive(false);
                TipText.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString($"GuidingTips0" + 2,"pingzi");
                bowlViews.Remove(ChessGuideSystem.Instance.activeToolObject.GetComponent<BowlView>());
                MoveHandToTile(bowlViews[0].transform);
                DianShouTable.SetActive(false);
            }
            else
            {
                this.Close();
            }
        }
        else if (ChessGuideSystem.Instance.toolSourceName == "ChessError")
        { 
            // 触发关卡教程重叠，先上报关卡教程
            int stage = ChessStageController.Instance.CurrentStage;
            if (stage == 1 || stage == 2 || stage == 5)
            {
                AnalyticMgr.GuideComplete();
            }
            
            // 是错误的开始
            ChessGuideSystem.Instance.currentTutorial = 3;
            AnalyticMgr.GuideBegin(); 
            DianShouTable.SetActive(true);
            Background.SetActive(true);
            TipText.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString($"GuidingTips0" + 4,"pingzi");
            ChessView chessView = ChessGuideSystem.Instance.ChesspieceList[0];
            int index = chessViews.IndexOf(chessView);
            Canvas canvas = chessView.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = chessView.AddComponent<Canvas>();
            }
            GraphicRaycaster gr = chessView.GetComponent<GraphicRaycaster>();
            if (gr == null)
            {
                gr = chessView.AddComponent<GraphicRaycaster>();
            }
            canvas.overrideSorting = true;
            canvas.sortingLayerName = UIPanelLayer.TipsPanel;
            canvas.sortingOrder = 1;
            canvas.enabled =true;
            gr.enabled = true;
            if (chessView.CurrState == TileState.Check)
            {
                chessView.SetChoose(true, UIPanelLayer.TipsPanel);
            }

            TipText.gameObject.SetActive(true);
            PropText.gameObject.SetActive(false);

            // 先清空再添加，防止重复添加
            if(index >=0 && index < chessViews.Count)
                chessViews[index] = chessView;
            else
                chessViews.Add(chessView);
            

            // Debug.Log("添加格子完"+ chessViews.Count);
            // Debug.Log("移动手到格子 " + ChessGuideSystem.Instance.activeToolObject.name);
            MoveHandToTile(ChessGuideSystem.Instance.activeToolObject.transform);
        }
        else if (ChessGuideSystem.Instance.toolSourceName == "ClickChess")
        {
            // 点击了错误词
            this.Close();
        }else if (ChessGuideSystem.Instance.toolSourceName == "UseTips")
        {
            DianShouTable.SetActive(true);
            MoveHandToTile(ChessGuideSystem.Instance.activeToolObject.transform);
        }
    }

    /// <summary>
    /// 移动手到目标位置
    /// </summary>
    public void MoveHandToTile(Transform transform)
    {
        if (transform == null) return;

        DianShouTable.GetComponent<Canvas>().sortingLayerName = UIPanelLayer.TipsPanel;
        // RectTransform movingRect = DianShouTable.GetComponent<RectTransform>();
        RectTransform targetRect = transform.GetComponent<RectTransform>();

        // 获取目标物体的四个世界坐标角落
        Vector3[] targetCorners = new Vector3[4];
        targetRect.GetWorldCorners(targetCorners);
        //for(int i = 0; i < targetCorners.Length; i++)
        //{
        //    Debug.Log($"目标的坐标 {i}: " + targetCorners[i]);
        //}
        // 直接使用目标物体的右下角坐标
        Vector3 targetBottomRight = targetCorners[3];
        // 将移动物体直接设置到目标位置
        DianShouTable.transform.position = targetBottomRight;
    }
    private void CleanChessViews()
    {
        foreach (ChessView chessView in chessViews)
        {
            if (chessView.GetComponent<GraphicRaycaster>() != null)
                chessView.GetComponent<GraphicRaycaster>().enabled = true;

            if (chessView.GetComponent<Canvas>() != null)
                chessView.GetComponent<Canvas>().sortingLayerName = UIPanelLayer.BasePanel;

            // Debug.Log("关闭时清理格子 " + chessView.Answer);
        }

        chessViews.Clear();
        foreach (BowlView bowlview in bowlViews)
        {
           
            if (bowlview.GetComponent<GraphicRaycaster>() != null)
                Destroy(bowlview.GetComponent<GraphicRaycaster>());

            if (bowlview.GetComponent<Canvas>() != null)
                Destroy(bowlview.GetComponent<Canvas>());
            
            // Debug.Log("关闭时清理词块 " + bowlview.letter);
        }
        bowlViews.Clear();
    }
    
    private void OnCloseBtn()
    {
        GameDataManager.Instance.UserData.ChessTutorialProgress[ChessGuideSystem.Instance.currentTutorial] = true; 
        AnalyticMgr.GuideComplete();
        CleanChessViews();
        ChessGuideSystem.Instance.CleanCurrentTutorial();
    }

    public override void Close(CloseMethod method = CloseMethod.Default)
    {
        OnCloseBtn();
        base.Close(method);
    }

    protected override void OnDisable()
    {
        OnCloseBtn();
        base.OnDisable();
    }
}
