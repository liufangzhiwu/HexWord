using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using Random = System.Random;

/// <summary>
/// 棋盘数据创建工厂
/// </summary>
public class PuzzleMatrixGenerator
{
    private static PuzzleMatrixGenerator _shared;
    private readonly Random rng = new Random();

    private int MaxAttempts = 20;

    private StageInfo CurrentStageConfig => StageController.Instance.CurStageInfo;
    private StageProgressData CurrentStageState => StageController.Instance.CurStageData;

    // 单例访问点
    public static PuzzleMatrixGenerator Shared => _shared ??= new PuzzleMatrixGenerator();

    public Queue<char> RemainingLetters = new Queue<char>();


    public async Task RebuildPuzzleMatrixAsync(Action completionCallback = null)
    {
        try
        {
            var unsolvedPuzzles = GetUnsolvedPuzzles();
            var tempMatrix = await GenerateValidMatrixAsync(unsolvedPuzzles);

            if (tempMatrix != null)
            {
                FillEmptyCells(tempMatrix);
                StageController.Instance.BoardData = GenerateBoardSnapshot(tempMatrix);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Rebuild failed: {ex.Message}");
        }
        finally
        {
            completionCallback?.Invoke();
        }
    }

    private List<string> GetUnsolvedPuzzles()
    {
        return CurrentStageConfig.Puzzles
            .Where(p => !CurrentStageState.FoundTargetPuzzles.Contains(p))
            .ToList();
    }

    private async Task<GameGridSystem> GenerateValidMatrixAsync(List<string> puzzles)
    {
        return await Task.Run(() =>
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                int rows = CurrentStageState.BoardSnapshot.rows + attempt;
                int cols = CurrentStageState.BoardSnapshot.cols + attempt;

                var matrix = new GameGridSystem(cols, rows);
                if (ArrangePuzzles(matrix, puzzles,0))
                {
                    return matrix;
                }
            }
            return null;
        });
    }

  

    private void FillEmptyCells(GameGridSystem matrix)
    {
        if (RemainingLetters.Count == 0) return;

        for (int col = matrix.GridWidth - 1; col >= 0; col--)
        {
            for (int row = 0; row < matrix.GridHeight; row++)
            {
                var cell = matrix.CellMatrix[col][row];
                if (cell.Letter == '\0' && RemainingLetters.Count > 0)
                {
                    cell.Letter = RemainingLetters.Dequeue();
                }
            }
        }
    }


    /// <summary>
    /// 随机打乱列表
    /// </summary>
    private void RandomizeListOrder<T>(List<T> items)
    {
        int count = items.Count;
        for (int idx = count - 1; idx > 0; idx--)
        {
            int randomIdx = rng.Next(0, idx + 1);
            (items[idx], items[randomIdx]) = (items[randomIdx], items[idx]);
        }
    }

    public bool ArrangePuzzles(GameGridSystem grid, List<string> puzzleList, int currentIndex)
    {
        if (currentIndex >= puzzleList.Count) return true;

        string currentPuzzle = puzzleList[currentIndex];
        int horizontalOptions;
        List<Placement> validPositions = FindPlacementOptions(
            grid, currentPuzzle.Length, currentIndex == 0, out horizontalOptions);

        if (validPositions.Count == 0)
        {
            foreach (char letter in currentPuzzle)
            {
                RemainingLetters.Enqueue(letter);
            }

            if (ArrangePuzzles(grid, puzzleList, currentIndex + 1)) return true;
        }

        for (int posIdx = 0; posIdx < validPositions.Count; posIdx++)
        {
            int selectedIdx = ChooseRandomPositionIndex(currentIndex, horizontalOptions, validPositions.Count);
            Placement placement = validPositions[selectedIdx];

            SwapOptions(validPositions, posIdx, selectedIdx);
            grid._operationLogs.Add(new Dictionary<string, OperationHistory>());

            PositionPuzzle(grid, currentPuzzle, placement);

            if (ArrangePuzzles(grid, puzzleList, currentIndex + 1)) return true;

            ReverseOperations(grid);
        }
        return false;
    }

    /// <summary>
    /// 选择随机的放置索引
    /// </summary>
    private int ChooseRandomPositionIndex(int puzzleIndex, int horizontalCount, int totalOptions)
    {
        if (horizontalCount > 0)
        {
            return puzzleIndex % 2 == 0 ?
                rng.Next(totalOptions - horizontalCount, totalOptions) :
                rng.Next(horizontalCount);
        }
        return rng.Next(totalOptions);
    }

    /// <summary>
    /// 交换放置位置
    /// </summary>
    private void SwapOptions(List<Placement> options, int firstIdx, int secondIdx)
    {
        var temp = options[firstIdx];
        options[firstIdx] = options[secondIdx];
        options[secondIdx] = temp;
    }

    /// <summary>
	/// 放置单词到棋盘
	/// </summary>
	private void PositionPuzzle(GameGridSystem grid, string puzzle, Placement placement)
    {
        switch (placement.Type)
        {
            case Placement.PlacementType.Vertical:
                MoveTilesUp(grid, placement.Cell.X, placement.Cell.Y, puzzle.Length);
                break;
            case Placement.PlacementType.ShiftRight:
                MoveColumnsRight(grid, placement.Cell.X);
                break;
            case Placement.PlacementType.ShiftLeft:
                MoveColumnsLeft(grid, placement.Cell.X);
                break;
        }

        bool reverseWord = rng.Next(0, 2) == 0;

        for (int charIdx = 0; charIdx < puzzle.Length; charIdx++)
        {
            int posX = placement.Cell.X +
                (placement.Type == Placement.PlacementType.Horizontal ? charIdx : 0);
            int posY = placement.Cell.Y +
                (placement.Type == Placement.PlacementType.Horizontal ? 0 : charIdx);

            GridUnit cell = GetGridCell(grid, posX, posY);

            if (placement.Type == Placement.PlacementType.Horizontal)
            {
                MoveTilesUp(grid, posX, posY, 1);
            }

            int letterPosition = reverseWord ? puzzle.Length - charIdx - 1 : charIdx;

            ModifyCellCharacter(grid, cell, puzzle[letterPosition]);
        }

        if (placement.Type != Placement.PlacementType.Horizontal)
        {
            UpdateVerticalFlag(grid, placement.Cell.X, true);
        }
    }

    /// <summary>
    /// 移动字母向上
    /// </summary>
    private void MoveTilesUp(GameGridSystem grid, int x, int y, int spaces)
    {
        if (GetGridCell(grid, x, y).Letter == '\0')
        {
            return;
        }

        MoveTilesUp(grid, x, y + 1, spaces);

        ModifyCellCharacter(grid, GetGridCell(grid, x, y + spaces), GetGridCell(grid, x, y).Letter);
    }

    private void MoveColumnsLeft(GameGridSystem grid, int columnX)
    {
        for (int xPos = 1; xPos <= columnX; xPos++)
        {
            for (int yPos = 0; yPos < grid.GridHeight; yPos++)
            {
                GridUnit sourceCell = GetGridCell(grid, xPos, yPos);
                GridUnit targetCell = GetGridCell(grid, xPos - 1, yPos);

                ModifyCellCharacter(grid, targetCell, sourceCell.Letter);

                if (xPos == columnX)
                {
                    ModifyCellCharacter(grid, sourceCell, '\0');
                }
            }

            UpdateVerticalFlag(grid, xPos - 1, grid.VerticalPlacementFlags[xPos]);
        }
    }

    private void MoveColumnsRight(GameGridSystem grid, int columnX)
    {
        for (int xPos = grid.GridWidth - 2; xPos >= columnX; xPos--)
        {
            for (int yPos = 0; yPos < grid.GridHeight; yPos++)
            {
                GridUnit sourceCell = GetGridCell(grid, xPos, yPos);
                GridUnit targetCell = GetGridCell(grid, xPos + 1, yPos);

                ModifyCellCharacter(grid, targetCell, sourceCell.Letter);

                if (xPos == columnX)
                {
                    ModifyCellCharacter(grid, sourceCell, '\0');
                }
            }

            UpdateVerticalFlag(grid, xPos + 1, grid.VerticalPlacementFlags[xPos]);
        }
    }

    /// <summary>
    /// 更新单元格字母
    /// </summary>
    private void ModifyCellCharacter(GameGridSystem grid, GridUnit cell, char newChar)
    {
        RecordCellOperation(grid, cell);
        cell.Letter = newChar;
    }

    /// <summary>
    /// 更新垂直标记
    /// </summary>
    private void UpdateVerticalFlag(GameGridSystem grid, int position, bool value)
    {
        RecordVerticalFlagOperation(grid, position);
        grid.VerticalPlacementFlags[position] = value;
    }

    /// <summary>
    /// 记录垂直标记操作
    /// </summary>
    private static void RecordVerticalFlagOperation(GameGridSystem grid, int position)
    {
        string key = position.ToString();
        Dictionary<string, OperationHistory> currentOperations = grid._operationLogs[^1];

        if (!currentOperations.ContainsKey(key))
        {
            PlacedVerticalUndo operation = new PlacedVerticalUndo(position, grid.VerticalPlacementFlags[position]);
            currentOperations.Add(key, operation);
        }
    }

    /// <summary>
    /// 查找可能的起始位置
    /// </summary>
    private List<Placement> FindPlacementOptions(
        GameGridSystem grid, int length, bool isFirst, out int horizontalCount)
    {
        horizontalCount = 0;
        List<Placement> options = new List<Placement>();
        int[] emptyColumns = new int[grid.GridWidth];

        for (int vertical = grid.GridHeight - 1; vertical >= 0; vertical--)
        {
            int maxHorizontal = 0;
            int minHorizontal = isFirst ? 1 : 0;

            bool isBottom = (vertical == 0);
            int bottomEmpty = 0;

            if (isBottom)
            {
                for (int horizontal = 0; horizontal < grid.GridWidth; horizontal++)
                {
                    if (grid.CellMatrix[horizontal][vertical].Letter == '\0')
                    {
                        bottomEmpty++;
                    }
                }
                minHorizontal = -1;
            }

            for (int horizontal = grid.GridWidth - 1; horizontal >= 0; horizontal--)
            {
                GridUnit currentCell = grid.CellMatrix[horizontal][vertical];
                GridUnit lowerCell = (vertical > 0) ? grid.CellMatrix[horizontal][vertical - 1] : null;

                emptyColumns[horizontal] += (currentCell.Letter == '\0') ? 1 : 0;

                if (emptyColumns[horizontal] > 0 && (lowerCell == null || lowerCell.Letter != '\0'))
                {
                    maxHorizontal++;
                }
                else
                {
                    maxHorizontal = 0;
                }

                if (isFirst || currentCell.Letter != '\0')
                {
                    minHorizontal = 1;
                }
                else if (minHorizontal != -1)
                {
                    minHorizontal++;
                }

                if (minHorizontal != -1 && length <= maxHorizontal && length >= minHorizontal)
                {
                    options.Add(new Placement(currentCell, Placement.PlacementType.Horizontal));
                    horizontalCount++;
                }

                if (!grid.VerticalPlacementFlags[horizontal] && emptyColumns[horizontal] >= length &&
                    ((isBottom && isFirst) || (currentCell.Letter != '\0')))
                {
                    options.Insert(0, new Placement(currentCell, Placement.PlacementType.Vertical));
                }

                if (isBottom && length <= grid.GridHeight)
                {
                    if (currentCell.Letter == '\0')
                    {
                        if (horizontal > 0 && grid.CellMatrix[horizontal - 1][0].Letter != '\0')
                        {
                            options.Insert(0, new Placement(currentCell, Placement.PlacementType.ShiftRight));
                        }
                        if (horizontal < grid.GridWidth - 1 && grid.CellMatrix[horizontal + 1][0].Letter != '\0')
                        {
                            options.Insert(0, new Placement(currentCell, Placement.PlacementType.ShiftLeft));
                        }
                    }
                }
            }
        }

        return options;
    }

    /// <summary>
    /// 撤销操作
    /// </summary>
    private void ReverseOperations(GameGridSystem grid)
    {
        Dictionary<string, OperationHistory> currentOperations = grid._operationLogs[^1];

        foreach (var operationEntry in currentOperations)
        {
            OperationHistory operation = operationEntry.Value;

            switch (operation.Type)
            {
                case OperationHistory.ActionType.Cell:
                    CellUndo cellOp = operation as CellUndo;
                    GridUnit targetCell = cellOp.Cell;
                    targetCell.Letter = cellOp.PreviousLetter;
                    break;

                case OperationHistory.ActionType.PlacedVertical:
                    PlacedVerticalUndo flagOp = operation as PlacedVerticalUndo;
                    grid.VerticalPlacementFlags[flagOp.ColumnIndex] = flagOp.PreviousValue;
                    break;
            }
        }

        grid._operationLogs.RemoveAt(grid._operationLogs.Count - 1);
    }

    /// <summary>
    /// 记录单元格操作
    /// </summary>
    private void RecordCellOperation(GameGridSystem grid, GridUnit cell)
    {
        string identifier = cell.X + "_" + cell.Y;
        Dictionary<string, OperationHistory> currentOperations = grid._operationLogs[grid._operationLogs.Count - 1];

        if (!currentOperations.ContainsKey(identifier))
        {
            CellUndo operation = new CellUndo(cell, cell.Letter);
            currentOperations.Add(identifier, operation);
        }
    }

    /// <summary>
    /// 获取或创建单元格
    /// </summary>
    private GridUnit GetGridCell(GameGridSystem grid, int horizontal, int vertical)
    {
        while (vertical >= grid.GridHeight)
        {
            for (int idx = 0; idx < grid.GridWidth; idx++)
            {
                GridUnit newCell = new GridUnit(horizontal, grid.GridHeight);
                grid.CellMatrix[idx].Add(newCell);
            }
            grid.GridHeight++;
        }
        return grid.CellMatrix[horizontal][vertical];
    }



    /// <summary>
    /// 生成棋盘快照
    /// </summary>
    /// <summary>
    /// 生成棋盘快照
    /// </summary>
    private BoardGame GenerateBoardSnapshot(GameGridSystem grid)
    {
        // 创建新的棋盘快照
        var snapshot = new BoardGame()
        {
            rows = grid.GridHeight,
            cols = grid.GridWidth,
            board = new List<List<List<char>>>()
        };

        // 复制棋盘数据（行优先顺序）
        for (int row = 0; row < grid.GridHeight; row++)
        {
            var rowData = new List<List<char>>();
            for (int col = 0; col < grid.GridWidth; col++)
            {
                // 获取单元格的字符层（深拷贝）
                var cell = grid.CellMatrix[col][row];
                var charLayers = new List<char>(cell.Letter);
                rowData.Add(charLayers);
            }
            snapshot.board.Add(rowData);
        }

        // 优化棋盘：移除空白行列
        TrimEmptyBoardEdges(snapshot);

        return snapshot;
    }

    /// <summary>
    /// 移除棋盘四周的空白行和列
    /// </summary>
    private void TrimEmptyBoardEdges(BoardGame board)
    {
        // 从右侧开始移除空白列
        for (int col = board.cols - 1; col >= 0; col--)
        {
            if (IsColumnEmpty(board, col))
            {
                RemoveColumn(board, col);
            }
        }

        // 从顶部开始移除空白行
        for (int row = board.rows - 1; row >= 0; row--)
        {
            if (IsRowEmpty(board, row))
            {
                RemoveRow(board, row);
            }
        }
    }

    /// <summary>
    /// 检查指定列是否完全空白
    /// </summary>
    private bool IsColumnEmpty(BoardGame board, int columnIndex)
    {
        // 只检查最底行（优化性能）
        return board.board[0][columnIndex][0] == '\0';
    }

    /// <summary>
    /// 检查指定行是否完全空白
    /// </summary>
    private bool IsRowEmpty(BoardGame board, int rowIndex)
    {
        var row = board.board[rowIndex][0];
        foreach (var cell in row)
        {
            if (cell != '\0') return false;
        }
        return true;
    }

    /// <summary>
    /// 移除指定列
    /// </summary>
    private void RemoveColumn(BoardGame board, int columnIndex)
    {
        foreach (var row in board.board)
        {
            row.RemoveAt(columnIndex);
        }
        board.cols--;
    }

    /// <summary>
    /// 移除指定行
    /// </summary>
    private void RemoveRow(BoardGame board, int rowIndex)
    {
        board.board.RemoveAt(rowIndex);
        board.rows--;
    }
}