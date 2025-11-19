using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text;

public class TextPunctuationAdjuster : MonoBehaviour
{
    [SerializeField] private Text targetText;
    [SerializeField] private bool adjustOnStart = true;
    [SerializeField] private bool enableDebugVisualization = false;
    
    private TextGenerator textGenerator;
    private TextGenerationSettings generationSettings;
    private Canvas canvas;
    private int hangcharCount;
     
    private void Start()
    {
        // if (targetText == null)
        //     targetText = GetComponent<Text>();
            
        //StartCoroutine(AdjustText());
        canvas = targetText?.canvas;
    }

    // private IEnumerator AdjustText()
    // {
    //     yield return new WaitForEndOfFrame();
    //     
    //     if (adjustOnStart && targetText != null)
    //     {
    //         AdjustTextPunctuation();
    //     }
    // }
    
    
    public void AdjustTextPunctuation()
    {
        if (targetText == null)
           targetText = GetComponent<Text>();
        
        InitializeGenerator();

        if (StageController.Instance.IsEnterVocabulary)
        {
            hangcharCount = 15;
        }
        else
        {
            hangcharCount = 19;
        }
        
        string originalText = targetText.text;
        string adjustedText = AdjustPunctuationPositions(originalText);
        
        if (adjustedText != originalText)
        {
            targetText.text = adjustedText;
            Debug.Log("文本句号位置已调整");
        }
        else
        {
            Debug.Log("无需调整");
        }
    }
    
    private void InitializeGenerator()
    {
        if (textGenerator == null)
            textGenerator = new TextGenerator();
            
        // if (canvas != null)
        // {
        //     generationSettings = targetText.GetGenerationSettings(
        //         new Vector2(targetText.rectTransform.rect.width, 
        //             targetText.rectTransform.rect.height));
        //     generationSettings.scaleFactor = canvas.scaleFactor;
        // }
        // else
        // {
            generationSettings = targetText.GetGenerationSettings(targetText.rectTransform.rect.size);
            generationSettings.scaleFactor = 1f;
        //}
    }
    
    // private void InitializeGenerator()
    // {
    //     textGenerator = new TextGenerator();
    //     generationSettings = targetText.GetGenerationSettings(targetText.rectTransform.rect.size);
    //     generationSettings.scaleFactor = 1f;
    // }
    
    private string AdjustPunctuationPositions(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        
        string currentText = text;
        bool changed;
        int maxIterations = 10; // 防止无限循环
        
        do
        {
            changed = false;
            InitializeGenerator();
            textGenerator.Populate(currentText, generationSettings);
            
            int lines=currentText.Length/hangcharCount +1;
            //IList<UILineInfo> lines = textGenerator.lines;
            if (lines <= 1) break; // 只有一行不需要处理
            
            StringBuilder sb = new StringBuilder(currentText);
            
            Debug.Log("检测文本: " + sb+"文本长度"+sb.Length+"行数"+lines);
            
            // 从第一行开始检查（跳过第0行，因为它没有上一行）
            for (int lineIndex = 1; lineIndex < lines; lineIndex++)
            {
                int lineStart = lineIndex*hangcharCount;
                Debug.Log("检测文本: 索引" + lineStart+"字符"+sb[lineStart]);
                // 检查行首是否为句号
                if (IsPunctuation(sb[lineStart]))
                {
                    //int prevLineStart = lines[lineIndex - 1].startCharIdx;
                    int prevLineEnd = lineStart - 1;
                    Debug.Log("存在句号: 索引为" + lineStart);
                    //if (lastCharIndex != -1)
                    {
                        // 获取上一行最后一个字符
                        char lastChar = sb[prevLineEnd];
                        
                        // 删除上一行的最后一个字符
                        //sb.Remove(lastCharIndex, 1);
                        
                        // 在当前行首插入这个字符（在句号前面）
                        sb.Insert(prevLineEnd, " ");
                        
                        changed = true;
                        Debug.Log($"移动字符 '{lastChar}' 从位置 {prevLineEnd} 到行首位置 {lineStart}");
                        
                        // 由于修改了文本，需要跳出循环重新计算布局
                        break;
                    }
                }
            }
            
            currentText = sb.ToString();
            
        } while (changed && maxIterations-- > 0);
        
        return currentText;
    }
    
    private bool IsPunctuation(char c)
    {
        return c == '。' || c == '.' || 
               c == '！' || c == '!' ||
               c == '？' || c == '?' ||
               c == '，' || c == ',';
    }
    
    // 可视化调试信息
    private void OnDrawGizmos()
    {
        if (!enableDebugVisualization || targetText == null) 
            return;
            
        // 绘制文本边界
        Gizmos.color = Color.green;
        RectTransform rectTransform = targetText.rectTransform;
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        Gizmos.DrawLine(corners[0], corners[1]);
        Gizmos.DrawLine(corners[1], corners[2]);
        Gizmos.DrawLine(corners[2], corners[3]);
        Gizmos.DrawLine(corners[3], corners[0]);
        
        // 如果有文本生成器信息，绘制每行位置
        if (textGenerator != null && textGenerator.lines != null)
        {
            Gizmos.color = Color.red;
            
            foreach (UILineInfo line in textGenerator.lines)
            {
                Vector3 lineStart = new Vector3(
                    corners[0].x, 
                    corners[0].y - line.topY / 100f, 
                    corners[0].z
                );
                
                Vector3 lineEnd = new Vector3(
                    corners[2].x,
                    lineStart.y,
                    corners[0].z
                );
                
                Gizmos.DrawLine(lineStart, lineEnd);
            }
        }
    }
    
    // 编辑器按钮
    [ContextMenu("调整文本句号")]
    private void AdjustInEditor()
    {
        AdjustTextPunctuation();
    }
    
    [ContextMenu("重置文本")]
    private void ResetText()
    {
        if (targetText != null)
        {
            targetText.text = "这是一个示例文本。它包含多行文本。\n这是第二行。这是第三行，需要测试句号是否在行首。";
        }
    }
}