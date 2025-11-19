using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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
    [SerializeField] private WordDetailTable wordProfab; // 词语预设
    [SerializeField] private Transform wordsParent; // 词语父对象
    [SerializeField] private ScrollRect scrollRect; // 滚动视图
    [SerializeField] private ViewListMove viewList;
    
    public float width; //当前页面ID
    public int curPage; //当前页面ID
    private List<string> words = new List<string>(); // 词语集合
  
    protected override void OnEnable()
    {
        if (StageController.Instance.PuzzleData.IsVocabularyPuzzle)
        {               
            ShowVocabularyWords();
            HeadTitle.text = MultilingualManager.Instance.GetString("LevelWord");                
        }           
        UpdateVisibleWords();

        EventDispatcher.instance.OnWordVocabularyStatus += UpdateWordVocabularyStatus;
        //EventDispatcher.instance.OnWordVocabularyStatus?.Invoke();

        if (StageController.Instance.IsEnterPuzzle)
        {
            _windowAnimator.Play("levelShow");
        }
        else
        {
            _windowAnimator.Play("idle");
        }

        UpdateWordVocabularyStatus();
    }
    
    protected override void InitializeUIComponents()
    {
        closeBtn.AddClickAction(OnCloseBtn); // 绑定关闭按钮事件
        leftBtn.AddClickAction(()=>MovePage(true)); // 左
        rightBtn.AddClickAction(()=>MovePage(false)); // 右
        wordBookBtn.AddClickAction(ShowWordVocabulary); // 绑定关闭按钮事件
    }

    private void UpdateWordVocabularyStatus()
    {
        wordBookBtn.gameObject.SetActive(GameDataManager.instance.UserData.isShowVocabulary);
    }

    private void ShowWordVocabulary()
    {
        StageController.Instance.IsEnterVocabulary = false;
        SystemManager.Instance.ShowPanel(PanelType.WordVocabularyScreen);
        OnHideAnimationEnd();
    }

    private void ShowVocabularyWords()
    {
        foreach (var word in GameDataManager.instance.UserData.GetWordVocabulary().LevelWords)
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
        width = wordProfab.GetComponent<RectTransform>().rect.width;
        wordsParent.DOLocalMoveX( width* -(curPage-1), 0.2f);
        PageCount.text= curPage+"/"+ words.Count;
    }

    private void UpdateVisibleWords()
    {
        width = wordProfab.GetComponent<RectTransform>().rect.width;
        curPage = StageController.Instance.PuzzleData.PageIndex;
        viewList.InitList(words);
        ParentMovePos(width * -(curPage-1),false);
        PageCount.text= curPage+"/"+ words.Count;            
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
        EventDispatcher.instance.OnWordVocabularyStatus -= UpdateWordVocabularyStatus;
        StageController.Instance.IsEnterPuzzle = false;
    }

}
