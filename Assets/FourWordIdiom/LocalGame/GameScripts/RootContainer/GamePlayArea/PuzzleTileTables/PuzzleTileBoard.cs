using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 处理单词拼接的面板
/// </summary>
public class PuzzleTileBoard : UIWindow
{
    [Header("UI References")]
    [SerializeField] private Image puzzleProgress;
    [SerializeField] private Transform PageListTrans;
    [SerializeField] private Transform TileParents;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Text wordCountText;

    [Header("Settings")]
    public float snapSpeed = 15f;
    public float snapThreshold = 0.9f;
    public float stopVelocityThreshold = 50f;
    public float pageSwitchThreshold = 0.1f;

    [Header("Prefabs")]
    [SerializeField] private GameObject puzzleTilePrefab;
    [SerializeField] private GameObject tileHorizontalPrefab;

    // 内部状态
    private List<PuzzleTileItem> puzzleTileItems = new List<PuzzleTileItem>();
    private List<WordProgress> pageProgresses = new List<WordProgress>();
    private List<int> pageWords = new List<int>();
    private ObjectPool puzzlePool;
    private ObjectPool togglePool;
    private ObjectPool puzzleHorizonPool;
    private float[] pagePositions;
    private float targetPosition;
    private bool isSnapping;
    private bool wasDragging;
    private int pageCount;
    private float pageWidth;

    private float dragStartPosition; // 记录拖动开始时的位置
    private bool isDragStartRecorded = false; // 是否已记录拖动起始点
    private int currentPageIndex; // 当前页面索引

    private void Awake()
    {
        InitializePools();

        // 计算单页宽度
        pageWidth = 1050;
    }

    private void Update()
    {
        HandleAutoSnapping();
    }

    private void InitializePools()
    {
        if (puzzleTilePrefab == null)
        {
            puzzleTilePrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "PuzzleTileItem");
        }

