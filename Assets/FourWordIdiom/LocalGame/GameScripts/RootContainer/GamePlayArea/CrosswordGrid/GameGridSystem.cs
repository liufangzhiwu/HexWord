using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// 表示游戏网格系统，管理单元状态和操作记录
/// </summary>
public class GameGridSystem
{

    public int GridWidth { get; set; }
    public int GridHeight { get; set; }
    public List<List<GridUnit>> CellMatrix => _cellMatrix;
    public List<bool> VerticalPlacementFlags => _verticalFlags;

    private List<List<GridUnit>> _cellMatrix;
    private List<bool> _verticalFlags;
    public List<Dictionary<string, OperationHistory>> _operationLogs = new List<Dictionary<string, OperationHistory>>();


    public GameGridSystem(int gridWidth, int gridHeight)
    {
        // 验证输入参数
        if (gridWidth <= 0 || gridHeight <= 0)
        {
            throw new System.ArgumentException($"Invalid grid dimensions: width={gridWidth}, height={gridHeight}");
        }

        GridWidth = gridWidth;
        GridHeight = gridHeight;

        InitializeCellStructure();
        SetupVerticalConstraints();
    }


    private void InitializeCellStructure()
    {
        _cellMatrix = new List<List<GridUnit>>(GridWidth);

        for (int xPos = 0; xPos < GridWidth; xPos++)
        {
            var columnUnits = new List<GridUnit>(GridHeight);
            for (int yPos = 0; yPos < GridHeight; yPos++)
            {
                columnUnits.Add(new GridUnit(xPos, yPos));
            }
            _cellMatrix.Add(columnUnits);
        }
    }

    private void SetupVerticalConstraints()
    {
        _verticalFlags = Enumerable.Repeat(false, GridWidth).ToList();
    }

    /// <summary>
    /// 访问指定坐标的网格单元
    /// </summary>
    public GridUnit AccessGridUnit(int xCoord, int yCoord)
    {
        if (xCoord < 0 || xCoord >= GridWidth || yCoord < 0 || yCoord >= GridHeight)
        {
            throw new System.ArgumentOutOfRangeException($"Unit coordinate out of bounds: ({xCoord}, {yCoord})");
        }

        return _cellMatrix[xCoord][yCoord];
    }

    /// <summary>
    /// 验证垂直放置操作可行性
    /// </summary>
    public bool ValidateVerticalPlacement(int columnIndex)
    {
        return columnIndex >= 0 && columnIndex < GridWidth && !_verticalFlags[columnIndex];
    }
}

/// <summary>
/// 表示棋盘上的一个单元格
/// </summary>
public class GridUnit
{
    public int X { get; }
    public int Y { get; }
    public char Letter { get; set; }

    public GridUnit(int x, int y, char letter = default)
    {
        X = x;
        Y = y;
        Letter = letter;
    }
}

/// <summary>
/// 表示字母放置操作的类型和位置
/// </summary>
public class Placement
{
    public enum PlacementType
    {
        Horizontal, // 水平
        Vertical,   // 垂直
        ShiftLeft,  // 左移
        ShiftRight  // 右移
    }

    public GridUnit Cell { get; }
    public PlacementType Type { get; }

    public Placement(GridUnit cell, PlacementType type)
    {
        Cell = cell ?? throw new System.ArgumentNullException(nameof(cell));
        Type = type;
    }
}

/// <summary>
/// 表示可撤销操作的基类
/// </summary>
public abstract class OperationHistory
{
    public enum ActionType
    {
        Cell,
        PlacedVertical
    }

    public abstract ActionType Type { get; }      
}

/// <summary>
/// 表示单元格内容的撤销操作
/// </summary>
public class CellUndo : OperationHistory
{
    public GridUnit Cell { get; }
    public char PreviousLetter { get; }

    public override ActionType Type => ActionType.Cell;

    public CellUndo(GridUnit cell, char previousLetter)
    {
        Cell = cell ?? throw new System.ArgumentNullException(nameof(cell));
        PreviousLetter = previousLetter;
    }
}

/// <summary>
/// 表示垂直放置标记的撤销操作
/// </summary>
public class PlacedVerticalUndo : OperationHistory
{
    public int ColumnIndex { get; }
    public bool PreviousValue { get; }

    public override ActionType Type => ActionType.PlacedVertical;

    public PlacedVerticalUndo(int columnIndex, bool previousValue)
    {
        ColumnIndex = columnIndex;
        PreviousValue = previousValue;
    }
}