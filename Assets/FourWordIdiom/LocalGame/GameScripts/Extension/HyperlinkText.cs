using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;


/// <summary>
/// 文本控件支持超链接、下划线
/// </summary>
public class HyperlinkText : Text, IPointerClickHandler
{
    public Action<string> onHyperlinkClick;

    /// 超链接信息类
    private class HyperlinkInfo
    {
        public int startIndex;
        public int endIndex;
        public string name;
        public readonly List<Rect> boxes = new List<Rect>();
        public List<int> linefeedIndexList = new List<int>();
    }

    /// 解析完最终的文本
    private string m_OutputText;

    /// 超链接信息列表
    private readonly List<HyperlinkInfo> m_HrefInfos = new List<HyperlinkInfo>();

    /// 文本构造器
    protected StringBuilder s_TextBuilder = new StringBuilder();

    [Tooltip("超链接文本颜色")]
    [SerializeField] private Color32 innerTextColor = new Color32(225, 129, 41, 128);

    /// 超链接正则
    private static readonly Regex s_HrefRegex = new Regex(@"<href=([^>\n\s]+)>(.*?)(</href>)", RegexOptions.Singleline);

    // ugui富文本标签
    // 格式1：<b></b>  <i></i>
    private static readonly string[] _uguiSymbols1 = { "b", "i" };
    // 格式2：<color=#ffffff></color> <color=red></color>
    private static readonly string[] _uguiSymbols2 = { "color", "size" };

    public string GetHyperlinkInfo { get { return text; } }

    public override void SetVerticesDirty()
    {
        base.SetVerticesDirty();

        text = GetHyperlinkInfo;
        m_OutputText = GetOutputText(text);
    }

    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        var orignText = m_Text;
        m_Text = m_OutputText;
        base.OnPopulateMesh(toFill);
        m_Text = orignText;
        UIVertex vert = new UIVertex();

        // 处理超链接包围框
        foreach (var hrefInfo in m_HrefInfos)
        {
            hrefInfo.boxes.Clear();
            hrefInfo.linefeedIndexList.Clear();
            if (hrefInfo.startIndex >= toFill.currentVertCount)
                continue;

            // 将超链接里面的文本顶点索引坐标加入到包围框
            toFill.PopulateUIVertex(ref vert, hrefInfo.startIndex);

            var pos = vert.position;
            var bounds = new Bounds(pos, Vector3.zero);
            hrefInfo.linefeedIndexList.Add(hrefInfo.startIndex);
            for (int i = hrefInfo.startIndex, m = hrefInfo.endIndex; i < m; i++)
            {
                if (i >= toFill.currentVertCount)
                    break;

                toFill.PopulateUIVertex(ref vert, i);
                vert.color = innerTextColor;
                toFill.SetUIVertex(vert, i);

                pos = vert.position;

                bool needEncapsulate = true;

                if (i > 4 && (i - hrefInfo.startIndex) % 4 == 0)
                {
                    UIVertex lastV = new UIVertex();
                    toFill.PopulateUIVertex(ref lastV, i - 4);
                    var lastPos = lastV.position;

                    if (pos.x < lastPos.x && pos.y < lastPos.y) // 换行重新添加包围框
                    {
                        hrefInfo.boxes.Add(new Rect(bounds.min, bounds.size));
                        hrefInfo.linefeedIndexList.Add(i);
                        bounds = new Bounds(pos, Vector3.zero);
                        needEncapsulate = false;
                    }
                }
                if (needEncapsulate)
                {
                    bounds.Encapsulate(pos); // 扩展包围框
                }
            }
            hrefInfo.boxes.Add(new Rect(bounds.min, bounds.size));
        }

