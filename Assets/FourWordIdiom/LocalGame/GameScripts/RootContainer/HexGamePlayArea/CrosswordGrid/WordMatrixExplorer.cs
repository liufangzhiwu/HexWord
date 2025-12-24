using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WordMatrixExplorer
{
    private BoardGame GameBoard;
    private HashSet<string> LevelLexicon;
    private int newDirections=-1;

    public IdiomBlock leftBlock;

    private StageInfo CurStageInfo
    {
        get
        {
            return StageHexController.Instance.CurStageInfo;
        }
    }
    
    private StageProgressData CurStageData
    {
        get
        {
            return StageHexController.Instance.CurStageData;
        }
    }

    public WordMatrixExplorer(BoardGame gameBoard, List<string> levelWords)
    {
        GameBoard = gameBoard;
        LevelLexicon = new HashSet<string>(levelWords);
    }

    public HashSet<string> ExploreWordMatrix()
    {

        LevelLexicon = new HashSet<string>(CurStageData.GetLeftPuzzles());
        
        GameBoard = CurStageData.BoardSnapshot;
        HashSet<string> discoveredWords = new HashSet<string>();
        bool[,] visited = new bool[GameBoard.rows, GameBoard.cols];

        for (int row = 0; row < GameBoard.rows; row++)
        {
            for (int col = 0; col < GameBoard.cols; col++)
            {
                if (GameBoard.board[row][col].Count > 0)
                {
                    if (GameBoard.board[row][col][0] != '\0' && !visited[row, col])
                    {
                        newDirections = -1;
                        ExploreFromPosition(row, col, "", discoveredWords, visited);
                    }
                }
            }
        }

        //无解时处理逻辑
        if (discoveredWords.Count <= 0)
        {
            foreach (string word in LevelLexicon)
            {
                IdiomData idiomData = CurStageInfo.idioms
                    .FirstOrDefault(idiom => idiom.word.Equals(word));
                
                string currentWord = "";
                leftBlock = null;
                
                // 检查是否是完整的成语
                if (idiomData != null)
                {
                    foreach (var idiomBlock in idiomData.blocks)
                    {
                        char cellChar = GameBoard.board[idiomBlock.position.x][idiomBlock.position.y][0];
                        if (cellChar.ToString() == idiomBlock.character)
                        {
                            currentWord += cellChar;
                        }
                        else
                        {
                            leftBlock = idiomBlock;
                        }
                    }

                    if (currentWord.Length > 2&&leftBlock!=null)
                    {
                        char targetChar = leftBlock.character.ToCharArray()[0];
                        
                        StageHexController.Instance.SetStageData(StageHexController.Instance.CurrentStage);
                        
                        List<char> cellChars = CurStageInfo.CurBoardData.board[leftBlock.position.x][leftBlock.position.y];
                        
                        int findCharCount = cellChars.FindAll(x => x == targetChar).Count;
                        
                        if(findCharCount<=1) continue;
                        
                        char oldcellChar = GameBoard.board[leftBlock.position.x][leftBlock.position.y][0];
                        
                        GameBoard.board[leftBlock.position.x][leftBlock.position.y][0] = targetChar;
                        GameBoard.board[leftBlock.position.x][leftBlock.position.y][1] = oldcellChar;

                        discoveredWords.Add(word);
                        return discoveredWords;
                    }
                }
            }
        }

        return discoveredWords;
    }

    /// <summary>
    /// 从指定位置开始探索单词矩阵
    /// </summary>
    /// <param name="row"></param>
    /// <param name="col"></param>
    /// <param name="currentWord"></param>
    /// <param name="foundWords"></param>
    /// <param name="visited"></param>
    private void ExploreFromPosition(int row, int col, string currentWord,
                                    HashSet<string> foundWords, bool[,] visited)
    {
        // 边界检查和单元格可用性检查
        if (row < 0 || row >= GameBoard.rows || col < 0 || col >= GameBoard.cols ||
            visited[row, col] || GameBoard.board[row][col].Count == 0 ||
            GameBoard.board[row][col][0] == '\0')
            return;
        
        List<char> chars = new List<char>();
        // 获取单元格中的字符
        if (GameBoard.board[row][col].Count > 1)
        {
            chars = GameBoard.board[row][col]; 
        }
          
        char cellChar = GameBoard.board[row][col][0];
        string newWord = currentWord + cellChar;

        // 检查新词是否可能是任何单词的前缀
        bool isPrefix = false;
        foreach (string word in LevelLexicon)
        {
            if (word.StartsWith(newWord))
            {
                // if (chars.Count > 1)
                // {
                //     List<char> tchars = chars.GetRange(1,chars.Count-1);
                //     if (tchars.Contains(cellChar))
                //     {
                //         IdiomData idiomData = StageHexController.Instance.CurStageInfo.idioms
                //             .FirstOrDefault(idiom => idiom.word.Equals(word));
                //
                //         
                //         chars=StageHexController.Instance.CurStageInfo.CurBoardData.board[row][col];
                //         
                //         // 检查是否是完整的成语
                //         if (idiomData != null)
                //         {
                //             IdiomBlock idiomBlock = idiomData.blocks.Find(block => block.character == cellChar.ToString());
                //             if(idiomBlock!=null&&idiomBlock.index==chars.Count)
                //             {
                //                 isPrefix = true;
                //                 break;
                //             }
                //         }
                //     }
                //     else
                //     {
                //         isPrefix = true;
                //         break;
                //     }
                // }
                // else
                // {
                    isPrefix = true;
                    break;
                //}
            }
        }

        if (!isPrefix)
        {
            newDirections = -1;
            return;
        }

        // 标记当前单元格已访问
        visited[row, col] = true;

        // 如果是完整单词则添加到结果集
        if (LevelLexicon.Contains(newWord))
        {
            foundWords.Add(newWord);
        }

        // 在搜索函数中使用
        int parity = col % 2;
        var directions=new (int, int)[6];
            
        if ((HexType)CurStageInfo.HexType == HexType.PingHexagon)
        {
            directions = (parity == 0) ?  CurStageInfo.HexDirectionsEven : CurStageInfo.HexDirectionsOdd;
        }
        else
        {
            parity = row % 2;
            directions = (parity == 0) ? CurStageInfo.HexJianDirectionsEven : CurStageInfo.HexJianDirectionsOdd;
        }
        
        if(newDirections!=-1)
        {
            var (dr, dc) = directions[newDirections];
            
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
        else
        {
            // 在六边形网格的六个方向上进行搜索
            int index = 0;
            foreach (var (dr, dc) in directions)
            {
                int newRow = row + dr;
                int newCol = col + dc;

                // 检查新位置是否有效
                if (newRow >= 0 && newRow < GameBoard.rows &&
                    newCol >= 0 && newCol < GameBoard.cols &&
                    !visited[newRow, newCol])
                {
                    newDirections = index;
                    ExploreFromPosition(newRow, newCol, newWord, foundWords, visited);
                }
                index++;
            }
        }
        // 回溯，标记当前单元格未访问
        visited[row, col] = false;
    }
}