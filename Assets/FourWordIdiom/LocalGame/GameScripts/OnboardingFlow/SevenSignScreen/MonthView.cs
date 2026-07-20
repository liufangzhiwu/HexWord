using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 月份视图：显示一个月的日历网格（6行×7列）
/// 每行使用 HorizontalLayoutGroup 排列 7 个 DayCell
/// </summary>
public class MonthView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform dayGridContainer;
    [SerializeField] private GameObject dayCellPrefab;
    [SerializeField] private GameObject dayRowPrefab;

    [Header("Streak Bar Settings")]
    // 宽度映射：覆盖天数 -> 宽度（像素）
    private Dictionary<int, float> barWidths = new Dictionary<int, float>()
    {
        { 1, 140f },
        { 2, 300f },
        { 3, 450f },
        { 4, 610f },
        { 5, 755f },
        { 6, 910f },
        { 7, 1070f },
    };
    
    private const float DEFAULT_WIDTH_FOR_LONG_STREAK = 1070f; // 覆盖≥7天时宽度

    // X轴偏移（按星期：                          0=周日, 一,  二,    三,    四,   五,    六...）
    private float[] barXOffsets = new float[7] { 80f, 235f, 390f, 543f, 696f, 853f, 1005f };

    private const int ROWS = 6;
    private const int COLS = 7;

    private int currentYear;
    private int currentMonth;
    private List<DayCell> dayCells = new List<DayCell>(ROWS * COLS);
    private List<GameObject> rowObjects = new List<GameObject>();

    // ==================== 公共方法 ====================

    public void Setup(int year, int month, List<int> signedDays)
    {
        currentYear = year;
        currentMonth = month;
        rowObjects.Clear();

        foreach (Transform child in dayGridContainer)
            Destroy(child.gameObject);
        dayCells.Clear();

        DateTime firstDay = new DateTime(year, month, 1);
        int firstWeekday = (int)firstDay.DayOfWeek;
        int daysInMonth = DateTime.DaysInMonth(year, month);
        DateTime today = DateTime.Now.Date;

        for (int row = 0; row < ROWS; row++)
        {
            GameObject rowGO = Instantiate(dayRowPrefab, dayGridContainer);
            rowGO.name = "Row_" + row;
            rowObjects.Add(rowGO);

            Transform cellParent = rowGO.transform.GetChild(2);
            if (cellParent == null)
            {
                Debug.LogError("行预制体缺少第2个子对象作为格子容器");
                continue;
            }

            for (int col = 0; col < COLS; col++)
            {
                int index = row * COLS + col;
                int dayNumber = index - firstWeekday + 1;

                GameObject cellGO = Instantiate(dayCellPrefab, cellParent);
                DayCell cell = cellGO.GetComponent<DayCell>();
                if (cell == null) continue;

                if (dayNumber >= 1 && dayNumber <= daysInMonth)
                {
                    bool isSigned = signedDays != null && signedDays.Contains(dayNumber);
                    cell.Setup(dayNumber, isSigned);
                    DateTime cellDate = new DateTime(year, month, dayNumber);
                    if (cellDate <= today)
                        cell.SetLastDate();
                    else
                    {
                        cell.Setup(dayNumber, false);
                    }
                }
                else
                {
                    cell.SetEmpty();
                }
                dayCells.Add(cell);
            }
        }

        if (GameDataManager.Instance.UserData._signSaveData.currentStreak >= 0)
        {
            Refresh(year,month,signedDays);
        }
        
    }

    public void Refresh(int year, int month,List<int> signedDays)
    {
        // 获取首次胜利日期（若存在）
        DateTime? firstWinDate = null;
        DateTime? lastWinDate = null;
        
        DateTime today = DateTime.Now.Date;
        
        SignSaveData signData = GameDataManager.Instance?.UserData?._signSaveData;
        if (signData != null && signData.firstSignDay != 0)
        {
            firstWinDate = UIUtilities.DayIndexToDateTime(signData.firstSignDay);
            lastWinDate = UIUtilities.DayIndexToDateTime(signData.lastSignDay);

            if (lastWinDate.Value>today)
            {
                lastWinDate= today;
            }
        }

        DateTime firstDay = new DateTime(year, month, 1);
        int firstWeekday = (int)firstDay.DayOfWeek;
        int daysInMonth = DateTime.DaysInMonth(year, month);
      

        for (int row = 0; row < ROWS && row < rowObjects.Count; row++)
        {
            GameObject rowGO = rowObjects[row];
            Transform cellParent = rowGO.transform.GetChild(2);
            if (cellParent == null) continue;

            for (int col = 0; col < COLS; col++)
            {
                int index = row * COLS + col;
                int dayNumber = index - firstWeekday + 1;

                Transform cellTrans = cellParent.GetChild(col);
                DayCell cell = cellTrans.GetComponent<DayCell>();
                if (cell == null) continue;

                if (dayNumber >= 1 && dayNumber <= daysInMonth)
                {
                    bool isSigned = false; // 默认未签到
                    // 否则按签到列表判断
                     isSigned = signedDays != null && signedDays.Contains(dayNumber);
                    
                    // 判断是否签到：如果日期在首次胜利日期之前（或等于），则按 signedDays 决定
                    DateTime cellDate = new DateTime(year, month, dayNumber);
                    if (firstWinDate.HasValue && cellDate >= firstWinDate.Value&&cellDate <= lastWinDate.Value)
                    {
                        cell.Setup(dayNumber, isSigned);
                        // 日期大于首次胜利日期 → 强制未签到
                        //isSigned = false;
                        
                        //if(lastWinDate.Value != today&&cellDate!=today){}
                        if (lastWinDate.Value == today && cellDate == today &&
                            GameDataManager.Instance.UserData._signSaveData.currentStreak == 1)
                        {
                            
                        }
                        else
                        {
                            if(isSigned)
                                cell.SetHasSigned(dayNumber);
                        }
                        
                       
                    }
                    
                    if (cellDate < firstWinDate.Value)
                    {
                        if (!isSigned)
                        {
                            cell.Setup(dayNumber, isSigned);
                            cell.SetLastDate();
                        }
                    }
                    else
                    {
                        if (cellDate > lastWinDate.Value)
                        {
                            if (!isSigned)
                            {
                                cell.Setup(dayNumber, false);
                            }
                        }
                        
                        if (cellDate <= today)
                        {
                            if (!isSigned)
                            {
                                cell.Setup(dayNumber, isSigned);
                                cell.SetLastDate();
                            }
                        }
                    }
                }
                else
                {
                    cell.SetEmpty();
                }
            }
        }

        // 重绘装饰条（可能因日期数据变化而更新）
        ApplyStreakBar();
       
    }

    // ==================== 装饰条核心逻辑（统一处理跨行） ====================

