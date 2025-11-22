using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ChessBowlGrid : MonoBehaviour
{
    [SerializeField] private GameObject PuzzleItemObj; // 预制体
    private ObjectPool LetterTilePool;

    private ChessStageProgressData CurrStageData { get => ChessStageController.Instance.CurrStageData; }
    public ChessPlayArea GamePlayArea { get; private set; }

    [SerializeField] public  List<BowlView> GridList = new();    // 存放词语的字块堆

    public BowlView CurrPuzzle { get; private set; }    // 当前选择的词

    public static bool _isProcessing;                  // 全局处理状态锁


    public void Initialize(ChessPlayArea play)
    {
        if (PuzzleItemObj == null)
        {
            PuzzleItemObj = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "ChessBowlView");
        }

        LetterTilePool = new ObjectPool(PuzzleItemObj.gameObject, transform, 3, PoolBehaviour.CanvasGroup);
        GamePlayArea = play;
    }

    public void CreatePuzzle()
    {
        StartCoroutine(SetupGrid());
    }

    private IEnumerator SetupGrid()
    {
        HashSet<Bowl> puzzles = CurrStageData.Puzzles;
        yield return new WaitForEndOfFrame();

        foreach (Bowl puzzle in puzzles)
        {
            if(puzzle.status == 2)
                continue;

            BowlView view = LetterTilePool.GetObject<BowlView>();
            view.Setup(puzzle, this);
          
            view.OnClickHandler += OnPuzzleSelected;
            GridList.Add(view);
        }
    }
    // 清理
    public Bowl CleanBowlView(Chesspiece chesspieces)
    {
        // 找出要删的 BowlView
        BowlView hitBowl = GridList
            .FirstOrDefault(bowl => bowl.letter == chesspieces.letter);
        if (hitBowl == null) return default;
        hitBowl.bowl.status = 2;
        Bowl retBowl = hitBowl.bowl;

        ChessStageController.Instance.ModifyBowl(retBowl);
        GridList.Remove(hitBowl);
        LetterTilePool.ReturnObjectToPool(hitBowl.GetComponent<PoolObject>());
        return retBowl;
    }

    /// <summary>
    /// 通知字堆结果 , 返回新的字
    /// </summary>
    public Bowl OnNotifyResult(Bowl bowl, int status)
    {
        // 检查是销毁还是锁定
        // Debug.Log($"移除字块前 {bowl.id}");
        BowlView hit = GridList.FirstOrDefault(bv => bv.bowl.id == bowl.id);
        if (hit == null) {
            // Debug.LogWarning($"没有找到对应的字块 {bowl.id} ");
            // foreach (var item in GridList)
            // {
            //     Debug.Log($"当前字块有 {item.letter} "+ JsonConvert.SerializeObject(item.bowl));
            // }
            return bowl;
        }
        // Debug.Log($"移除字块后 {hit.bowl.id}  {status}");
        hit.bowl.status = status;
        ChessStageController.Instance.ModifyBowl(hit.bowl);
        if (status == 2) // 销毁
        {
            // Debug.Log($"移除字块 {hit.letter}");
            if(hit.GetComponent<Canvas>() != null)
            {
                Destroy(hit.GetComponent<GraphicRaycaster>());
                Destroy(hit.GetComponent<Canvas>());
            }
            GridList.Remove(hit);
            LetterTilePool.ReturnObjectToPool(hit.GetComponent<PoolObject>());
        }
        else if(status == 1)
        {
            hit.Lock();
        }else if(status == 0)
        {
            hit.Unlock();
        }
        return hit.bowl;
    }

    /// <summary>
    /// 设置的委托 点击字体进入棋盘
    /// </summary>
    /// <param name="puzzle"></param>
    public void OnPuzzleSelected(BowlView puzzle)
    {
        CurrPuzzle = puzzle;
        StartCoroutine(GamePlayArea.chessboardGrid.HandleBolwViewState(puzzle));
    }

    public  void Clear()
    {
        GridList.Clear();
        LetterTilePool.ReturnAllObjectsToPool();
    }

  
}
