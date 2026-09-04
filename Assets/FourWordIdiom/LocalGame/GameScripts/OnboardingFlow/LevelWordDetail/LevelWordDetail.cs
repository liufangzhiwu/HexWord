using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Middleware;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;


public class LevelWordDetail : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Button wordBookBtn; // 关闭按钮
    [SerializeField] private Button leftBtn; // 关闭按钮
    [SerializeField] private Button rightBtn; // 关闭按钮
    [SerializeField] private Text PageCount; // 页面文本
    [SerializeField] private Text HeadTitle;
    [SerializeField] private Transform wordsParent; // 词语父对象
    [SerializeField] private ScrollRect scrollRect; // 滚动视图
    [SerializeField] private ViewListMove viewList;
    
    private WordDetailTable _wordPrefab; // 词语预设
    public float width; //当前页面ID
    public int curPage; //当前页面ID
    private List<string> words = new List<string>(); // 词语集合
  
    protected override void OnEnable()
    {
        bool isVocabularyPuzzle = false;
        if(GameDataManager.Instance.UserData.levelMode == 1)
            isVocabularyPuzzle = StageHexController.Instance.PuzzleData.IsVocabularyPuzzle;
        else if (GameDataManager.Instance.UserData.levelMode == 2)
            isVocabularyPuzzle = ChessStageController.Instance.PuzzleData.IsVocabularyPuzzle;
        if (isVocabularyPuzzle)
        {               
            ShowVocabularyWords();
            HeadTitle.text = MultilingualManager.Instance.GetString("IdiomExplain", "pingzi");                
        }           

        // EventDispatcher.OnWordVocabularyStatus += UpdateWordVocabularyStatus;
        //EventManager.OnWordVocabularyStatus?.Invoke();

        bool isEnter = false;
        if (GameDataManager.Instance.UserData.levelMode == 1)
            isEnter = StageHexController.Instance.IsEnterPuzzle;
        else if (GameDataManager.Instance.UserData.levelMode == 2)
            isEnter = ChessStageController.Instance.IsEnterPuzzle;
        if (isEnter)
        {
            _windowAnimator?.Play("levelShow");
        }
        else
        {
            _windowAnimator?.Play("idle");
        }

        // UpdateWordVocabularyStatus();
        
         StartCoroutine(UpdateVisibleWords());
    }
    
    protected override void InitializeUIComponents()
    {
        _wordPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject(ToolUtil.GetLanguageBundle(), "item_levelWordDTable").GetComponent<WordDetailTable>();
        closeBtn.AddVibraClickAction(OnCloseBtn); // 绑定关闭按钮事件
        leftBtn.AddClickAction(()=>MovePage(true)); // 左
        rightBtn.AddClickAction(()=>MovePage(false)); // 右
        wordBookBtn.AddClickAction(ShowWordVocabulary); // 绑定关闭按钮事件
        
    }

    private void UpdateWordVocabularyStatus()
    {
        wordBookBtn.gameObject.SetActive(GameDataManager.Instance.UserData.isShowVocabulary);
    }

    private void ShowWordVocabulary()
    {
        // if (GameDataManager.Instance.UserData.levelMode == 1)
        //     StageController.Instance.IsEnterVocabulary = false;
        // else if (GameDataManager.Instance.UserData.levelMode == 2)
        //     ChessStageController.Instance.IsEnterVocabulary = false;
        
        SystemManager.Instance.ShowPanel(PanelType.LevelWordScreen);
        OnHideAnimationEnd();
    }

    private void ShowVocabularyWords()
    {
        List<string> wordsss = GameDataManager.Instance.UserData.GetWordVocabulary().LevelWords;
        Debug.Log("是否有值" + JsonConvert.SerializeObject(wordsss));
        foreach (var word in GameDataManager.Instance.UserData.GetWordVocabulary().LevelWords)
        {
            if (!words.Contains(word))
            {
                words.Add(word);
            }
        }
    }
    
    public void MovePage(bool isLeft)
    {
        if (isLeft)
        {
            if (curPage > 1)
            {
                curPage--;
                PageChange(true);
            }
            else
            {
                PageChange(true);
            }
        }
        else
        {
            if (curPage < words.Count)
            {
                curPage++;
                PageChange(false);
            }
            else
            {
                PageChange(false);
            }
        }
    }

    public void ParentMovePos(float x,bool isAnim=true)
    {
        if (isAnim)
            wordsParent.DOLocalMoveX(x, 0.2f);
        else
            wordsParent.localPosition = new Vector3(x,0,0);
    }

    
    public void PageChange(bool isLeftMove)
    {
        if (_wordPrefab==null) return;
        
        width = _wordPrefab.GetComponent<RectTransform>().rect.width;
        wordsParent.DOLocalMoveX( width* -(curPage-1), 0.2f);
        PageCount.text= curPage+"/"+ words.Count;
    }

    IEnumerator UpdateVisibleWords()
    {
        // yield return new WaitForSeconds(0.1f);
        width = _wordPrefab.GetComponent<RectTransform>().rect.width;
        if (GameDataManager.Instance.UserData.levelMode == 1)
        {
            curPage = StageHexController.Instance.PuzzleData.PageIndex;
        }else if (GameDataManager.Instance.UserData.levelMode == 2)
        {
            curPage = ChessStageController.Instance.PuzzleData.PageIndex;
        }
        Debug.Log("当前打开的words " + JsonConvert.SerializeObject(words));
        Debug.Log("滚动视图组件" + viewList);
        try
        {
            viewList.InitList(words);
        }
        catch (Exception e)
        {
            Debug.LogError($"异常类型：{e.GetType().Name}\n" +
                           $"消息：{e.Message}\n" +
                           $"堆栈：\n{e.StackTrace}");
    
            // 如果有内部异常，也一并打出
            if (e.InnerException != null)
                Debug.LogError($"内部异常：{e.InnerException}");
        }

        Debug.Log($"宽度： {width} - 当前页{curPage}" );
        ParentMovePos(width * -(curPage-1),false);
        Debug.Log("父物体？" + wordsParent.localPosition);
        PageCount.text= curPage+"/"+ words.Count;   
        Debug.Log("文本" + PageCount.text);

        yield return null;
    }


    private void OnCloseBtn()
    {
        words.Clear();
        base.Close(); // 隐藏面板
    }
    
    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        words.Clear();
        base.OnDisable();
        // EventDispatcher.OnWordVocabularyStatus -= UpdateWordVocabularyStatus;
        StageHexController.Instance.IsEnterPuzzle = false;
    }

}
