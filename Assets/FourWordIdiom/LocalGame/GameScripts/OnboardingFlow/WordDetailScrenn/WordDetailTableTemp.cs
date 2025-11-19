using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
using Toggle = UnityEngine.UI.Toggle;


/// *命名空间：** PuzzleWord
/// *功能：** 此类表示一个与文字相关的按钮组件，主要用于在界面上显示并响应单词的点击事件。它包含文字的显示以及按钮点击时触发的逻辑，如更新当前选中的单词并在界面上展示单词详情。
public class WordDetailTableTemp : MonoBehaviour
{ 
    [SerializeField] private Text wordText;
    [SerializeField] private Text meanText;
    [SerializeField] private Text huaText;
    [SerializeField] private Text pinText;
    [SerializeField] private Image meandownImage;
    [SerializeField] private Image meanImage;
    [SerializeField] private Text downmeanText;
    [SerializeField] private Toggle starToggle;
    private string word;

    private int maxlength = 200;
    private Image useImage=null;
    private Image ciImage=null;
    private string interstr ;//意味
    private string exampstr ;//用例
    private string synonstr;//近义词

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

        pinText.gameObject.SetActive(GameDataManager.instance.UserData.LanguageCode=="Japanese");
    }

    public void SetText(string str)
    {
        word = str;
        wordText.text = word;
        
        starToggle.isOn = GameDataManager.instance.UserData.GetWordVocabulary().UserNotes.Contains(word);
        
        DictionaryEntry entry = WordVocabularyManager.Instance.GetEntry(str);
        
        if (entry != null)
        {
            if(entry.Pinyin != null)
                pinText.text =entry.Pinyin;
            meanText.text =$"<b>{interstr}</b><color=#00000000>.</color>"+ entry.Definition;
            maxlength = 200;
            int dianleng =12 - (entry.Definition.Length+3) % 12;
            int lie =(int)Math.Ceiling((meanText.text.Length-32) / 12.0)+1; // 使用 Math.Ceiling 来处理余数
            string dots = new string('.', dianleng==12?12:12+dianleng); // 根据 dianleng 创建相应数量的点
            string temp =meanText.text+$"<color=#00000000>{dots}</color><b>{exampstr}</b><color=#00000000>.</color>"+ entry.Synonym;
            if ((temp.Length-57) > maxlength)
            {
                
                if ((temp.Length - 57) > 350)
                {
                    temp = meanText.text;                       
                }
                string ntxt = temp.Substring(0, maxlength);
                //意味在meanText中可以放置完全
                if (meanText.text.Length <= maxlength)                   
                {
                    //默认可以放置用例文本
                    ntxt =temp.Substring(0,257);   
                    maxlength = ntxt.Length;
                    //如果meanText长度过长。则不放置用例文本
                    if(meanText.text.Length >= 182)
                    {
                        string m = meanText.text + $"<color=#00000000>{dots}</color>";
                        ntxt = temp.Substring(0,m.Length);
                        maxlength = m.Length;
                    }
                }                    
               
                meanText.text =ntxt;
                // 处理超过部分的内容
                string excessContent = temp.Substring(maxlength);
                downmeanText.text = excessContent;
                // 这里可以添加您需要的逻辑，例如打印或记录超过的内容
                Debug.Log("超过的内容:" + excessContent);
                if (excessContent.Contains(exampstr))
                {
                    lie = lie - 14;
                    int removecodeling = excessContent.StartsWith("<b>用例</b>") ? 32 : 57;
                    if (removecodeling == 32) lie = 0;
                    HighlightExampleText(exampstr, lie,false);
                    
                    //显示近义词
                    if (entry.Synonym != null)
                    {
                        if (entry.Synonym.Length + excessContent.Length <= maxlength)
                        {
                            dianleng =12 - (excessContent.Length-removecodeling) % 12;
                            lie =(int)Math.Ceiling((excessContent.Length-removecodeling) / 12.0)+1; // 使用 Math.Ceiling 来处理余数
                            dots = new string('.', dianleng == 12 ? 12 : 12+dianleng); // 根据 dianleng 创建相应数量的点
                    
                            downmeanText.text =downmeanText.text+$"<color=#00000000>{dots}</color><b>{synonstr}</b><color=#00000000>.</color>"+entry.Synonym;
                            HighlightciImage(synonstr,lie,false);
                        }
                    }
                    else
                    {
                        if(ciImage!=null)
                            ciImage.gameObject.SetActive(false);
                    }
                }
                else
                {
                    HighlightExampleText(exampstr,lie,true);
                    
                    //显示近义词
                    if (entry.Synonym != null)
                    {
                        if (entry.Synonym.Length + excessContent.Length <= maxlength)
                        {
                            dianleng =12 - (excessContent.Length) % 12;
                            lie =(int)Math.Ceiling(excessContent.Length / 12.0)+1; // 使用 Math.Ceiling 来处理余数
                            dots = new string('.', dianleng == 12 ? 12 : 12+dianleng); // 根据 dianleng 创建相应数量的点
                    
                            downmeanText.text =downmeanText.text+$"<color=#00000000>{dots}</color><b>{synonstr}</b><color=#00000000>.</color>"+entry.Synonym;
                            HighlightciImage(synonstr, lie,false);
                        }
                    }
                    else
                    {
                        if(ciImage!=null)
                            ciImage.gameObject.SetActive(false);
                    }
                }
            }
            else
            {            
                if(!string.IsNullOrEmpty(entry.Example))
                {
                    meanText.text = temp;
                }
                else
                {
                    meanText.text = meanText.text + $"<color=#00000000>{dots}</color>";
                }                         
                HighlightExampleText(exampstr,lie,true);
                downmeanText.text = "";
                if(ciImage!=null)
                    ciImage.gameObject.SetActive(false);
            }
        }
        else
        {
            pinText.text ="";
            //TipManager.Instance.ShowTip(word+"当前词语未找到!");
            //Debug.LogError(word+"当前词语未找到");
        }
        huaText.text = MultilingualManager.Instance.GetString("Collect");
    }
    
    void HighlightExampleText(string keyword,int lie,bool ismeanText)
    {
        // 获取文本内容
        string fullText =ismeanText? meanText.text:downmeanText.text;
        Image TImage=ismeanText? meanImage:meandownImage;
        // 如果文本中包含关键字
        if (fullText.Contains(keyword))
        {
            // 设置背景图像
            if (useImage == null)
            {
                useImage = Instantiate(TImage, meanText.transform.parent);
            }
            useImage.gameObject.SetActive(true); // 确保背景图显示
            useImage.rectTransform.sizeDelta = TImage.rectTransform.sizeDelta; // 设置背景图的大小
            useImage.transform.SetSiblingIndex(TImage.transform.GetSiblingIndex());
            float jiange = StageController.Instance.IsEnterVocabulary?59.5f: 69.8f;
            // 计算背景图的位置
            useImage.rectTransform.anchoredPosition = new Vector3(TImage.rectTransform.anchoredPosition.x-(lie*jiange),TImage.rectTransform.anchoredPosition.y,0);
        
            // 这里可以根据需要进一步调整背景图的位置
            // 例如，调整到关键字的中心
        }
        else
        {                
            useImage?.gameObject.SetActive(false); // 隐藏背景图
        }
    }
    
    void HighlightciImage(string keyword,int lie,bool ismeanText)
    {
        // 获取文本内容
        string fullText =ismeanText? meanText.text:downmeanText.text;
        Image TImage=ismeanText? meanImage:meandownImage;
        // 如果文本中包含关键字
        if (fullText.Contains(keyword))
        {
            // 设置背景图像
            if (ciImage == null)
            {
                ciImage = Instantiate(TImage, meanText.transform.parent);
            }
            RectTransform rectTransform = ciImage.rectTransform;
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, StageController.Instance.IsEnterVocabulary?190: 230);
            ciImage.gameObject.SetActive(true); // 确保背景图显示
            //sciImage.rectTransform.sizeDelta = TImage.rectTransform.sizeDelta; // 设置背景图的大小
            ciImage.transform.SetSiblingIndex(TImage.transform.GetSiblingIndex());
            float jiange = StageController.Instance.IsEnterVocabulary?59.5f: 69.8f;
            // 计算背景图的位置
            ciImage.rectTransform.anchoredPosition = new Vector3(TImage.rectTransform.anchoredPosition.x-(lie*jiange),TImage.rectTransform.anchoredPosition.y,0);
        
            // 这里可以根据需要进一步调整背景图的位置
            // 例如，调整到关键字的中心
        }
        else
        { 
            ciImage?.gameObject.SetActive(false); // 隐藏背景图
        }
    }

    private void OnDisable()
    {
        //downmeanText.text ="";
    }

    private void ClickWordToggle(bool isStar)
    {
        if (isStar)
        {
            GameDataManager.instance.UserData.AddNoteBook(word);
            //EventDispatcher.OnWordVocabularyStatus?.Invoke();
        }
        else
        {
            GameDataManager.instance.UserData.RemoveNoteBook(word);
        }
    }
}


