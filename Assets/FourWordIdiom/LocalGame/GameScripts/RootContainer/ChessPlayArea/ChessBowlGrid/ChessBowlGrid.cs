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
    // 🌟 专门用来存放飞行幻影的对象池
    public ObjectPool PhantomPool { get; private set; }
    private ChessStageProgressData CurrStageData
    {
        get => ChessStageController.Instance.CurrStageData;
    }

    public ChessPlayArea GamePlayArea { get; private set; }

    [SerializeField] public List<BowlView> GridList = new(); // 存放词语的字块堆
    
    public static bool IsTutorialBlocking = false; // 🌟 引导拦截闸门

    public void Initialize(ChessPlayArea play)
    {
        if (PuzzleItemObj == null)
        {
            PuzzleItemObj = AssetBundleLoader.SharedInstance.LoadGameObject("commonitem", "ChessBowlView");
        }

        LetterTilePool = new ObjectPool(PuzzleItemObj.gameObject, transform, 3, PoolBehaviour.CanvasGroup);
        PhantomPool = new ObjectPool(PuzzleItemObj.gameObject, ObjectPool.CreatePoolContainer(transform.parent.parent, "PhantomPool"), 5, PoolBehaviour.CanvasGroup);
        GamePlayArea = play;
    }

    public void CreatePuzzle()
    {
        StartCoroutine(SetupGrid());
    }

    private IEnumerator SetupGrid()
    {
        List<Bowl> puzzles = CurrStageData.Puzzles.ToList();
        // System.Globalization.CultureInfo zhCulture = new System.Globalization.CultureInfo("zh-CN");
        if (ChessStageController.Instance.CurrStageData.StageId != 1)
        {
            // 第一关：不打乱，保持原样（或者按成语原本的顺序排序）
            // 如果原本 Puzzles 里的数据就是按顺序的，这里什么都不用做
            puzzles.Sort((a, b) => string.Compare(a.pinyin, b.pinyin, StringComparison.OrdinalIgnoreCase));
        }
        yield return new WaitForEndOfFrame();

        foreach (Bowl puzzle in puzzles)
        {
            if (puzzle.status == 2)
                continue;

            BowlView view = LetterTilePool.GetObject<BowlView>();
            view.Setup(puzzle, this);

            view.OnClickHandler += OnPuzzleSelected;
            GridList.Add(view);
        }
    }

    /// <summary>
    /// 通知字堆结果 , 返回新的字
    /// </summary>
    public Bowl OnNotifyResult(Bowl bowl, int status)
    {
        // 检查是销毁还是锁定
        BowlView hit = GridList.FirstOrDefault(bv => bv.letter == bowl.letter);
        Bowl archiveBowl = CurrStageData.Puzzles.FirstOrDefault(b => b.letter == bowl.letter);
        if (archiveBowl == null)
        {
            archiveBowl = new Bowl { id = "b_" + Guid.NewGuid().ToString("N")[..8], letter = bowl.letter, status = 0, count = 1 };
            CurrStageData.Puzzles.Add(archiveBowl);
        }
        
        // ==========================================
        // 操作 A：恢复（解锁）到字盘
        // ==========================================
        if (status == 0)
        {
            if (hit != null)
            {
                hit.bowl.count++;
                hit.UpdateBadge();
                hit.bowl.status = 0;
                hit.Unlock();
            }
            else
            {
                archiveBowl.count = 1;
                archiveBowl.status = 0;
                BowlView view = LetterTilePool.GetObject<BowlView>();
                view.Setup(archiveBowl, this);
                view.OnClickHandler -= OnPuzzleSelected;
                view.OnClickHandler += OnPuzzleSelected;
                GridList.Add(view);
                hit = view; // 关联上
            }
        }
        // ==========================================
        // 操作 B：销毁（成语通关）
        // ==========================================
        else if (status == 2) 
        {
            if (hit != null && hit.bowl.count <= 0) // 注意这里原代码的逻辑：库存空了才销毁
            {
                hit.bowl.status = 2;
                hit.bowl.count = 0;
                if (hit.GetComponent<Canvas>() != null)
                {
                    Destroy(hit.GetComponent<GraphicRaycaster>());
                    Destroy(hit.GetComponent<Canvas>());
                }
                GridList.Remove(hit);
                LetterTilePool.ReturnObjectToPool(hit.GetComponent<PoolObject>());
            }
            else if (hit != null)
            {
                // 如果字还有库存，说明虽然销毁了一个，但字盘上还该有，不管它
                hit.UpdateBadge();
            }
        }
        // ==========================================
        // 操作 C：锁定（玩家点击飞入棋盘）
        // ==========================================
        else if (status == 1) 
        {
            if (hit != null)
            {
                hit.bowl.count--;
                hit.UpdateBadge();
                
                if (hit.bowl.count > 0)
                {
                    hit.bowl.status = 0; // 还有库存，继续解锁
                }
                else
                {
                    hit.bowl.status = 1; // 用光了，老老实实锁定
                    hit.Lock();
                }
            }
        }
        // 🌟 核心修复：更新底层存档状态！用同一个内存引用！
        if (hit != null)
        {
            archiveBowl.status = hit.bowl.status;
            archiveBowl.count = hit.bowl.count;
            ChessStageController.Instance.ModifyBowl(archiveBowl);
            return archiveBowl;
        }
        return bowl;
    }

    /// <summary>
    /// 设置的委托 点击字体进入棋盘
    /// </summary>
    /// <param name="puzzle"></param>
    public void OnPuzzleSelected(BowlView puzzle)
    {
        if (IsTutorialBlocking) return;
        StartCoroutine(GamePlayArea.chessboardGrid.HandleBlowViewState(puzzle));
    }

    public void Clear()
    {
        GridList.Clear();
        LetterTilePool.ReturnAllObjectsToPool();
        PhantomPool?.ReturnAllObjectsToPool();
        IsTutorialBlocking = false;
    }
    private void OnDisable()
    {
        // 🌟 修复点 2：隐藏界面时必定开闸！
        IsTutorialBlocking = false;
    }
}