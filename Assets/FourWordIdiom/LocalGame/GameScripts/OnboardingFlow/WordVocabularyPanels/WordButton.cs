using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WordButton : MonoBehaviour
{
   [SerializeField] private Text wordText;
   [SerializeField] private Text pinText;
   [SerializeField] private Button wordBtn;
   public PuzzleData wordData=new PuzzleData();

   private void Awake()
   {
       InitButton();
   }

    private void OnEnable()
    {
        InitUI();
    }

    public void InitButton()
   {
       wordBtn.AddClickAction(ClickWord); 
    }

    private void InitUI()
    {
        bool isJan = GameDataManager.instance.UserData.LanguageCode == "JS";

        if (pinText != null)
        {
            pinText.gameObject.SetActive(isJan);
        }

        RectTransform rectTransform = wordText.transform.GetComponent<RectTransform>();

        // 设置高度，使用 sizeDelta 来调整
        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, isJan ? 330 : 660);
    }

   public void SetText(string str,int index,bool isVocabulary=true)
    {
        wordData.CurPuzzle = str;
        wordText.text = wordData.CurPuzzle;
        wordData.IsVocabularyPuzzle= isVocabulary;
        wordData.PageIndex= index;
        if (wordData.IsVocabularyPuzzle)
        {
            DictionaryEntry entry = WordVocabularyManager.Instance.GetEntry(str);

            if (entry != null)
            {
                pinText.text = string.IsNullOrEmpty(entry.Pinyin)?"": entry.Pinyin;
            }
            //wordBtn.GetComponent<Image>().color = Color.white;
        }
        else
        {
            if (pinText != null)
            {
                pinText.text ="";
            }
            
            //wordBtn.GetComponent<Image>().color = Color.green;
        }
    }
   
    private void ClickWord()
    {
        StageController.Instance.PuzzleData = wordData;
        //Debug.LogError("点击词语的索引"+LevelManager.Instance.WordData.PageIndex);
        //LevelManager.Instance.WordData.CurWord=word;
        if (StageController.Instance.IsEnterVocabulary)
        {
            SystemManager.Instance.HidePanel(PanelType.LevelWordScreen,false);
            SystemManager.Instance.ShowPanel(PanelType.LevelWordDetail);
        }
        else
        {
            //UIManager.Instance.HidePanel(PanelName.WordVocabularyScreen,false);
            SystemManager.Instance.ShowPanel(PanelType.WordDetailScreen);
        }
    }
}

