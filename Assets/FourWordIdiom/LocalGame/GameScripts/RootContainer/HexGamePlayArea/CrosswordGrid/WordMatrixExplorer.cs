using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WordMatrixExplorer
{
    private BoardGame GameBoard;
    private readonly HashSet<string> LevelLexicon;

    // 平顶六边形网格的六个方向定义（行为偶数时）
    private static readonly (int, int)[] HexDirectionsEven = {
        (1, 0),    // 上
        (0, 1),   // 右上
        (0, -1),   // 左上
        (-1, 0),   // 下
        (-1, -1),  // 左下
        (-1, 1)     // 右下
    };

    private static readonly (int, int)[] HexDirectionsOdd = {
        (1, 0),    // 上
        (1, 1),   // 右上
        (1, -1),  // 左上
        (-1, 0),   // 下
        (0, -1),   // 左下
        (0, 1)     // 右下
    };

    public WordMatrixExplorer(BoardGame gameBoard, List<string> levelWords)
    {
        GameBoard = gameBoard;
        LevelLexicon = new HashSet<string>(levelWords);
    }

    public HashSet<string> ExploreWordMatrix()
    {
        GameBoard = StageHexController.Instance.CurStageData.BoardSnapshot;
        HashSet<string> discoveredWords = new HashSet<string>();
        bool[,] visited = new bool[GameBoard.rows, GameBoard.cols];

        for (int row = 0; row < GameBoard.rows; row++)
        {
            for (int col = 0; col < GameBoard.cols; col++)
            {
                if (GameBoard.board[row][col].Count > 0)
                {
                    //int mxa = GameBoard.board[row][col].Count - 1;
                    if (GameBoard.board[row][col][0] != '\0' && !visited[row, col])
                    {
                        ExploreFromPosition(row, col, "", discoveredWords, visited);
                    }
                }
            }
        }

        return discoveredWords;
    }

    private void ExploreFromPosition(int row, int col, string currentWord,
                                    HashSet<string> foundWords, bool[,] visited)
    {
        // 边界检查和单元格可用性检查
        if (row < 0 || row >= GameBoard.rows || col < 0 || col >= GameBoard.cols ||
            visited[row, col] || GameBoard.board[row][col].Count == 0 ||
            GameBoard.board[row][col][0] == '\0')
            return;

        // 获取单元格中的字符
        //int mxa = GameBoard.board[row][col].Count - 1;
        char cellChar = GameBoard.board[row][col][0];
        string newWord = currentWord + cellChar;

        // 检查新词是否可能是任何单词的前缀
        bool isPrefix = false;
        foreach (string word in LevelLexicon)
        {
            if (word.StartsWith(newWord))
            {
                isPrefix = true;
                break;
            }
        }

        if (!isPrefix)
            return;

        // 标记当前单元格已访问
        visited[row, col] = true;

        // 如果是完整单词则添加到结果集
        if (LevelLexicon.Contains(newWord))
        {
            foundWords.Add(newWord);
        }

        // 在搜索函数中使用
        int parity = col % 2;
        var directions = (parity == 0) ? HexDirectionsEven : HexDirectionsOdd;
        
        // 在六边形网格的六个方向上进行搜索
        foreach (var (dr, dc) in directions)
        {
            int newRow = row + dr;
            int newCol = col + dc;

            // 检查新位置是否有效
            if (newRow >= 0 && newRow < GameBoard.rows &&
                newCol >= 0 && newCol < GameBoard.cols &&
                !visited[newRow, newCol])
            {
                ExploreFromPosition(newRow, newCol, newWord, foundWords, visited);
            }
        }

        // 回溯，标记当前单元格未访问
        visited[row, col] = false;
    }
}