        if (tileHorizontalPrefab == null)
        {
            tileHorizontalPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "TileHorizontal");
        }

        puzzlePool = new ObjectPool(puzzleTilePrefab, ObjectPool.CreatePoolContainer(transform, "puzzleTilePool"));
        togglePool = new ObjectPool(puzzleProgress.gameObject, ObjectPool.CreatePoolContainer(transform, "togglePool"));
        puzzleHorizonPool = new ObjectPool(tileHorizontalPrefab, ObjectPool.CreatePoolContainer(transform, "puzzleHorizonPool"));
    }

    private void HandleAutoSnapping()
    {
        // 自动对齐逻辑
        if (isSnapping)
        {
            scrollRect.horizontalNormalizedPosition = Mathf.Lerp(
                scrollRect.horizontalNormalizedPosition,
                targetPosition,
                snapSpeed * Time.deltaTime);

            if (Mathf.Abs(scrollRect.horizontalNormalizedPosition - targetPosition) < snapThreshold)
            {
                scrollRect.horizontalNormalizedPosition = targetPosition;
                isSnapping = false;
                // 更新当前页面索引
                currentPageIndex = GetCurrentPageIndex();
            }
        }

        // 检测拖动状态变化
        bool isDraggingNow = Mathf.Abs(scrollRect.velocity.x) > stopVelocityThreshold;

        // 拖动开始时记录初始位置
        if (!wasDragging && isDraggingNow)
        {
            dragStartPosition = scrollRect.horizontalNormalizedPosition;
            isDragStartRecorded = true;
            // 记录拖动开始时的页面索引
            currentPageIndex = GetCurrentPageIndex();
        }

        // 拖动结束时判断方向（最终优化版）
        if (wasDragging && !isDraggingNow && isDragStartRecorded)
        {
            float dragEndPosition = scrollRect.horizontalNormalizedPosition;
    
            // 计算实际像素位移（使用完整内容宽度）
            float pixelDelta = (dragEndPosition - dragStartPosition) * pageWidth;
            float absPixelDelta = Mathf.Abs(pixelDelta);
            const float pixelThreshold = 10f;

            // 仅当有效拖动距离超过阈值时执行方向判断
            if (absPixelDelta > pixelThreshold)
            {
                // 直接基于像素位移方向判断（避免归一化精度问题）
                if (pixelDelta > 0) 
                {
                    SnapToNextPage();
                    Debug.Log($"右滑翻页 位移:{pixelDelta:F2}px");
                }
                else 
                {
                    SnapToPreviousPage();
                    Debug.Log($"左滑翻页 位移:{pixelDelta:F2}px");
                }
            }
            else
            {
                // 微移情况：恢复原始位置
                SnapToNearestPage();
                Debug.Log($"微调吸附 位移:{pixelDelta:F2}px");
            }
    
            // 重置拖动标记
            isDragStartRecorded = false;
        }

        wasDragging = isDraggingNow;
    }

    public void Setup(StageInfo stageInfo, StageProgressData stageData)
    {
        Clear();

        // 创建优先显示列表：先显示已找到的成语，再显示未找到的
        List<string> sortedPuzzles = new List<string>();

        // 1. 添加所有已找到的成语（保持原始顺序）
        foreach (string puzzle in stageInfo.Puzzles)
        {
            if (stageData.FoundTargetPuzzles.Contains(puzzle))
            {
                sortedPuzzles.Add(puzzle);
            }
        }

        // 2. 添加所有未找到的成语
        foreach (string puzzle in stageInfo.Puzzles)
        {
            if (!stageData.FoundTargetPuzzles.Contains(puzzle))
            {
                sortedPuzzles.Add(puzzle);
            }
        }

        // 计算总页数（每页8个成语）
        int totalPages = Mathf.CeilToInt(sortedPuzzles.Count / 8.0f);

        // 创建页面指示器
        CreatePageToggles(totalPages,sortedPuzzles.Count);

        // 创建拼图项
        CreatePuzzleItems(sortedPuzzles, stageData);

        // 初始化滚动设置
        InitializeScrollSettings(totalPages);
    }

    private void CreatePageToggles(int totalPages,int totalWords)
    {
        for (int i = 0; i < totalPages; i++)
        {
            WordProgress toggle = togglePool.GetObject<WordProgress>(PageListTrans);
            toggle.gameObject.SetActive(true);
            toggle.ImageProgress.fillAmount = 0;
            pageProgresses.Add(toggle);

            int maxcount = totalWords - i * 8;
            if(maxcount >8) maxcount = 8;
            
            // 记录每页的成语数量
            pageWords.Add(maxcount);
        }
    }

    private void CreatePuzzleItems(List<string> sortedPuzzles, StageProgressData stageData)
    {
        const int puzzlesPerRow = 4;
        int rowCount = 0;
        HorizontalLayoutGroup currentRow = null;

        for (int i = 0; i < sortedPuzzles.Count; i++)
        {
            // 每行开始时创建新行
            if (i % puzzlesPerRow == 0)
            {
                currentRow = puzzleHorizonPool.GetObject<HorizontalLayoutGroup>(TileParents);
                rowCount++;
            }

            // 创建拼图块
            string puzzle = sortedPuzzles[i];
            PuzzleTileItem puzzleTileItem = puzzlePool.GetObject<PuzzleTileItem>(currentRow.transform);
            puzzleTileItem.SetPuzzleData(puzzle);

            // 根据找到状态设置显示
            if (stageData.FoundTargetPuzzles.Contains(puzzle))
            {
                puzzleTileItem.ShowBlock();
            }
            else
            {
                // puzzleTileItem.SetDefaultState();
            }

            puzzleTileItems.Add(puzzleTileItem);
        }
    }

    private void InitializeScrollSettings(int totalPages)
    {
        // 初始化页面位置
        InitializePagePositions(totalPages);       
    }

    private void InitializePagePositions(int totalPages)
    {
        pageCount = totalPages;
        pagePositions = new float[pageCount];

        if (pageCount <= 1)
        {
            pagePositions[0] = 0f;
        }
        else
        {
            for (int i = 0; i < pageCount; i++)
            {
                pagePositions[i] = i * 1.0f / (pageCount - 1);
            }
        }
    }

    // 新增方法：获取当前最近页面的索引
    private int GetCurrentPageIndex()
    {
        int nearestPage = 0;
        float minDistance = float.MaxValue;

        for (int i = 0; i < pageCount; i++)
        {
            float distance = Mathf.Abs(scrollRect.horizontalNormalizedPosition - pagePositions[i]);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestPage = i;
            }
        }
        return nearestPage;
    }

    // 新增方法：对齐到上一页
    private void SnapToPreviousPage()
    {
        int targetPage = Mathf.Max(0, currentPageIndex - 1);
        ScrollToPage(targetPage);
    }

    // 新增方法：对齐到下一页
    private void SnapToNextPage()
    {
        int targetPage = Mathf.Min(pageCount - 1, currentPageIndex + 1);
        ScrollToPage(targetPage);
    }


    private void SnapToNearestPage()
    {
        ScrollToPage(GetCurrentPageIndex());
    }

    public void ScrollToPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= pageCount) return;

        targetPosition = pagePositions[pageIndex];
        isSnapping = true;
        currentPageIndex = pageIndex; // 更新当前页面索引

        // // 更新当前页面的进度条
        // if (pageIndex < pageProgresses.Count)
        // {
        //     pageProgresses[pageIndex].ImageProgress.fillAmount = 1;
        // }
    }

    /// <summary>
    /// 重置棋盘，移除所有的瓷砖
    /// </summary>
    public void Clear()
    {
        float progress = (float)StageHexController.Instance.CurStageData.FoundTargetPuzzles.Count/StageHexController.Instance.CurStageInfo.Puzzles.Count;
        int  wordProgress = Mathf.FloorToInt(progress*100);
        wordCountText.text = $"{wordProgress}%";
        currentPageIndex = 0;
        puzzlePool.ReturnAllObjectsToPool();
        togglePool.ReturnAllObjectsToPool();
        puzzleHorizonPool.ReturnAllObjectsToPool();

        puzzleTileItems.Clear();
        pageProgresses.Clear();
        pageWords.Clear();
    }

    /// <summary>
    /// 获取给定单词的字母瓷砖
    /// </summary>
    public PuzzleTileItem GetPuzzleTile(string puzzle)
    {
        int wordCount = StageHexController.Instance?.CurStageData?.FoundTargetPuzzles?.Count ?? 0;

        if (wordCount > 0 && wordCount <= puzzleTileItems.Count)
        {
            PuzzleTileItem puzzleTileItem = puzzleTileItems[wordCount - 1];
            puzzleTileItem.SetPuzzleData(puzzle);

            int currentPage = Mathf.FloorToInt(wordCount / 8.0f);

            StartCoroutine(WaitTimeScrollToPage(currentPage));

            return puzzleTileItem;
        }

        return null;
    }

    IEnumerator WaitTimeScrollToPage(int currentPage)
    {
        yield return new WaitForSeconds(0.7f);
        ScrollToPage(currentPage);
    }

    /// <summary>
    /// 显示已找到的单词
    /// </summary>
    public void ShowPuzzleFound(string puzzle, int index, Action callback = null)
    {
        PuzzleTileItem puzzleTileItem = GetPuzzleTile(puzzle);
        if (puzzleTileItem == null) return;

        if (index == puzzleTileItem.TextPuzzles.Count - 1 &&
            StageHexController.Instance?.CurStageData?.PuzzleHints?.Contains(puzzle) == true)
        {
            puzzleTileItem.ShowText(callback);
            // StartCoroutine(ShowFlyButterCoin(puzzle, callback));
        }
        else
        {
            puzzleTileItem.ShowText(callback);
        }
        
        // 更新当前页面的进度条
        // 计算总页数（每页8个成语）
        int wordsInCurrentPage = pageWords[currentPageIndex];
        int leftWordCount = StageHexController.Instance.CurStageData.FoundTargetPuzzles.Count%8;

        if (leftWordCount == 0) leftWordCount = wordsInCurrentPage;
        pageProgresses[currentPageIndex].ImageProgress.fillAmount = (float)leftWordCount / wordsInCurrentPage;
        
        float progress = (float)StageHexController.Instance.CurStageData.FoundTargetPuzzles.Count/StageHexController.Instance.CurStageInfo.Puzzles.Count;
        
        int  wordProgress = Mathf.FloorToInt(progress*100);
        
        wordCountText.text = $"{wordProgress}%";
        
    }

    private void OnDisable()
    {
        Clear();
    }
}