        //一个字一个字的划 效率差 而且字与字之间容易有接缝
        DrawUnderLine(toFill);
    }

    private void DrawUnderLine(VertexHelper vh)
    {
        UIVertex vert = new UIVertex();
        List<Vector3> startPosList = new List<Vector3>();
        List<Vector3> endPosList = new List<Vector3>();
        foreach (var hrefInfo in m_HrefInfos)
        {
            if (hrefInfo.startIndex >= vh.currentVertCount) continue;

            float minY = float.MaxValue;
            for (int i = hrefInfo.startIndex, m = hrefInfo.endIndex; i < m; i += 4)
            {
                if (i >= vh.currentVertCount)
                    break;

                if (hrefInfo.linefeedIndexList.Contains(i))
                {
                    for (int j = 0; j < startPosList.Count; j++)
                    {
                        MeshUnderLine(vh, new Vector2(startPosList[j].x, minY), new Vector2(endPosList[j].x, minY));
                    }
                    startPosList.Clear();
                    endPosList.Clear();
                }

                vh.PopulateUIVertex(ref vert, i + 3);
                startPosList.Add(vert.position);
                vh.PopulateUIVertex(ref vert, i + 2);
                endPosList.Add(vert.position);

                if (vert.position.y < minY)
                {
                    minY = vert.position.y;
                }
            }

            for (int j = 0; j < startPosList.Count; j++)
            {
                MeshUnderLine(vh, new Vector2(startPosList[j].x, minY), new Vector2(endPosList[j].x, minY));
            }
            startPosList.Clear();
            endPosList.Clear();
        }
    }

    private void MeshUnderLine(VertexHelper vh, Vector2 startPos, Vector2 endPos)
    {
        Vector2 extents = rectTransform.rect.size;
        var setting = GetGenerationSettings(extents);

        // 使用下划线字符生成顶点数据
        TextGenerator underlineText = new TextGenerator();
        underlineText.Populate("—", setting); // 使用 "—" 作为下划线字符

        IList<UIVertex> lineVer = underlineText.verts; // 获取下划线字符的顶点数据

        // 定义下划线的四个顶点位置
        Vector3[] pos = new Vector3[4];

        // 调整下划线的长度
        float underlineLength = endPos.x - startPos.x; // 计算当前下划线长度
        float extendLength = 15f; // 延长下划线的额外长度（可根据需求调整）

        // 设置下划线的顶点位置
        pos[0] = startPos + new Vector2(-extendLength / 2, 0); // 左下角
        pos[1] = endPos + new Vector2(extendLength / 2, 0);    // 右下角
        pos[2] = endPos + new Vector2(extendLength / 2, 4f);   // 右上角
        pos[3] = startPos + new Vector2(-extendLength / 2, 4f); // 左上角

        // 创建下划线的顶点数据
        UIVertex[] tempVerts = new UIVertex[4];
        for (int i = 0; i < 4; i++)
        {
            tempVerts[i] = lineVer[i];
            tempVerts[i].color = innerTextColor; // 设置下划线颜色
            tempVerts[i].position = pos[i];     // 设置顶点位置
        }

        // 将下划线顶点添加到 VertexHelper
        vh.AddUIVertexQuad(tempVerts);
    }

    /// <summary>
    /// 获取超链接解析后的最后输出文本
    /// </summary>
    /// <returns></returns>
    protected virtual string GetOutputText(string outputText)
    {
        s_TextBuilder.Length = 0;
        m_HrefInfos.Clear();
        var indexText = 0;
        int count = 0;
        foreach (Match match in s_HrefRegex.Matches(outputText))
        {
            string appendStr = outputText.Substring(indexText, match.Index - indexText);

            s_TextBuilder.Append(appendStr);

            //空格和回车没有顶点渲染，所以要去掉
            count += appendStr.Length - appendStr.Replace(" ", "").Replace("\n", "").Length;
            //去掉富文本标签的长度
            for (int i = 0; i < _uguiSymbols1.Length; i++)
            {
                count += appendStr.Length - appendStr.Replace($"<{_uguiSymbols1[i]}>", "").Replace($"</{_uguiSymbols1[i]}>", "").Length;
            }
            for (int i = 0; i < _uguiSymbols2.Length; i++)
            {
                string pattern = $"<{_uguiSymbols2[i]}=(.*?)>";
                count += appendStr.Length - Regex.Replace(appendStr, pattern, "").Length;
                count += appendStr.Length - appendStr.Replace($"</{_uguiSymbols2[i]}>", "").Length;
            }

            int startIndex = (s_TextBuilder.Length - count) * 4;
            var group = match.Groups[1];
            var hrefInfo = new HyperlinkInfo
            {
                startIndex = startIndex, // 超链接里的文本起始顶点索引
                endIndex = startIndex + (match.Groups[2].Length * 4),
                name = group.Value
            };
            m_HrefInfos.Add(hrefInfo);

            s_TextBuilder.Append(match.Groups[2].Value);
            indexText = match.Index + match.Length;
        }
        s_TextBuilder.Append(outputText.Substring(indexText, outputText.Length - indexText));
        return s_TextBuilder.ToString();
    }

    /// <summary>
    /// 点击事件检测是否点击到超链接文本
    /// </summary>
    /// <param name="eventData"></param>
    public void OnPointerClick(PointerEventData eventData)
    {
        Vector2 lp = Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out lp);

        foreach (var hrefInfo in m_HrefInfos)
        {
            var boxes = hrefInfo.boxes;
            for (var i = 0; i < boxes.Count; ++i)
            {
                if (boxes[i].Contains(lp))
                {
                    if (onHyperlinkClick != null)
                        onHyperlinkClick.Invoke(hrefInfo.name);

                    return;
                }
            }
        }
    }



#if UNITY_EDITOR
    //需延迟调用该方法
    public void AddVisibleBound()
    {
        int index = 0;

        foreach (var hrefInfo in m_HrefInfos)
        {
            Color color = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), 0.2f);
            index++;
            foreach (Rect rect in hrefInfo.boxes)
            {
                GameObject gameObject = new GameObject();
                gameObject.name = string.Format("GOBoundBox[{0}]", hrefInfo.name);
                gameObject.transform.SetParent(this.gameObject.transform, false);

                RectTransform rectTransform = gameObject.AddComponent<RectTransform>();
                rectTransform.sizeDelta = rect.size;
                rectTransform.localPosition = new Vector3(rect.position.x + rect.size.x / 2, rect.position.y + rect.size.y / 2, 0);

                Image image = gameObject.AddComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
            }
        }
    }
#endif
}

