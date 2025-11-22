using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class LearningGuide : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Button HideBtn; // 关闭按钮
    
    [SerializeField] private GameObject shushouObj; 
    [SerializeField] private GameObject dianshouTable; 
    [SerializeField] private GameObject hengshouTable; // 关闭按钮
    [SerializeField] private GameObject shushouTable; // 关闭按钮
    [SerializeField] private GameObject PuzzletipsText; // 关闭按钮
    [SerializeField] private GameObject ItemTable; // 关闭按钮
    [SerializeField] private Text tipsText; // 关闭按钮
    [SerializeField] private Text tooltipsText; // 关闭按钮

    private List<TileView> Puzzles=new List<TileView>();
    private List<GameObject> guidebuttons=new List<GameObject>();
    private DateTime startTime;
  
    protected override void OnEnable()
    {
        base.OnEnable();
      
        StartCoroutine(ShowPuzzle());
        ShowUIStyle();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        //FirebaseManager.Instance.TurorialBegin(GameDataManager.instance.UserData.tutorial);
        startTime=DateTime.Now;
    }
   

    private void ShowUIStyle()
    {
        //GameDataManager.instance.UserData.UpdateTutorialProgress();
        int id = GameDataManager.Instance.UserData.GetTutorialProgress()+1;
        string tips = MultilingualManager.Instance.GetString("GuidingTips0"+id);
        switch (id)
        {
            // case 1:
            //     //tips = "スライド熟語";
            //     PuzzletipsText.gameObject.SetActive(true);
            //     ItemTable.gameObject.SetActive(false);
            //     tipsText.text = tips + "<color=#E18129>\n「 " + GuideSystem.Instance.targetPuzzle + " 」</color>";
            //     dianshouTable.gameObject.SetActive(false);
            //     break;
            // case 2:
            //     //tips = "よくやった！別の熟語を探してください";
            //     PuzzletipsText.gameObject.SetActive(true);
            //     ItemTable.gameObject.SetActive(false);
            //     tipsText.text = tips + "<color=#E18129>\n「 " + GuideSystem.Instance.targetPuzzle + " 」</color>";
            //     dianshouTable.gameObject.SetActive(false);
            //     break;
            case 3:
                //tips = "ヒントツールをクリックすると、次のスライド可能な言葉が<color=#E18129>提示</color>されます。";
                PuzzletipsText.gameObject.SetActive(false);
                dianshouTable.gameObject.SetActive(true);
                ItemTable.gameObject.SetActive(true);
                shushouTable.gameObject.SetActive(false);
                hengshouTable.gameObject.SetActive(false);
                tooltipsText.text = tips;
                break;
            case 4:
                //tips = "<color=#E18129>リセット</color>ツールをクリックすると、残りのブロックを<color=#E18129>並べ替える</color>ことができます。";
                PuzzletipsText.gameObject.SetActive(false);
                dianshouTable.gameObject.SetActive(true);
                ItemTable.gameObject.SetActive(true);
                shushouTable.gameObject.SetActive(false);
                hengshouTable.gameObject.SetActive(false);
                tooltipsText.text = tips;
                break;           
        }

        if (id >2&&id<=4)
        {
            GameObject toolobj = GuideSystem.Instance.activeToolObject;
            GameObject PuzzleObj = Instantiate(toolobj);
            PuzzleObj.transform.SetParent(transform);
            PuzzleObj.transform.localScale=toolobj.transform.localScale;
            PuzzleObj.transform.position = toolobj.transform.position; // 设置为 PuzzleGrid 的位置
            dianshouTable.transform.parent = PuzzleObj.transform;
            dianshouTable.transform.position = PuzzleObj.transform.position;
            guidebuttons.Add(PuzzleObj);
            closeBtn.gameObject.SetActive(false);
            HideBtn.onClick.RemoveAllListeners();
            HideBtn.AddClickAction(OnCloseBtn);
            PuzzleObj.GetComponent<Button>().onClick.RemoveAllListeners();
            PuzzleObj.GetComponent<Button>().AddClickAction(OnClickOkButton);
        }
    }

    private void OnClickOkButton()
    {
        GameObject toolobj = GuideSystem.Instance.activeToolObject;
        toolobj.GetComponent<Button>().onClick?.Invoke();
        OnCloseBtn();
    }
    
    IEnumerator ShowPuzzle()
    {
        bool isheng = true;
        if (GameDataManager.Instance.UserData.GetTutorialProgress() <2)
        {
            foreach (var PuzzleGrid in GuideSystem.Instance.PuzzleGrids)
            {
                // 实例化字块
                GameObject PuzzleObj = PuzzleGrid.TileView.gameObject;
                Canvas canvas= PuzzleObj.AddComponent<Canvas>();
                PuzzleObj.AddComponent<GraphicRaycaster>();
                canvas.overrideSorting=true;
                canvas.sortingLayerName="TipsPanel";
                canvas.sortingOrder=1;
                Puzzles.Add(PuzzleObj.GetComponent<TileView>());
            }

            // if (GuideSystem.Instance.PuzzleGrids[0].Column == GuideSystem.Instance.PuzzleGrids[1].Column)
            // {
            //     isheng = false;
            // }
            
            if (GameDataManager.Instance.UserData.GetTutorialProgress()<=0)
            {
                shushouTable.gameObject.SetActive(false);
                yield return new WaitForSeconds(0.25f);
                hengshouTable.transform.localScale=new Vector3(1f,1f,1f);
                hengshouTable.gameObject.SetActive(true);
                hengshouTable.transform.position = Puzzles[0].transform.position;
                _windowAnimator.Play("hengAnim");
            }
            else
            {
                hengshouTable.gameObject.SetActive(false);
                yield return new WaitForSeconds(0.25f);
                shushouObj.transform.localScale=new Vector3(-1f,1f,1f);
                shushouTable.transform.localScale=new Vector3(-1f,1f,1f);
                shushouTable.gameObject.SetActive(true);
                shushouTable.transform.position = Puzzles[0].transform.position;
                _windowAnimator.Play("ShuAnim");
                
                
                // hengshouTable.gameObject.SetActive(false);
                // yield return new WaitForSeconds(0.4f);
                // shushouTable.gameObject.SetActive(true);
                // shushouTable.transform.position = Puzzles[0].transform.position;
                //
                // _windowAnimator.Play("ShuAnim");
               
            }
        }
    }

    private void OnCloseBtn()
    {
        //TimeSpan timeSpan = DateTime.Now.Subtract(startTime);
        //ThinkManager.instance.Event_CompleteGuide();
        hengshouTable.gameObject.SetActive(false);
        GameDataManager.Instance.UserData.UpdateTutorialProgress();
        OnHideAnimationEnd();
    }

    public override void Close(CloseMethod method = CloseMethod.Default)
    {
        base.Close(method);
        OnCloseBtn();
    }
    
    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        foreach (var PuzzleGrid in Puzzles)
        {
            GraphicRaycaster graphic= PuzzleGrid.GetComponent<GraphicRaycaster>();
            if(graphic!=null) Destroy(graphic);
            Canvas canvas = PuzzleGrid.GetComponent<Canvas>();
            if(canvas!=null) Destroy(canvas);
            
            //PuzzleGrid.gameObject.SetActive(false);
        }
        foreach (var bGuidebtn in guidebuttons)
        {
            bGuidebtn.SetActive(false);
        }
        dianshouTable.gameObject.SetActive(false);
        guidebuttons.Clear();
        Puzzles.Clear();
    }
}
