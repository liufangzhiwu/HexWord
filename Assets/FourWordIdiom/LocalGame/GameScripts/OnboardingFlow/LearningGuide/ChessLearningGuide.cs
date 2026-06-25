using System;
using System.Collections;
using System.Collections.Generic;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class ChessLearningGuide : UIWindow
{
    [SerializeField] private GameObject Background;  // 背景 
    [SerializeField] private GameObject DianShouTable; // 点击的手
    [SerializeField] private GameObject TipText;           // 提示的文本
    [SerializeField] private GameObject PropText;          //  道具文本
    [Header("新玩法内嵌面板组件")]
    [SerializeField] private GameObject _specialTipPanel;   // 新提示面板的根节点
    [SerializeField] private Button _specialTipBtn;         // 新提示面板的关闭按钮
    
    [SerializeField] private List<ChessView> chessViews = new List<ChessView>();
    [SerializeField] private List<BowlView> bowlViews = new List<BowlView>();
    private GameObject highlightedCollectionPoint; // 缓存第一步中被赋予高亮 Canvas 的树叶进度条对象
   
    // ✨ 新增：用于缓存 TipText 父节点的初始本地坐标
    private Vector3 _originalTipTextParentLocalPos;
    private bool _hasSavedTipTextParentPos = false;
    
    protected override void InitializeUIComponents()
    {
        base.InitializeUIComponents();
        _specialTipBtn.AddClickAction(()=>this.Close());
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
        // ✨ 1. 记录初始位置，并在每次刷新UI时先将其归位，防止污染其他教程的排版
        if (TipText != null && TipText.transform.parent != null)
        {
            if (!_hasSavedTipTextParentPos)
            {
                _originalTipTextParentLocalPos = TipText.transform.parent.localPosition;
                _hasSavedTipTextParentPos = true;
            }
            TipText.transform.parent.localPosition = _originalTipTextParentLocalPos;
        }
        
        if (_specialTipPanel != null) _specialTipPanel.SetActive(false);
        
        switch(ChessGuideSystem.Instance.toolSourceName)
        {
            case "FirstStage":
                string word = MultilingualManager.Instance.GetString($"GuidingTips0" + 1 , "pingzi");
                string targetLetter = "益"; // 默认兜底字
                if (ChessGuideSystem.Instance.TargetPuzzle != null && ChessGuideSystem.Instance.TargetPuzzle.Count > 0)
                {
                    // 优先从引导系统锁定的目标字块列表中取第一个字
                    targetLetter = ChessGuideSystem.Instance.TargetPuzzle[0].letter; 
                }
                else if (ChessGuideSystem.Instance.activeToolObject != null)
                {
                    // 备用方案：直接从挂载了小手的光标对象上取字
                    BowlView bowl = ChessGuideSystem.Instance.activeToolObject.GetComponent<BowlView>();
                    if (bowl != null) targetLetter = bowl.letter;
                }
                TipText.GetComponentInChildren<Text>().text = word.Replace("水", targetLetter);
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
                PropText.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString($"GuidingTips0" + 5,"pingzi");
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
            // 🌟 👇 新增：冰块玩法引导样式
            case "IceTutorial":
                _specialTipPanel.SetActive(true); // 亮起内嵌的小面板
                _specialTipPanel.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ClearIceRule", "pingzi");
                Background.SetActive(true);
                TipText.SetActive(false);
                DianShouTable.SetActive(false); // 让手亮起指向被冻住的格子
                PropText.SetActive(false);
                break;

            // 🌟 👇 新增：花朵玩法引导样式
            case "FlowerTutorial":
                _specialTipPanel.SetActive(true);
                _specialTipPanel.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ClearFlowerRule", "pingzi");
                Background.SetActive(true);
                TipText.SetActive(false);
                DianShouTable.SetActive(false); // 让手亮起指向带有花骨朵的格子
                PropText.SetActive(false);
                break;

            // 🌟 新增：树叶引导第一步样式（无确认按钮，显现文字与手指，强制填字）
            case "LeafTutorialStep1":
                _specialTipPanel.SetActive(false); // 彻底隐藏带有关闭/我知道了按钮的面板
                Background.SetActive(true);
                TipText.SetActive(true);
                string baseStr = MultilingualManager.Instance.GetString("ButterflyRule","pingzi");
                TipText.GetComponentInChildren<Text>().text = string.Format(baseStr, ChessGuideSystem.Instance.targetPhrase);
                DianShouTable.SetActive(false);    // 唤醒指引小手
                PropText.SetActive(false);
                TipText.transform.localPosition = new Vector3(
                        _originalTipTextParentLocalPos.x, 
                        _originalTipTextParentLocalPos.y + 100f, 
                        _originalTipTextParentLocalPos.z);
                
                break;

            // 🌟 新增：树叶引导第二步样式（隐藏手指，显现带有确认按钮的内嵌面板提示去收集更多）
            case "LeafTutorialStep2":
                _specialTipPanel.SetActive(true);  // 亮起内嵌的小面板，展现确认关闭按钮
                _specialTipPanel.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ButterflyPraise","pingzi");
                Background.SetActive(true);
                TipText.SetActive(false);
                DianShouTable.SetActive(false);   // 隐藏手指
                PropText.SetActive(false);
                break;
            
            default:
                break;
        }

        if (_specialTipPanel.activeSelf)
        {
            _specialTipBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ButtonGotIt", "pingzi");
        }
    }      

    /// <summary>
    /// 处理提示词
    /// </summary>
    private IEnumerator ShowPuzzle()
    {
        bool isLeafStep2 = ChessGuideSystem.Instance.toolSourceName == "LeafTutorialStep2";
        // yield return new WaitForSeconds(0.4f);
        if (!isLeafStep2)
        {
            foreach (ChessView chessView in ChessGuideSystem.Instance.ChesspieceList)
            {
                // 🌟 1. 先让内部方法去设置它自己的状态，防止它覆盖我们后续的强制层级
                if (chessView.CurrState == TileState.Check || chessView.CurrState == TileState.Error)
                {
                    chessView.SetChoose(true, UIPanelLayer.TipsPanel);
                }
                Canvas canvas = chessView.GetComponent<Canvas>();
                if (canvas == null)
                {
                    canvas = chessView.gameObject.AddComponent<Canvas>();
                }

                GraphicRaycaster graphicRaycaster = chessView.GetComponent<GraphicRaycaster>();
                if (graphicRaycaster == null)
                {
                    graphicRaycaster = chessView.gameObject.AddComponent<GraphicRaycaster>();
                }
                canvas.overrideSorting = false;
                canvas.overrideSorting = true;
                canvas.sortingLayerName = UIPanelLayer.TipsPanel;
                canvas.sortingOrder = 1;
               
                string source = ChessGuideSystem.Instance.toolSourceName;
                if (source == "IceTutorial" || source == "FlowerTutorial" || source == "LeafTutorial")
                    graphicRaycaster.enabled = false;
                else
                    graphicRaycaster.enabled = (source == "ChessError");

                chessViews.Add(chessView);
            }

            yield return null;
            int index = 0;
            foreach (BowlView bowlView in ChessGuideSystem.Instance.TargetPuzzle)
            {
                if ((ChessGuideSystem.Instance.currentTutorial == 1 && index == 0) ||
                    ChessGuideSystem.Instance.toolSourceName == "LeafTutorialStep1")
                {
                    Canvas canvas = bowlView.GetComponent<Canvas>();
                    if (canvas == null)
                        canvas = bowlView.gameObject.AddComponent<Canvas>();
                    canvas.overrideSorting = true;
                    canvas.sortingLayerName = UIPanelLayer.TipsPanel;
                    canvas.sortingOrder = 1;
                    GraphicRaycaster gr = bowlView.GetComponent<GraphicRaycaster>();
                    if (gr == null)
                        gr = bowlView.gameObject.AddComponent<GraphicRaycaster>();

                    gr.enabled = true; // 确保可被点击
                }

                bowlViews.Add(bowlView);
                index++;
            }

            yield return null;
        }

        if ((ChessGuideSystem.Instance.toolSourceName == "LeafTutorialStep1" || isLeafStep2) && ChessGuideSystem.Instance.collectionPointObject != null)
        {
            highlightedCollectionPoint = ChessGuideSystem.Instance.collectionPointObject;
            Canvas cpCanvas = highlightedCollectionPoint.GetComponent<Canvas>();
            if (cpCanvas == null)
            {
                cpCanvas = highlightedCollectionPoint.AddComponent<Canvas>();
            }

            GraphicRaycaster cpRaycaster = highlightedCollectionPoint.GetComponent<GraphicRaycaster>();
            if (cpRaycaster == null)
            {
                cpRaycaster = highlightedCollectionPoint.AddComponent<GraphicRaycaster>();
            }
            cpCanvas.overrideSorting = true;
            cpCanvas.sortingLayerName = UIPanelLayer.TipsPanel;
            cpCanvas.sortingOrder = 1;
            cpRaycaster.enabled = false; // 运行第一步时锁定进度条自身点击，仅用于视觉高亮
        }
        // 🌟 如果属于特殊玩法，不需要小手指路，手势节点直接隐藏
        string src = ChessGuideSystem.Instance.toolSourceName;
        bool isSpecial = (src == "IceTutorial" || src == "FlowerTutorial" || src == "LeafTutorial");

        if (!isSpecial && ChessGuideSystem.Instance.activeToolObject != null)
        {
            MoveHandToTile(ChessGuideSystem.Instance.activeToolObject.transform);
        }
    }

    public void SetClickCallback()
    {
        // 🌟 终极修复：拦截底层广播的 "SetChess" (填字) 事件，处理树叶引导进度
        if (ChessGuideSystem.Instance.toolSourceName == "SetChess" && ChessGuideSystem.Instance.currentTutorial == 8)
        {
            // 1. 获取刚刚被玩家点击并飞入棋盘的字块
            if (ChessGuideSystem.Instance.activeToolObject != null)
            {
                BowlView clickedBowl = ChessGuideSystem.Instance.activeToolObject.GetComponent<BowlView>();
                if (clickedBowl != null)
                {
                    // 👇 🌟 核心修复：玩家点击后，立刻销毁该字块身上的提层组件，让它瞬间失去高亮！
                    if (clickedBowl.GetComponent<GraphicRaycaster>() != null)
                        Destroy(clickedBowl.GetComponent<GraphicRaycaster>());

                    if (clickedBowl.GetComponent<Canvas>() != null)
                        Destroy(clickedBowl.GetComponent<Canvas>());
                    
                    // 从目标追踪列表中移除它
                    bowlViews.Remove(clickedBowl);
                    ChessGuideSystem.Instance.TargetPuzzle.Remove(clickedBowl);
                }
            }

            // 2. 判定该词组是否还有其他空格没填完？
            if (ChessGuideSystem.Instance.TargetPuzzle.Count > 0)
            {
                // 词组还没填完（比如缺两个字，玩家刚填了一个）
                // 强行将状态名改回第一步，不要关闭界面，继续等待玩家填剩下的字！
                ChessGuideSystem.Instance.toolSourceName = "LeafTutorialStep1";
                return; 
            }
            else
            {
                // 所有的目标字都填完了！完美推进到第二步
                CleanChessViews(true); // 卸载第一步的穿透高亮组件，让大盘恢复正常视觉
            
                // ChessGuideSystem.Instance.currentTutorial = 9; 
                ChessGuideSystem.Instance.toolSourceName = "WaitLeafAnimation";
                // ChessGuideSystem.Instance.activeToolObject = null; // 第二步不需要小手引路
                GameDataManager.Instance.UserData.ChessTutorialProgress[8] = true;
                GameDataManager.Instance.CommitGameData();
                base.Close(); // 临时关闭界面，舞台交还给棋盘 // 唤醒第二步的 UI 面板（带确认按钮）
                return;
            }
        }
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
                if (ChessGuideSystem.Instance.activeToolObject != null)
                {
                    BowlView clickedBowl = ChessGuideSystem.Instance.activeToolObject.GetComponent<BowlView>();
                    if (clickedBowl != null) bowlViews.Remove(clickedBowl);
                }
           
                if (bowlViews.Count > 0)
                {
                    MoveHandToTile(bowlViews[0].transform);
                }
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
            foreach (var bowl in bowlViews)
            {
                if (bowl != null && bowl.GetComponent<GraphicRaycaster>() != null)
                {
                    bowl.GetComponent<GraphicRaycaster>().enabled = false;
                }
            }
            // 是错误的开始
            ChessGuideSystem.Instance.currentTutorial = 3;
            AnalyticMgr.GuideBegin(); 
            DianShouTable.SetActive(true);
            Background.SetActive(true);
            TipText.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString($"GuidingTips0" + 4,"pingzi");
           
            if (_hasSavedTipTextParentPos && TipText != null && TipText.transform.parent != null)
            {
                TipText.transform.parent.localPosition = _originalTipTextParentLocalPos;
            }
            ChessView chessView = ChessGuideSystem.Instance.ChesspieceList[0];
            int index = chessViews.IndexOf(chessView);
            if (chessView.CurrState == TileState.Check || chessView.CurrState == TileState.Error)
            {
                chessView.SetChoose(true, UIPanelLayer.TipsPanel);
            }
            Canvas canvas = chessView.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = chessView.gameObject.AddComponent<Canvas>();
            }
            GraphicRaycaster gr = chessView.GetComponent<GraphicRaycaster>();
            if (gr == null)
            {
                gr = chessView.gameObject.AddComponent<GraphicRaycaster>();
            }
            canvas.overrideSorting = false; // 强行关闭一次
            canvas.overrideSorting = true;
            canvas.sortingLayerName = UIPanelLayer.TipsPanel;
            canvas.sortingOrder = 1;
            canvas.enabled =true;
            gr.enabled = true;
            
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
    private void CleanChessViews(bool keepCollectionPoint = false)
    {
        List<ChessView> tempChessViews = new List<ChessView>(chessViews);
        foreach (ChessView chessView in tempChessViews)
        {
            // if (chessView.GetComponent<GraphicRaycaster>() != null)
            //     chessView.GetComponent<GraphicRaycaster>().enabled = true;
            //
            // if (chessView.GetComponent<Canvas>() != null)
            //     chessView.GetComponent<Canvas>().sortingLayerName = UIPanelLayer.BasePanel;
            if (chessView.GetComponent<GraphicRaycaster>() != null)
                Destroy(chessView.GetComponent<GraphicRaycaster>());

            if (chessView.GetComponent<Canvas>() != null)
                Destroy(chessView.GetComponent<Canvas>());

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
        
        if (!keepCollectionPoint && highlightedCollectionPoint != null)
        {
            if (highlightedCollectionPoint.GetComponent<GraphicRaycaster>() != null)
                Destroy(highlightedCollectionPoint.GetComponent<GraphicRaycaster>());

            if (highlightedCollectionPoint.GetComponent<Canvas>() != null)
                Destroy(highlightedCollectionPoint.GetComponent<Canvas>());
        
            highlightedCollectionPoint = null;
        }
    }
    
    private void OnCloseBtn()
    {
        if (_hasSavedTipTextParentPos)
        {
            TipText.transform.localPosition = _originalTipTextParentLocalPos;
        }
        
        if (string.IsNullOrEmpty(ChessGuideSystem.Instance.toolSourceName)) 
            return;
        if (ChessGuideSystem.Instance.toolSourceName == "WaitLeafAnimation") 
            return;
        // 🌟 核心防错修复：根据当前的模式名，精准把对应玩法的引导进度设为 true
        int currentGuideId = ChessGuideSystem.Instance.currentTutorial;
        string source = ChessGuideSystem.Instance.toolSourceName;
        if (source == "IceTutorial") currentGuideId = 6;
        else if (source == "FlowerTutorial") currentGuideId = 7;
        else if (source == "LeafTutorialStep1" || source == "LeafTutorialStep2") currentGuideId = 8;
        
        GameDataManager.Instance.UserData.ChessTutorialProgress[currentGuideId] = true; 
        GameDataManager.Instance.CommitGameData();
        AnalyticMgr.GuideComplete();
        CleanChessViews();
        ChessGuideSystem.Instance.CleanCurrentTutorial();
        
        if (EventDispatcher.instance != null)
        {
            EventDispatcher.instance.TriggerCheckShowChessTutorial();
        }
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
