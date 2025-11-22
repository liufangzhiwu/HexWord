using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
using Toggle = UnityEngine.UI.Toggle;


/// *命名空间：** PuzzleWord
/// *功能：** 此类表示一个与文字相关的按钮组件，主要用于在界面上显示并响应单词的点击事件。它包含文字的显示以及按钮点击时触发的逻辑，如更新当前选中的单词并在界面上展示单词详情。
public class WordDetailTable : MonoBehaviour
{ 
    [SerializeField] private Transform MeanList;
    [SerializeField] private Text wordText;
    [SerializeField] private MeanTable meanTable;
    [SerializeField] private MeanTable exampstrTable;
    [SerializeField] private Text huaText;
    [SerializeField] private Text pinText;
    [SerializeField] private Toggle starToggle;
    private string word;

    private string interstr ;//意味
    private string exampstr ;//用例
    private string synonstr;//近义词
    
    // private GameObject exampstrTable=null;
    // private GameObject synonstrTable=null;

    private void Awake()
    {
        InitButton();
    }
   
   public void InitButton()
   {
       starToggle.onValueChanged.AddListener(ClickWordToggle);
       interstr = MultilingualManager.Instance.GetString("Interpretation");
       exampstr = MultilingualManager.Instance.GetString("ExampleSentence");
       synonstr = MultilingualManager.Instance.GetString("Synonym");

       //pinText.gameObject.SetActive(GameDataManager.Instance.UserData.LanguageCode=="Japanese");
    }

    public void SetText(string str)
    {
        word = str;
        wordText.text = word;
        
        starToggle.isOn = GameDataManager.Instance.UserData.GetWordVocabulary().UserNotes.Contains(word);
        
        DictionaryEntry entry = WordVocabularyManager.Instance.GetEntry(str);
        
        if (entry != null)
        {
            if(entry.Pinyin != null)
                pinText.text ="("+entry.Pinyin+")";
            //意味
            if (!string.IsNullOrEmpty(entry.Definition))
            {
                meanTable.gameObject.SetActive(true);
                //meanTable.transform.SetParent(MeanList.transform);
                meanTable.InitUI(interstr, entry.Definition);
            }
            //用例
            if (!string.IsNullOrEmpty(entry.Example))
            {
                // if (exampstrTable == null)
                // {
                //     exampstrTable=Instantiate(meanTable.transform.gameObject,MeanList);
                // }
                exampstrTable.gameObject.SetActive(true);
                exampstrTable.GetComponent<MeanTable>().InitUI(exampstr, entry.Example);
            }
            
            // if (!string.IsNullOrEmpty(entry.Synonym))
            // {
            //     // if (exampstrTable == null)
            //     // {
            //     //     exampstrTable=Instantiate(meanTable.transform.gameObject,MeanList);
            //     // }
            //     exampstrTable.gameObject.SetActive(true);
            //     exampstrTable.GetComponent<MeanTable>().InitUI(synonstr, entry.Synonym);
            // }
            
            // if(exampstrTable!=null)
            //     exampstrTable.gameObject.SetActive(entry.Example != null);
            // if(exampstrTable!=null)
            //     exampstrTable.gameObject.SetActive(entry.Synonym != null);
        }
        else
        {
            pinText.text ="";
            //TipManager.Instance.ShowTip(word+"当前词语未找到!");
            //Debug.LogError(word+"当前词语未找到");
        }
        huaText.text = MultilingualManager.Instance.GetString("Collect");
        
        //MeanList.GetComponent<VerticalLayoutGroup>().SetLayoutVertical();
    }

    private void OnDisable()
    {
        //downmeanText.text ="";
    }

    private void ClickWordToggle(bool isStar)
    {
        
        // if (isStar)
        // {
            meanTable.SetTextLayer();
            //exampstrTable.SetTextLayer();
        //}
        
        if (isStar)
        {
            GameDataManager.Instance.UserData.AddNoteBook(word);
            EventDispatcher.instance.OnWordVocabularyStatus?.Invoke();
        }
        else
        {
            GameDataManager.Instance.UserData.RemoveNoteBook(word);
        }
    }
}