private void ApplyStreakBar()
{
    // 遍历每一行
    for (int row = 0; row < rowObjects.Count; row++)
    {
        GameObject targetRow = rowObjects[row];
        Transform barTransform = targetRow.transform.GetChild(0);
        Transform cellsParent = targetRow.transform.GetChild(2);
        RectTransform originalBarRect = barTransform.GetComponent<RectTransform>();
        if (originalBarRect == null) continue;

        DayCell[] cells = cellsParent.GetComponentsInChildren<DayCell>();

        // 收集所有连续段（起始索引、长度）
        List<(int startIndex, int length)> segments = new List<(int, int)>();
        int currentStart = -1;
        int currentLength = 0;

        for (int i = 0; i < cells.Length; i++)
        {
            DayCell cell = cells[i];
            bool isInStreak = (cell.signMark.enabled == false && cell.signMark.gameObject.activeSelf);

            if (isInStreak)
            {
                if (currentStart == -1)
                {
                    currentStart = i;
                    currentLength = 1;
                }
                else
                {
                    currentLength++;
                }
            }
            else
            {
                if (currentStart != -1)
                {
                    segments.Add((currentStart, currentLength));
                    currentStart = -1;
                    currentLength = 0;
                }
            }
        }
        if (currentStart != -1)
        {
            segments.Add((currentStart, currentLength));
        }

        // 如果没有段，隐藏原有装饰条
        if (segments.Count == 0)
        {
            originalBarRect.gameObject.SetActive(false);

            foreach (Image cellline in targetRow.transform.GetChild(1).GetComponentsInChildren<Image>())
            {
                cellline.gameObject.SetActive(false);
            }
            
            continue;
        }

        // 如果有多个段，需要动态生成多个装饰条；否则直接使用原条
        if (segments.Count > 1)
        {
            // 隐藏原有装饰条
            originalBarRect.gameObject.SetActive(false);

            // 为每个段生成一个新的装饰条（使用原条作为模板）
            foreach (var seg in segments)
            {
                // 克隆原装饰条（包括 RectTransform 和所有组件）
                GameObject newBarGO = Instantiate(originalBarRect.gameObject, targetRow.transform.GetChild(1));
                RectTransform newBarRect = newBarGO.GetComponent<RectTransform>();
                if (newBarRect == null) continue;

                // 计算该段的 X 偏移和宽度
                float targetX = barXOffsets[seg.startIndex];
                float targetWidth = GetWidthForStreak(seg.length);

                // 若长度为1，只点亮格子，不显示条（逻辑同前）
                if (seg.length == 1)
                {
                    cells[seg.startIndex].SetSignedState(true);
                    newBarGO.SetActive(false);
                    continue;
                }

                // 应用设置
                newBarRect.transform.SetAsFirstSibling();
                newBarRect.sizeDelta = new Vector2(targetWidth, newBarRect.sizeDelta.y);
                newBarRect.anchoredPosition = new Vector2(targetX, newBarRect.anchoredPosition.y);
                newBarGO.SetActive(true);
            }
        }
        else // 只有一段
        {
            // 使用原有装饰条
            var seg = segments[0];
            float targetX = barXOffsets[seg.startIndex];
            float targetWidth = GetWidthForStreak(seg.length);

            if (seg.length == 1)
            {
                cells[seg.startIndex].SetSignedState(true);
                originalBarRect.gameObject.SetActive(false);
            }
            else
            {
                originalBarRect.transform.SetAsFirstSibling();
                originalBarRect.gameObject.SetActive(true);
                originalBarRect.sizeDelta = new Vector2(targetWidth, originalBarRect.sizeDelta.y);
                originalBarRect.anchoredPosition = new Vector2(targetX, originalBarRect.anchoredPosition.y);
            }
        }
    }
}

    /// <summary>
    /// 根据覆盖天数返回对应的宽度
    /// </summary>
    private float GetWidthForStreak(int days)
    {
        if (days >= 7)
            return DEFAULT_WIDTH_FOR_LONG_STREAK; // 1070
        else if (barWidths.ContainsKey(days))
            return barWidths[days];
        else
            return 0f; // 未定义的天数，隐藏
    }


    public void Clear()
    {
        foreach (Transform child in dayGridContainer)
            Destroy(child.gameObject);
        rowObjects.Clear();
        dayCells.Clear();
    }
}