using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class WordVocabularyScreen : UIWindow
{        
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Text headTitle; // 关闭按钮
    [SerializeField] private WordButton wordProfab; // 词语预设
   
    [SerializeField] private Transform wordsVocabularyParent; // 词语父对象
   
    private ObjectPool objectPool; // 对象池实例
    private Dictionary<string, WordButton> NoteBooks = new Dictionary<string, WordButton>();
    
    protected override void Awake()
    {
        base.Awake();
        // 初始化对象池
        objectPool = new ObjectPool(wordProfab.gameObject, ObjectPool.CreatePoolContainer(transform, "WordPool"));
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        //EventDispatcher.instance.OnRemoveNotePuzzle += RemoveBookWord;
        ShowNoteBook();
        headTitle.text = MultilingualManager.Instance.GetString("WordNewIdioms");
        AudioManager.Instance.PlaySoundEffect("ShowUI");
    }
    
    protected override void InitializeUIComponents()
    {
        closeBtn.AddClickAction(OnCloseBtn); // 绑定关闭按钮事件
    }
    
    private void ShowNoteBook()
    {
        int i = 1;
        foreach (var word in GameDataManager.Instance.UserData.GetWordVocabulary().UserNotes)
        {
            if (!NoteBooks.Keys.Contains(word))
            {
                // 从对象池获取奖励文字对象
                var wordButtonInstance = objectPool.GetObject<WordButton>(wordsVocabularyParent);
                wordButtonInstance.SetText(word,i,false);
                wordButtonInstance.transform.SetSiblingIndex(i);
                NoteBooks.Add(word,wordButtonInstance);
                i++;
            }else
            {
                NoteBooks[word].gameObject.SetActive(true);
                NoteBooks[word].wordData.PageIndex= i;
                NoteBooks[word].transform.SetSiblingIndex(i);
                i++;
            }
        }
    }

    private void RemoveBookWord(string str)
    {
        if (NoteBooks.Keys.Contains(str))
        {
            // 从对象池获取奖励文字对象
            var wordButtonInstance = NoteBooks[str];
            NoteBooks.Remove(str);
            objectPool.ReturnObjectToPool(wordButtonInstance.GetComponent<PoolObject>());
        }
        int i = 0;
        foreach (var wordbtn in NoteBooks.Values)
        {
            wordbtn.wordData.PageIndex= ++i;
        }
    }

    private void OnCloseBtn()
    {
        base.Close(); // 隐藏面板
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        //EventDispatcher.instance.OnRemoveNotePuzzle -= RemoveBookWord;
        if (NoteBooks.Count > 0)
        {
            foreach (var word in NoteBooks.ToList())
            {
                RemoveBookWord(word.Key);
            }
        }
    }
}



