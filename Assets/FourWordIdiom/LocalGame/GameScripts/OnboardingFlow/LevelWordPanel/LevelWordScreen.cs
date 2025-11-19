using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;


public class LevelWordScreen : UIWindow
{        
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Button VocabularyBtn;
    [SerializeField] private Text headTitle;
    [SerializeField] private WordButton wordLevelBtn; // 词语预设
    [SerializeField] private Transform wordsVocabularyParent; // 词语父对象
    private ObjectPool objectPool; // 对象池实例
    private Dictionary<string, WordButton> WordVocabularys = new Dictionary<string, WordButton>();
    //private Dictionary<string, WordButton> NoteBooks = new Dictionary<string, WordButton>();
    
    protected override void Awake()
    {
        base.Awake();
        // 初始化对象池
        objectPool = new ObjectPool(wordLevelBtn.gameObject, ObjectPool.CreatePoolContainer(transform, "WordPool"));
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ShowLevelWord();
        headTitle.text = MultilingualManager.Instance.GetString("LevelWord");
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        VocabularyBtn.gameObject.SetActive(GameDataManager.instance.UserData.isShowVocabulary);
    }
    
    protected override void InitializeUIComponents()
    {
        closeBtn.AddClickAction(OnCloseBtn); // 绑定关闭按钮事件
        VocabularyBtn.AddClickAction(ShowWordVocabulary); // 绑定关闭按钮事件
    }

    private void ShowWordVocabulary()
    {
        StageController.Instance.IsEnterVocabulary = false;
        SystemManager.Instance.ShowPanel(PanelType.WordVocabularyScreen);
        OnHideAnimationEnd();
    }
    
    /// <summary>
    /// 显示关内熟语
    /// </summary>
    private void ShowLevelWord()
    {
        int i = 1;
        
        foreach (var word in GameDataManager.instance.UserData.GetWordVocabulary().LevelWords)
        {
            if (!WordVocabularys.Keys.Contains(word))
            {
                // 从对象池获取奖励文字对象
                var wordButtonInstance = objectPool.GetObject<WordButton>(wordsVocabularyParent);
                wordButtonInstance.SetText(word,i,true);
                wordButtonInstance.transform.SetSiblingIndex(i);
                WordVocabularys.Add(word,wordButtonInstance);
                i++;
            }
            else
            {
                WordVocabularys[word].wordData.PageIndex= i;
                WordVocabularys[word].gameObject.transform.SetSiblingIndex(i);
                i++;
            }
        }
    }

    private void OnCloseBtn()
    {
        base.Close(); // 隐藏面板
    }
    
    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        foreach (var wordbtn in WordVocabularys.Values)
        {
            objectPool.ReturnObjectToPool(wordbtn.GetComponent<PoolObject>()); // 将对象返回到池中
        }
        WordVocabularys.Clear();
    }
}



