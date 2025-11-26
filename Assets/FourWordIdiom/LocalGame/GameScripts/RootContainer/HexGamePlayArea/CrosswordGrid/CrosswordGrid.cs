using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using Unity.VisualScripting;
using System.Data;
using UnityEditor.Playables;


/// <summary>
/// 字块矩阵面板
/// </summary>
public class CrossPuzzleGrid : UIWindow,IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [SerializeField] private Animator animator;
    [SerializeField] private CanvasGroup PuzzleTitle;
    [SerializeField] private CanvasGroup GameBaseCanvas;
	public RectTransform RectT;
    [SerializeField] private GameObject PuzzleItemObj;//字块预制体
	private ObjectPool	letterTilePool;
	private RectTransform PuzzleParent;  
    // 修改数据结构为三维列表：行 -> 列 -> 层
    List<List<List<PuzzleTile>>> gridList = new List<List<List<PuzzleTile>>>();
    //选中字块列表
    private List<PuzzleTile> selectedPuzzleGrids = new List<PuzzleTile>();      
    private string selectedPuzzle;
    private int	activePointerId;
	/// <summary>
	/// 选中字块状态
	/// </summary>
	private TileSelectionState	selectState;
	/// <summary>
	/// 选中的开始字块
	/// </summary>
	private PuzzleTile selectStart;
	/// <summary>
	/// 选中的结束字块
	/// </summary>
	private PuzzleTile selectEnd;
	private int	numTilesMoving;
    
    const float LAYER_OFFSET = 10f; // 每层偏移量

    private StageProgressData curStageData
    {
        get { return StageHexController.Instance.CurStageData; }
    }

    /// <summary>
    /// 初始化此实例。
    /// </summary>
    public void Initialize()
	{
		CreatePuzzleParent();
        if (PuzzleItemObj == null)
        {
            PuzzleItemObj = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "TileView");
        }
		//创建对象池用于管理字块
		letterTilePool = new ObjectPool(PuzzleItemObj.gameObject, PuzzleParent,3, PoolBehaviour.CanvasGroup);        

    }


    protected override void OnEnable()
    {
        EventDispatcher.instance.OnPlayChoicePuzzle += OnPlayChoicePuzzle;
        SetPuzzleBoardState(false);
    }

    public void SetPuzzleBoardState(bool isblock)
    {
        GameBaseCanvas.blocksRaycasts = isblock;
    }

    /// <summary>
    ///根据保存的关卡数据创建字块矩阵
    /// </summary>
    public void CreatePuzzles(bool isAnim=false,bool isResetAnim=false)
	{
		Clear();			
		StartCoroutine(SetupGrid(isAnim,ShowTopPanel));			
	}
    
    public void ResetPuzzles(bool isResetAnim=false)
    {
        //Clear();			
        //SetGrid(isResetAnim);			
    }

    private void ShowTopPanel()
    {
        SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        StartCoroutine(ShowPuzzleTitle());
    }

    IEnumerator ShowPuzzleTitle()
    {
        //animator.Play("Showbuttons"); 
        //yield return new WaitForSeconds(0.1f); 
        EventDispatcher.instance.TriggerCheckShowTutorial();
        EnhancedVideoController.Instance.TogglePause();
        yield return new WaitForSeconds(0.3f); 
        PuzzleTitle.DOFade(1, 0.3f);
    }

	/// <summary>
	/// 清空界面
	/// </summary>
	public void Clear()
	{
        letterTilePool.ReturnAllObjectsToPool();
		gridList.Clear();
        ClearSelectData();
	}

	/// <summary>
	/// 清除选择数据
	/// </summary>
	public void ClearSelectData()
	{
		selectStart = null;
		selectEnd = null;
		selectState	= TileSelectionState.None;
		selectedPuzzle = "";
		selectedPuzzleGrids.Clear();
	}

	/// <summary>
	/// 创建字块矩阵
	/// </summary>
	IEnumerator SetupGrid(bool isanim,Action call=null)
	{
		BoardGame boardData = curStageData.BoardSnapshot;

        List<PuzzleTile> LayerpuzzleTiles = new List<PuzzleTile>();
        float tempscale = 0;
        
        float sizeoffsetw = 800*UIUtilities.GetScreenRatio();
        float sizeoffseth = (boardData.rows - boardData.minRow) >= 6 ? 140 : 0;

        //if (StageController.Instance.ActiveTileSize<=0||GameDataManager.MainInstance.UserData.CurrentStage!=curStageData.StageId||StageController.Instance.IsGMEnterStage)
        {
            float height= (RectT.rect.height-sizeoffseth ) /(boardData.rows-boardData.minRow+1);
            float width= (RectT.rect.width+sizeoffsetw)/(boardData.cols-boardData.minCol+1);
            float tileSize = Mathf.Min(width, height);
            
            if(tileSize>=220)
            {
                tileSize = sizeoffseth > 0 || (boardData.cols - boardData.minCol) >= 6 ? 215 : 230;
            }
            
            StageHexController.Instance.ActiveTileSize = tileSize;
        }			

        yield return new WaitForSeconds(1.2f);
        
		for (int row = 0; row < boardData.rows; row++)
		{
            // 添加新行的层列表
            gridList.Add(new List<List<PuzzleTile>>());           

            for (int col = 0; col < boardData.cols; col++)
			{
                // 添加新列的层列表
                gridList[row].Add(new List<PuzzleTile>());

                // 获取当前格子的所有字符（每个字符代表一层）
                List<char> layerChars = boardData.board[row][col];
                int layerCount = layerChars.Count;

                // 从顶层到底层遍历（索引0为最上层）
                for (int layer = 0; layer < layerCount; layer++)
                {
                    char letter = layerChars[layer];
                    bool isEmpty = letter == '\0';
                    int actualLayer = layerChars.Count - 1 - layer;

                    // 创建拼图块（新增layer参数）
                    PuzzleTile puzzleTile = new PuzzleTile(row, col, actualLayer, letter)
                    {
                        IsEmpty = isEmpty
                    };
                    if (!puzzleTile.IsEmpty)
                    {
                        // 从对象池获取TileView
                        TileView tileView = letterTilePool.GetObject<TileView>();   
                        Vector2 cellPos = Vector2.zero;
                        if((HexType)StageHexController.Instance.CurStageInfo.HexType==HexType.PingHexagon)
                        {
                            // 获取位置（考虑层级偏移）
                            cellPos = GetPingHexCellPosition(row, col, layer);     
                        }
                        else
                        {
                            // 获取位置（考虑层级偏移）
                            cellPos = GetJianCellPosition(row, col, layer);     
                        }
                        
                        // 设置位置和缩放
                        tileView.TileTransform.anchoredPosition = cellPos;
                        SetPuzzleItemScale(tileView.TileTransform);
                        tileView.SetupCharacter(letter);
                        puzzleTile.TileView = tileView;
                        puzzleTile.Layer = actualLayer; // 存储可视化层级
                        tileView.TileTransform.GetComponent<CanvasGroup>().alpha = 0;
                        tempscale= tileView.TileTransform.localScale.x;                        
                        // 设置层级关系：上层对象显示在顶层

                        if (layer > 0)
                        {
                            LayerpuzzleTiles.Add(puzzleTile);    
                        }
                        else
                        {
                            tileView.TileTransform.localScale = Vector3.zero;
                            tileView.TileTransform.SetAsFirstSibling();

                            // 动画处理
                            if (isanim)
                            {
                                tileView.TileTransform.DOScale(tempscale + 0.1f, 0.2f).OnComplete(() =>
                                {
                                    tileView.TileTransform.DOScale(tempscale, 0.2f);
                                });
                                tileView.TileTransform.GetComponent<CanvasGroup>().DOFade(1, 0.02f);

                                yield return new WaitForSeconds(0.03f);
                            }
                        }
                    }

                    // 添加到当前层
                    gridList[row][col].Add(puzzleTile);                    
                }  
			}
		}

        foreach (PuzzleTile item in LayerpuzzleTiles)
        {           
            item.TileView.TileTransform.SetAsFirstSibling();
            item.TileView.TileTransform.GetComponent<CanvasGroup>().DOFade(1, 0.02f);
        }
        
        if (isanim)
        { 
            yield return new WaitForSeconds(0.01f);
            call?.Invoke();
        }
    }       

    private Vector2 GetOffsetVector(int row, int col)
    {
        float offsetx = 80 * (col <= 1&&row <= 2 ? -1 : 1);
        float offsety = 80 * (row <= 2 ? -1 : 1);
        Vector2 vector2 = new Vector2(offsetx,offsety);
        return vector2;
    }

    private void ResetPuzzleAnim(PuzzleTile PuzzleGrid,Vector2 vector)
    {
        // Vector3 startPosition = PuzzleGrid.TileView.TileTransform.anchoredPosition;
        // Vector3 oldOffset = GetOffsetVector(PuzzleGrid.Row, PuzzleGrid.Column);
        // Vector3 controlPoint = new Vector3(startPosition.x + oldOffset.x, startPosition.y + oldOffset.y, 0);
        //
        // // 创建路径
        // Vector3[] path = { startPosition, controlPoint,vector };
        // char letter = PuzzleGrid.Letter;
        // // 沿路径移动
        // PuzzleGrid.TileView.TileTransform.DOLocalPath(path, 0.5f, PathType.CatmullRom).SetEase(Ease.InQuad).OnComplete(() =>
        // {
        //     PuzzleGrid.TileView.SetupCharacter(letter);
        // });
    }


    private PuzzleTile GetPuzzleGrid(char letter)
    {
        // 按行优先顺序搜索
        for (int row = 0; row < gridList.Count; row++)
        {
            // 按列顺序搜索
            for (int col = 0; col < gridList[row].Count; col++)
            {
                // 按层顺序搜索（从顶层到底层）
                List<PuzzleTile> puzzleTiles = gridList[row][col];
                for (int layer = 0; layer < puzzleTiles.Count; layer++)
                {
                    PuzzleTile puzzleTile = puzzleTiles[layer];

                    // 检查是否匹配且未被移除
                    if (puzzleTile != null && puzzleTile.Letter == letter)
                    {
                        // 从层列表中移除
                        puzzleTiles.RemoveAt(layer);
                  
                        return puzzleTile;
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 获取六边形字块位置（平顶六边形，行对齐）
    /// </summary>
    private Vector2 GetPingHexCellPosition(int row, int col, int layer)
    {
        float activeTileSize = StageHexController.Instance.ActiveTileSize;

        // 平顶六边形参数（行对齐）
        float hexWidth = activeTileSize * Mathf.Sqrt(3) / 2.3f;                  // 六边形宽度（水平方向）
        float hexHeight = activeTileSize * Mathf.Sqrt(3) / 1.75f;  // 六边形高度（垂直方向）
        float horizontalSpacing = hexWidth;                  // 列间距（水平方向，无重叠）
        float verticalSpacing = hexHeight * 0.85f;           // 行间距（垂直方向，考虑重叠）

        int cols=curStageData.BoardSnapshot.cols-curStageData.BoardSnapshot.minCol;
        int rows=curStageData.BoardSnapshot.rows-curStageData.BoardSnapshot.minRow;

        // 计算网格尺寸（列控制水平尺寸，行控制垂直尺寸）
        float totalGridWidth = cols * horizontalSpacing;
        float totalGridHeight = rows * verticalSpacing;

        // 动态计算起始点（居中）
        Vector2 bottomLeft = new Vector2(
            -totalGridWidth / 2f,
            -totalGridHeight / 2f
        );
        
        float soffsetx = 0.5f;
        
        // 计算基础位置（列控制X轴，行控制Y轴）
        float xPos = bottomLeft.x + (col-curStageData.BoardSnapshot.minCol+soffsetx) * horizontalSpacing;
        float yPos = bottomLeft.y + (row-curStageData.BoardSnapshot.minRow-soffsetx/2f) * verticalSpacing;        
        
        // 为奇数列添加垂直偏移（六边形网格特性）
        if ((col & 1) == 1)  // 位运算判断奇数列（比取模运算更快）
        {
            yPos += hexHeight * 0.42f;  // 下移半个垂直间距
        }
        // 取消注释启用层级偏移
        if (layer>=0)
        {
            float layerOffset =layer * LAYER_OFFSET;
            yPos -= layerOffset; // 向下偏移
        }

        return new Vector2(xPos, yPos);
    }

    ///// <summary>
    ///// 获取六边形格子位（尖顶六边形，列对齐）
    ///// </summary>
    private Vector2 GetJianCellPosition(int row, int col, int layer)
    {
        float activeTileSize = StageHexController.Instance.ActiveTileSize;

        // 六边形几何参数计算
        float hexHeight = activeTileSize;                    // 六边形高度（垂直方向）
        float hexWidth = activeTileSize;  // 六边形宽度（水平方向）
        float horizontalSpacing = hexWidth*0.9f;                   // 列间距
        float verticalSpacing = hexHeight * 0.75f;            // 行间距（考虑重叠）
        
        int cols=curStageData.BoardSnapshot.cols-curStageData.BoardSnapshot.minCol;
        int rows=curStageData.BoardSnapshot.rows-curStageData.BoardSnapshot.minRow;

        // 计算网格尺寸（列控制水平尺寸，行控制垂直尺寸）
        float totalGridWidth = cols * horizontalSpacing;
        float totalGridHeight = rows * verticalSpacing;
        
        //动态计算起始点（居中）
        Vector2 bottomLeft = new Vector2(
            -totalGridWidth / 2f,
            -totalGridHeight / 2f
        );
        
        float soffsetx = curStageData.BoardSnapshot.minCol%2==0?0.5f:0f;

        // 计算当前格子位置
        float xPos = bottomLeft.x + (col-curStageData.BoardSnapshot.minCol+soffsetx) * horizontalSpacing;
        float yPos = bottomLeft.y + (row-curStageData.BoardSnapshot.minRow+soffsetx) * verticalSpacing;

        // 奇数行横向偏移（蜂窝状交错）
        if (row % 2 == 1)
        {
            xPos += horizontalSpacing / 2f;
        }
        // 计算偏移量：层级越低（值越小），偏移越大
        if (layer>=0)
        {
            float layerOffset = layer * LAYER_OFFSET;
            yPos -= layerOffset;
        }
        return new Vector2(xPos, yPos);
    }

    /// <summary>
    /// 获取指定位置的最大层级数（统计非空字符的数量）
    /// </summary>
    private int GetMaxLayerAtPosition(int row, int col)
    {
        // 验证行列是否在范围内
        if (row < 0 || row >= gridList.Count) return 0;
        if (col < 0 || col >= curStageData.BoardSnapshot.board[row].Count) return 0;

        // 获取当前位置的层级值列表
        var layers = curStageData.BoardSnapshot.board[row][col];
        int count = 0;
    
        // 遍历所有层级值，统计非空字符的数量
        foreach (var value in layers)
        {
            if (value != '\0') // 检查字符是否为空
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// 设置字块缩放
    /// </summary>
    private void SetPuzzleItemScale(RectTransform PuzzleTileRectT)
	{
		float xScale	= StageHexController.Instance.ActiveTileSize / PuzzleTileRectT.rect.width;
		float yScale	= StageHexController.Instance.ActiveTileSize / PuzzleTileRectT.rect.height;
		float scale		= Mathf.Min(xScale, yScale);

        PuzzleTileRectT.localScale = new Vector3(scale, scale, 1);
	}

    /// <summary>
    ///创建字块父对象
    /// </summary>
    private void CreatePuzzleParent()
	{
        PuzzleParent = new GameObject("grid_container").AddComponent<RectTransform>();
        PuzzleParent.SetParent(transform, false);
        PuzzleParent.anchorMin = Vector2.zero;
        PuzzleParent.anchorMax = Vector2.one;
        PuzzleParent.offsetMin = Vector2.zero;
        PuzzleParent.offsetMax = Vector2.zero;
	}

    public void OnDrag(PointerEventData eventData)
    {
        //Debug.LogError("触发拖曳事件"+ numTilesMoving+ " activePointerId:" + activePointerId+ " pointerId:" + eventData.pointerId + "curStageData: " + curStageData);
        if (activePointerId != -1)
        {
            activePointerId = eventData.pointerId;
        }
        if (numTilesMoving > 0 || activePointerId != eventData.pointerId || curStageData == null) return;             
        UpdateSelected(eventData.position);
    }

    /// <summary>
    /// 更新字块状态
    /// </summary>
    private void UpdateSelected(Vector2 screenPosition)
    {
        Vector2 localPosition = StageHexController.Instance.ScreenToLocalPosition(screenPosition,PuzzleParent);
        PuzzleTile PuzzleGrid = GetPuzzleGridAt(localPosition);

        if (PuzzleGrid == null) return;          

        if (!PuzzleGrid.IsEmpty)
        {
            switch (selectState)
            {
                case TileSelectionState.None:                       
                    selectStart = PuzzleGrid;
                    selectState = TileSelectionState.Selected;
                    SetSelectEnd(PuzzleGrid);
                    break;
                case TileSelectionState.Selected:
                    SetSelectEnd(PuzzleGrid);
                    //Debug.Log("拖曳选择字块" + PuzzleGrid.Letter);
                    break;                   
            }
            //Debug.LogError("拖曳字块"+PuzzleGrid.Letter);                    
            UpdateSelectedBoard();
            StageHexController.Instance.ResetInactivityTimer();
            ClearPuzzleGrid();
        }
    }

    private void ClearPuzzleGrid()
    {
        if (!string.IsNullOrEmpty(StageHexController.Instance.tipPuzzle));
        {
            List<PuzzleTile> puzzleDatas= GetPuzzleTileRowCol(StageHexController.Instance.tipPuzzle);
    
            foreach (PuzzleTile puzzleTile in puzzleDatas)
            {
                if (puzzleTile.IsEmpty) continue;
                puzzleTile.TileView.StopPulseAnimation();
            }

            StageHexController.Instance.tipPuzzle = "";
        }
    }

    /// <summary>
    /// 设置选中的结束字块
    /// </summary>
    private void SetSelectEnd(PuzzleTile PuzzleGrid)
    {
        // 开始字块跟结束字块的行或者列一致，或者在右斜方向(尖顶六边形消除逻辑)
        
        if((HexType)StageHexController.Instance.CurStageInfo.HexType==HexType.JianHexagon)
        {
            //Debug.Log("尖顶六边形消除逻辑");
            if (PuzzleGrid.Row == selectStart.Row || IsRightJianDirection(selectStart, PuzzleGrid)
                                                  || IsLeftJianDirection(selectStart, PuzzleGrid))
            {
                selectEnd = PuzzleGrid;
            }
        }
        else
        {
            
            //开始字块跟结束字块的行或者列一致，或者在右斜方向(平顶六边形消除逻辑)
            if (PuzzleGrid.Column == selectStart.Column || IsRightPingDirection(selectStart, PuzzleGrid)
                                                        || IsLeftPingDirection(selectStart, PuzzleGrid))
            {
                //Debug.Log("结束字块" + PuzzleGrid.Letter);
                selectEnd = PuzzleGrid;
            }    
        }
    }


    /// <summary>
    /// 根据坐标获取字块（支持多层检测）
    /// </summary>
    private PuzzleTile GetPuzzleGridAt(Vector2 localPosition)
    {
        // 计算点击检测范围（基于字块大小）
        float tileSize = StageHexController.Instance.ActiveTileSize;
        float halfSize = tileSize / 2f;
        // 扩大检测范围（适应六边形形状）
        float detectionSize = tileSize * 0.5f;

        // 从顶层到底层遍历（优先选择顶层）
        for (int row = 0; row < gridList.Count; row++)
        {
            for (int col = 0; col < gridList[row].Count; col++)
            {
                // 获取当前格子的所有层
                List<PuzzleTile> layers = gridList[row][col];
                if (layers == null || layers.Count == 0) continue;

                // 从顶层开始检测（layer=0为最上层）
                for (int layer = 0; layer < layers.Count; layer++)
                {
                    PuzzleTile puzzleTile = layers[layer];
                    if (puzzleTile.IsEmpty) continue;
                    
                    Vector2 cellPos = Vector2.zero;
                    if((HexType)StageHexController.Instance.CurStageInfo.HexType==HexType.PingHexagon)
                    {
                        // 获取位置（考虑层级偏移）
                        cellPos = GetPingHexCellPosition(row, col, layer);     
                    }
                    else
                    {
                        // 获取位置（考虑层级偏移）
                        cellPos = GetJianCellPosition(row, col, layer);     
                    }
                  
                    if (localPosition.x >= cellPos.x - halfSize && localPosition.x <= cellPos.x + halfSize
                        && localPosition.y >= cellPos.y - halfSize && localPosition.y <= cellPos.y + halfSize)
                    {
                        return puzzleTile;
                    }

                }
            }
        }
        return null;
    }
        

    public void OnPointerDown(PointerEventData eventData)
    {
        //Debug.LogError("字块按下事件" + numTilesMoving + " activePointerId:" + activePointerId + " pointerId:" + eventData.pointerId + "curStageData: " + curStageData);

        if (numTilesMoving > 0)
        {
            return;
        }

        if (activePointerId != -1)
        {
            activePointerId = eventData.pointerId;
        }

        if (activePointerId == eventData.pointerId)
        {
            UpdateSelected(eventData.position);
        }
                          
    }
   
    public void OnPointerUp(PointerEventData eventData)
    {
        //Debug.LogError("字块按住松开事件" + numTilesMoving + " activePointerId:" + activePointerId + " pointerId:" + eventData.pointerId + "curStageData: " + curStageData);
        if (numTilesMoving > 0 || activePointerId != eventData.pointerId) return;
        //Debug.LogError("字块按住松开事件" + eventData.pointerId+" "+ eventData.position);
        UpdateSelected(eventData.position);
        if (selectState == TileSelectionState.Selected &&selectedPuzzleGrids.Count>1)
        {
            //Debug.LogWarning("字块按住松开事件" + eventData.pointerId+" "+ eventData.position+"   "+ selectedPuzzle);
            ClearBoardPuzzles(selectedPuzzleGrids);
            EventDispatcher.instance.TriggerLetterSelected(selectedPuzzle, GetPuzzleGridRowCol(selectedPuzzleGrids));
        }

        //选中一个字块时，不判断为连击失败，且不播放连词失败的音效
        if (selectedPuzzleGrids.Count==1)
        {
            List<int[]> positions = GetPuzzleGridRowCol(selectedPuzzleGrids);
            HideChoicePuzzle(positions);
            EventDispatcher.instance.TriggerPlayChoicePuzzle(positions, false);
        }
        ClearSelectData();
    }

    /// <summary>
    /// 保存选中字块的行列
    /// </summary>
    private List<int[]> GetPuzzleGridRowCol(List<PuzzleTile> PuzzleGrids)
    {
        List<int[]> PuzzleGridRowCol = new List<int[]>();
        foreach (PuzzleTile grid in PuzzleGrids)
        {
            PuzzleGridRowCol.Add(new int[] { grid.Row, grid.Column });
        }         
        return PuzzleGridRowCol;
    }

    /// <summary>
    /// 清除展示区字块
    /// </summary>
    private void ClearBoardPuzzles(List<PuzzleTile> PuzzleGrids)
    {
        foreach (PuzzleTile grid in PuzzleGrids)
        {
            grid.TileView.SetSelectionState(false, true);
        }           
    }

    /// <summary>
    /// 更新棋盘字块显示
    /// </summary>
    private void UpdateSelectedBoard()
    {
        if (selectStart == null) return;

        //清除棋盘字块
        ClearBoardPuzzles(selectedPuzzleGrids);
        
        if((HexType)StageHexController.Instance.CurStageInfo.HexType==HexType.PingHexagon)
        {
            //Debug.Log("平顶六边形消除逻辑");
            SetPingSelectedBoard(selectStart, selectEnd);
        }
        else
        {
            //设置棋盘选中字块
            SetJianSelectedBoard(selectStart, selectEnd);
           
        }
    }

    /// <summary>
    /// 设置棋盘选中字块（平顶六边形—— 支持横向和斜向选择）
    /// </summary>
    private void SetPingSelectedBoard(PuzzleTile start, PuzzleTile end)
    {
        // 判断选择方向：竖向或斜向
        bool isVertical = start.Column == end.Column;
        bool isLeftDiagonal = IsLeftPingDirection(start, end);
        bool isRightDiagonal = IsRightPingDirection(start, end);

        //Debug.Log($"选择方向 - 竖向:{isVertical}左向:{isLeftDiagonal} 右向:{isRightDiagonal} {start.Letter}{end.Letter}");      
    
        // 如果不是有效的选择方向，则只选中起点
        if (!isVertical && !isLeftDiagonal && !isRightDiagonal)
        {
            end = start;
        }

        HashSet<PuzzleTile> lastSelectedPuzzleGrids = new HashSet<PuzzleTile>(selectedPuzzleGrids);
        selectedPuzzle = "";
        selectedPuzzleGrids.Clear();

        // 计算步数和方向
        int steps = isVertical ? Math.Abs(end.Row - start.Row) :
                    Math.Abs(end.Column - start.Column); // 斜向用行差计算步数

        //Debug.Log($"左斜向用列差计算步数:{steps}");

        for (int i = 0; i <= steps; i++)
        {
            int row=0, col;

            if (isLeftDiagonal)
            {
                // 计算行移动方向
                int colStep = Math.Sign(end.Column - start.Column);
                // int rowStep = end.Row - start.Row;
                // Debug.LogError($"{start.Letter}:{start.Row}  {end.Letter}:{end.Row}行相隔数:{rowStep}");

                bool isleftup = end.TileView.TileTransform.anchoredPosition.y > start.TileView.TileTransform.anchoredPosition.y;
                
                if (isleftup)
                {
                    if (start.Column % 2 != 0)
                    {
                        row = start.Row + (i + 1) / 2;
                    }
                    else
                    {
                        row = start.Row + i / 2;
                    }
                    //Debug.Log($"左斜向上计算位置:{row}");
                }
                else
                {
                    if (start.Column % 2 != 0)
                    {
                        // if (i == 1)
                        // {
                        //     row = start.Row - i;
                        // }
                        // else
                        // {
                            row = start.Row - i / 2;
                        //}
                    }
                    else
                    {
                        row = start.Row - (i + 1) / 2;
                    }
                    //Debug.Log($"左斜向下计算位置:{row}:");
                }
                
                col = start.Column + i * colStep;
                //Debug.Log($"左斜向每步计算的位置:{row}:{col}");
               
            }
            else if (isRightDiagonal) // isRightDiagonal
            {
                // 计算行移动方向
                int colStep = Math.Sign(end.Column - start.Column);
                //int deltaRow = Math.Sign(end.Row - start.Row);

                bool isrightup = end.TileView.TileTransform.anchoredPosition.y > start.TileView.TileTransform.anchoredPosition.y;
                
                if (isrightup) // 行号减小（向上移动）
                {
                    if (start.Column % 2 == 0)
                    {
                        row = start.Row + i / 2;
                    }
                    else
                    {
                        row = start.Row + (i + 1) / 2;
                    }
                }
                else
                {
                    if (start.Column % 2 == 0)
                    {
                        row = start.Row - (i + 1) / 2;                        
                    }
                    else
                    {
                        row = start.Row - i / 2;
                    }      
                }
                col = start.Column + i * colStep;
                //Debug.Log($"右斜向计算位置:{row}:{col}");
            }
            else
            {
                // 横向：行不变，列变化
                col = start.Column;
                row = start.Row + i * Math.Sign(end.Row - start.Row);
            }

            // 检查边界
            if (row < 0 || row >= gridList.Count || col < 0 || col >= gridList[0].Count)
                break;

            if (gridList[row][col].Count <= 0) break;

            // 获取该位置的最大层级
            int maxLayer = GetMaxLayerAtPosition(row, col);

            PuzzleTile puzzleGrid = gridList[row][col][0];

            //Debug.Log("选中字块信息：" + puzzleGrid.Letter + "层级:" + maxLayer);

            // 遇到空白格退出设置选中词
            if (puzzleGrid.IsEmpty || puzzleGrid.Letter == '\0')
            {
                break;
            }

            bool justSelected = !lastSelectedPuzzleGrids.Contains(puzzleGrid);

            puzzleGrid.TileView.SetSelectionState(true, justSelected);
            selectedPuzzle += puzzleGrid.Letter;
            selectedPuzzleGrids.Add(puzzleGrid);

            // 刚选中
            if (justSelected)
            {
                if (selectedPuzzle.Length > 0)
                {
                    AudioManager.Instance.PlaySoundEffect("Puzzle" + selectedPuzzle.Length);
                }
            }
        }

        if (selectedPuzzleGrids.Count < lastSelectedPuzzleGrids.Count)
        {
            AudioManager.Instance.TriggerVibration(1, 10);
            AudioManager.Instance.PlaySoundEffect("Puzzle" + lastSelectedPuzzleGrids.Count);
        }

        EventDispatcher.instance.TriggerShowSelectedPuzzle(selectedPuzzle);
    }

    /// <summary>
    /// 设置棋盘选中字块（尖顶六边形—— 支持横向和斜向选择）
    /// </summary>
    private void SetJianSelectedBoard(PuzzleTile start, PuzzleTile end)
    {
        // 判断选择方向：横向或斜向
        bool isHorizontal = start.Row == end.Row;
        bool isLeftDiagonal = IsLeftJianDirection(start, end);
        bool isRightDiagonal = IsRightJianDirection(start, end);

        Debug.Log($"选择方向 - 横向:{isHorizontal}左下斜:{isLeftDiagonal} 右下斜:{isRightDiagonal} {start.Letter}{end.Letter}");

        // 如果不是有效的选择方向，则只选中起点
        if (!isHorizontal && !isRightDiagonal&& !isLeftDiagonal)
        {
            end = start;
        }

        HashSet<PuzzleTile> lastSelectedPuzzleGrids = new HashSet<PuzzleTile>(selectedPuzzleGrids);
        selectedPuzzle = "";
        selectedPuzzleGrids.Clear();

        // 计算步数和方向
        int steps = isHorizontal ? Math.Abs(end.Column - start.Column) :
                    Math.Abs(end.Row - start.Row); // 斜向用行差计算步数

        Debug.Log($"斜向用行差计算步数:{steps}");

        for (int i = 0; i <= steps; i++)
        {
            int row, col;

            if (isLeftDiagonal)
            { 
                // 计算行移动方向
                int rowStep = Math.Sign(end.Row - start.Row);
                row = start.Row + i * rowStep;

                if (start.Row % 2 == 0)
                {
                    col = start.Column - (i+1) / 2;
                }
                else
                {
                    col = start.Column - i / 2;
                }

                Debug.Log($"左斜向计算位置:{row}:{col}");
            }
            else if(isRightDiagonal) // isRightDiagonal
            {

                // 计算行移动方向
                int rowStep = Math.Sign(end.Row - start.Row);
                row = start.Row + i * rowStep;

                if (start.Row % 2 == 0)
                {
                    col = start.Column + i / 2;
                }
                else
                {
                    col = start.Column + (i + 1) / 2;
                }

                Debug.Log($"右斜向计算位置:{row}:{col}");
            }
            else
            {
                // 横向：行不变，列变化
                row = start.Row;
                col = start.Column + i * Math.Sign(end.Column - start.Column);
            }            

            // 检查边界
            if (row < 0 || row >= gridList.Count || col < 0 || col >= gridList[0].Count)
                break;

            if (gridList[row][col].Count <= 0) break;

            // 获取该位置的最大层级
            int maxLayer = GetMaxLayerAtPosition(row, col);            

            PuzzleTile puzzleGrid = gridList[row][col][0];

            Debug.Log("选中字块信息：" + puzzleGrid.Letter+"层级:"+maxLayer);

            // 遇到空白格退出设置选中词
            if (puzzleGrid.IsEmpty||puzzleGrid.Letter=='\0')
            {
                break;
            }

            bool justSelected = !lastSelectedPuzzleGrids.Contains(puzzleGrid);

            puzzleGrid.TileView.SetSelectionState(true, justSelected);
            selectedPuzzle += puzzleGrid.Letter;
            selectedPuzzleGrids.Add(puzzleGrid);

            // 刚选中
            if (justSelected)
            {
                if (selectedPuzzle.Length > 0)
                {
                    AudioManager.Instance.PlaySoundEffect("Puzzle" + selectedPuzzle.Length);
                }
            }
        }

        if (selectedPuzzleGrids.Count < lastSelectedPuzzleGrids.Count)
        {
            AudioManager.Instance.TriggerVibration(1, 10);
            AudioManager.Instance.PlaySoundEffect("Puzzle" + lastSelectedPuzzleGrids.Count);
        }

        EventDispatcher.instance.TriggerShowSelectedPuzzle(selectedPuzzle);
    }

    /// <summary>
    /// 判断是否是左斜方向（包括左上和左下方向_平顶六边形）
    /// </summary>
    private bool IsLeftPingDirection(PuzzleTile start, PuzzleTile end)
    {
        // 计算行列差值
        int deltaRow = end.Row - start.Row;
        int deltaCol = end.Column - start.Column;

        //Debug.Log($"{start.Letter}_{start.Row}:{start.Column} {end.Letter}_{end.Row}:{end.Column}是否为左斜方向");

        if (deltaCol > 0) return false;

        // 左上方向（行号减小，列号减小）的检查
        if (deltaRow >= 0) // 行号减小（向上移动）
        {
            // 左上方向需要满足：每移动一行，列减少约0.5
            // 数学关系：2 * deltaCol ≈ deltaRow
            if (Mathf.Abs(2 * deltaRow + deltaCol) <= 1)
            {
                //Debug.Log($"判断方向{start.Letter} {end.Letter}为左上方向的字块");
                return true;
            }
        }
        // 左下方向（行号增加，列号减小）的检查
        else if (deltaRow < 0&& deltaCol<0) // 行号增加（向下移动）
        {
            // 左下方向需要满足：每移动一行，列减少约0.5
            // 数学关系：2 * deltaCol ≈ -deltaRow
            if (Mathf.Abs(2 * deltaRow - deltaCol) <= 1)
            {
                //Debug.Log($"判断方向{start.Letter} {end.Letter}为左下方向的字块");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断是否是右斜方向（包括右上和右下方向_平顶六边形）
    /// </summary>
    private bool IsRightPingDirection(PuzzleTile start, PuzzleTile end)
    {
        // 计算行列差值
        int deltaRow = end.Row - start.Row;
        int deltaCol = end.Column - start.Column;

        //Debug.Log($"{start.Letter}_{start.Row}:{start.Column} {end.Letter}_{end.Row}:{end.Column}是否为右斜方向");

        if (deltaCol < 0) return false;

        // 右下方向（行号减小，列号增加）的检查      
        if (deltaRow<0) // 行号减小（向上移动）
        {
            // 右上方向需要满足：每移动一行，列增加约0.5
            // 数学关系：2 * deltaCol ≈ -deltaRow
            if (Mathf.Abs(2 * deltaRow + deltaCol) <= 1)
            {
                //Debug.Log($"{start.Letter} {end.Letter}为右下方向的字块");
                return true;
            }
        }
        // 右上方向（行号增加，列号增加）的检查
        else if (deltaRow >= 0) // 行号增加（向下移动）
        {
            // 右下方向需要满足：每移动一行，列增加约0.5
            // 数学关系：2 * deltaCol ≈ deltaRow
            if (Mathf.Abs(2 * deltaRow - deltaCol) <= 1)
            {
                //Debug.Log($"{start.Letter} {end.Letter}为右上方向的字块");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断是否是左斜方向（包括左上和左下方向_尖顶六边形）
    /// </summary>
    private bool IsLeftJianDirection(PuzzleTile start, PuzzleTile end)
    {
        // 计算行列差值
        int deltaRow = end.Row - start.Row;
        int deltaCol = end.Column - start.Column;
    
        Debug.Log($"{start.Letter}_{start.Row}:{start.Column} {end.Letter}_{end.Row}:{end.Column}是否为左斜方向");
    
        if (deltaCol >= 0) return false;
    
        // 左上方向（行号减小，列号减小）的检查
        if (deltaRow < 0) // 行号减小（向上移动）
        {
            // 左上方向需要满足：每移动一行，列减少约0.5
            // 数学关系：2 * deltaCol ≈ deltaRow
            if (Mathf.Abs(2 * deltaCol - deltaRow) <= 1)
            {
                Debug.Log($"{start.Letter} {end.Letter}为左上方向的字块");
                return true;
            }
        }
        // 左下方向（行号增加，列号减小）的检查
        else if (deltaRow > 0) // 行号增加（向下移动）
        {
            // 左下方向需要满足：每移动一行，列减少约0.5
            // 数学关系：2 * deltaCol ≈ -deltaRow
            if (Mathf.Abs(2 * deltaCol + deltaRow) <= 1)
            {
                Debug.Log($"{start.Letter} {end.Letter}为左下方向的字块");
                return true;
            }
        }
    
        return false;
    }

    /// <summary>
    /// 判断是否是右斜方向（包括右上和右下方向）
    /// </summary>
    private bool IsRightJianDirection(PuzzleTile start, PuzzleTile end)
    {
        // 计算行列差值
        int deltaRow = end.Row - start.Row;
        int deltaCol = end.Column - start.Column;
    
        Debug.Log($"{start.Letter}_{start.Row}:{start.Column} {end.Letter}_{end.Row}:{end.Column}是否为右斜方向");
    
        if (deltaCol < 0) return false;
    
        // 右下方向（行号减小，列号增加）的检查      
        if (deltaRow < 0) // 行号减小（向上移动）
        {
            // 右上方向需要满足：每移动一行，列增加约0.5
            // 数学关系：2 * deltaCol ≈ -deltaRow
            if (Mathf.Abs(2 * deltaCol + deltaRow) <= 1)
            {
                Debug.Log($"{start.Letter} {end.Letter}为右下方向的字块");
                return true;
            }
        }
        // 右上方向（行号增加，列号增加）的检查
        else if (deltaRow > 0) // 行号增加（向下移动）
        {
            // 右下方向需要满足：每移动一行，列增加约0.5
            // 数学关系：2 * deltaCol ≈ deltaRow
            if (Mathf.Abs(2 * deltaCol - deltaRow) <= 1)
            {
                Debug.Log($"{start.Letter} {end.Letter}为右上方向的字块");
                return true;
            }
        }
    
        return false;
    }


    /// <summary>
    /// 隐藏选中词语
    /// </summary>
    /// <param name="PuzzleGridPositions"> 选中字母位置列表</param>
    /// <param name="isValid">是否有效</param>
    public void HideChoicePuzzle(List<int[]> PuzzleGridPositions)
    {
        List<PuzzleTile> PuzzleGrids = GetPuzzleGridsAtPos(PuzzleGridPositions);

        foreach (PuzzleTile Puzzle in PuzzleGrids)
        {
             //Puzzle.TileView.TriggerErrorState(true);
             Puzzle.TileView.HideChoice();
        }
    }

    /// <summary>
    /// 播放选中词语
    /// </summary>
    /// <param name="PuzzleGridPositions"> 选中字母位置列表</param>
    /// <param name="isValid">是否有效</param>
    public void OnPlayChoicePuzzle(List<int[]> PuzzleGridPositions,bool isValid)
    {
        List<PuzzleTile> PuzzleGrids = GetPuzzleGridsAtPos(PuzzleGridPositions);
        if(PuzzleGrids.Count<=1) return;
        bool isx = PuzzleGrids[0].Row == PuzzleGrids[1].Row;

        for (int i = 0; i < PuzzleGrids.Count; i++)
        {
            PuzzleTile Puzzle = PuzzleGrids[i];
        
            //if (isValid)
            //{
            //    //SoundManager.Instance.Play("Puzzle_already_found");
            //    //Puzzle.PuzzleItem.PlayDown();
            //}
            //else
            Puzzle.TileView.TriggerErrorState(isx);
        
            //if (i == 0)
            //{
            //    Puzzle.PuzzleItem.Vibrate();
            //}
        }

        // if (PuzzleGrids.Count > 1)
        // {
            AudioManager.Instance.PlaySoundEffect("xuanzhecuowu");
            AudioManager.Instance.TriggerVibration(1, 10);
        //}
    }


    /// <summary>
    /// 根据位置信息获取组成词语的字块列表（按词语顺序）
    /// </summary>
    public List<PuzzleTile> GetPuzzleTileRowCol(string puzzle)
    {
        List<PuzzleTile> resultTiles = new List<PuzzleTile>();

        if (string.IsNullOrEmpty(puzzle))
        {
            //Debug.LogWarning("Invalid idiom data provided");
            return resultTiles;
        }

        List<IdiomData> currentIdioms = StageHexController.Instance.CurStageInfo.idioms;

        IdiomData idiomData = null;

        // 首先尝试精确匹配（考虑大小写）
        foreach (IdiomData idiom in currentIdioms)
        {
            if (idiom.word.Equals(puzzle))
            {
                idiomData= idiom;
                break;
            }
        }

        if(idiomData==null) return resultTiles;
       
        foreach (IdiomBlock block in idiomData.blocks)
        {
            Vector2Int pos = block.position;

            int layers = gridList[pos.x][pos.y].Count;

            if (layers > 0)
            {
                // 获取该位置的所有字块           
                PuzzleTile tilesAtPosition = gridList[pos.x][pos.y][0];
          
                if (tilesAtPosition != null)
                {
                    resultTiles.Add(tilesAtPosition);
                }
            }

            
        }

        return resultTiles;
    }    

    /// <summary>
    /// 根据坐标列表获取字块列表（支持多层结构）
    /// </summary>
    public List<PuzzleTile> GetPuzzleGridsAtPos(List<int[]> puzzleGridPositions)
    {
        List<PuzzleTile> resultTiles = new List<PuzzleTile>();

        foreach (var pos in puzzleGridPositions)
        {
            // 验证坐标格式
            if (pos == null || pos.Length < 2)
            {
                resultTiles.Add(null);
                continue;
            }

            int row = pos[0];
            int col = pos[1];

            // 验证行索引范围
            if (row < 0 || row >= gridList.Count)
            {
                resultTiles.Add(null);
                continue;
            }

            // 验证列索引范围
            if (col < 0 || col >= gridList[row].Count)
            {
                resultTiles.Add(null);
                continue;
            }

            // 获取该位置的所有层
            List<PuzzleTile> layers = gridList[row][col];

            // 查找顶层可见字块
            PuzzleTile topTile = null;
            foreach (var tile in layers)
            {
                if (tile != null && !tile.IsEmpty)
                {
                    topTile = tile;
                    break; // 找到第一个有效字块即停止
                }
            }

            resultTiles.Add(topTile);
        }

        return resultTiles;
    }

    /// <summary>
    /// 移除组成的词语
    /// </summary>
    public void RemovePuzzleFound(List<int[]> gridCellPositions)
    {
        List<PuzzleTile> gridCells = GetPuzzleGridsAtPos(gridCellPositions);

        // 修复1：按行列分组处理，避免交叉修改
        var positionGroups = gridCells
            .GroupBy(tile => new { tile.Row, tile.Column })
            .ToList();

        foreach (var group in positionGroups)
        {
            int row = group.Key.Row;
            int col = group.Key.Column;

            // 修复2：先处理数据层再处理视图
            List<char> layers = curStageData.BoardSnapshot.board[row][col];

            // 修复3：正确移除最高层级（最后一项）
            if (layers.Count > 0)
            {
                layers[0] = '\0';
                layers.RemoveAt(0); // 关键修复：移除最后一项
            }

            // 修复4：批量处理视图
            UpdateRemainingTiles(row, col, layers);
        }
    }

    /// <summary>
    /// 更新剩余图块的显示（线程安全版本）
    /// </summary>
    private void UpdateRemainingTiles(int row, int col, List<char> layers)
    {
        // 修复5：添加安全校验
        if (row < 0 || row >= gridList.Count) return;
        if (col < 0 || col >= gridList[row].Count) return;
        
        List<PuzzleTile> tiles = gridList[row][col];
        int targetCount = Mathf.Min(tiles.Count, layers.Count);

        // 修复6：使用对象池安全回收
        for (int i = targetCount; i < tiles.Count; i++)
        {
            PuzzleTile tile = tiles[i];
            if (tile.TileView != null)
            {
                // 修复7：终止关联动画
                //DOTween.Kill(tile.TileView.GetComponent<CanvasGroup>());
                tile.TileView.HideElement();
                 letterTilePool.ReturnObjectToPool(tile.TileView.GetComponent<PoolObject>());
                tile.SetAsEmpty();
            }
        }

        // 修复8：安全裁剪列表
        if (tiles.Count > targetCount)
        {
            tiles.RemoveRange(targetCount, tiles.Count - targetCount);
        }

        //修复9：层级安全更新
        for (int i = 0; i < targetCount; i++)
        {
            PuzzleTile tile = tiles[i];
            tile.Letter = layers[i];
            tile.Layer = i;
        
            if (tile.TileView != null)
            {
                // 修复10：使用局部变量避免闭包陷阱
                var tileView = tile.TileView;
                tileView.DownCharSetCharacter(tile.Letter);
            }
        }
    }


    private void OnDisable()
    {
        EventDispatcher.instance.OnPlayChoicePuzzle -= OnPlayChoicePuzzle;
        base.OnDisable();
        PuzzleTitle.alpha = 0;
        Clear();			
        letterTilePool.ReturnAllObjectsToPool();
    }
}

