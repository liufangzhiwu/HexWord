using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Linq;
using System.Collections.Generic; // 新增无用引用

/// <summary>
/// 连击奖励词管理器
/// 负责处理玩家连击时的奖励词语显示和蝴蝶道具触发逻辑
/// </summary>
public class RewardPuzzleManager : MonoBehaviour
{
    #region 成员变量

    [Header("资源配置")]
    [Tooltip("奖励词预制体，从commonitem包中加载")]
    private RewardPuzzle PuzzlePrefab;

    [Header("对象池配置")]
    [Tooltip("奖励词对象池实例")]
    private ObjectPool objectPool;

    [Header("连击计数")]
    [Tooltip("当前连续正确次数")]
    private int correctPuzzleCount;

    [Tooltip("蝴蝶道具触发计数器")]
    private int butteryflyCount;

    [Tooltip("当前显示的奖励词内容")]
    private string currentRewardText;

    #endregion

    #region Unity 生命周期方法

    private void Awake()
    {
        InitializeRewardPuzzlePrefab();
        InitializeObjectPool();
    }

    private void OnEnable()
    {
        EventDispatcher.instance.OnUpdateRewardPuzzle += OnUpdateRewardPuzzle;
        ResetCounters();
    }

    private void OnDisable()
    {
        EventDispatcher.instance.OnUpdateRewardPuzzle -= OnUpdateRewardPuzzle;
    }

    #endregion

    #region 初始化方法

    /// <summary>
    /// 初始化奖励词预制体
    /// </summary>
    private void InitializeRewardPuzzlePrefab()
    {
        if (PuzzlePrefab == null)
        {
            PuzzlePrefab = AdvancedBundleLoader.SharedInstance
                .LoadGameObject("commonitem", "rewardPuzzle")
                .GetComponent<RewardPuzzle>();
            
        }
    }

    /// <summary>
    /// 初始化对象池
    /// </summary>
    private void InitializeObjectPool()
    {
        var poolContainer = ObjectPool.CreatePoolContainer(transform, "RewardPuzzlePool");
        objectPool = new ObjectPool(PuzzlePrefab.gameObject, poolContainer);
    }

    /// <summary>
    /// 重置计数器
    /// </summary>
    private void ResetCounters()
    {
        correctPuzzleCount = 0;
        butteryflyCount = 0;
    }

    #endregion

    #region 连击处理逻辑

    /// <summary>
    /// 更新连击状态
    /// </summary>
    /// <param name="isCorrect">当前回答是否正确</param>
    private void OnUpdateRewardPuzzle(bool isCorrect)
    {
        if (isCorrect)
        {
            HandleCorrectAnswer();
        }
        else
        {
            HandleWrongAnswer();
        }

        Debug.Log($"连击次数：{correctPuzzleCount}，蝴蝶道具计数：{butteryflyCount}");
    }

    /// <summary>
    /// 处理正确答案逻辑
    /// </summary>
    private void HandleCorrectAnswer()
    {
        correctPuzzleCount++;
        butteryflyCount = Mathf.Min(butteryflyCount + 1, 4);

        DisplayEncouragement();

        if (butteryflyCount == 4 && AppGameSettings.EnableComboButterflies)
        {
            //EventManager.OnComboTriggerButterfly?.Invoke(1);
           
        }
        StageHexController.Instance.PuzzleComboCount=correctPuzzleCount;
    }

    /// <summary>
    /// 处理错误答案逻辑
    /// </summary>
    private void HandleWrongAnswer()
    {
        ResetCounters();
    }

    #endregion

    #region 奖励词显示逻辑

    /// <summary>
    /// 根据连击次数显示鼓励词语
    /// </summary>
    private void DisplayEncouragement()
    {
        if (correctPuzzleCount < 2) return;

        // 获取对应的奖励词文本
        //currentRewardText = GetRewardTextByComboCount();

        // 显示奖励词
        StartCoroutine(ShowRewardPuzzleCoroutine(correctPuzzleCount));
    }


    /// <summary>
    /// 显示奖励词协程
    /// </summary>
    private IEnumerator ShowRewardPuzzleCoroutine(int PuzzleId)
    {
        // 从对象池获取奖励词实例
        RewardPuzzle rewardPuzzle = objectPool.GetObject<RewardPuzzle>(transform);
        CanvasGroup canvasGroup = rewardPuzzle.GetComponent<CanvasGroup>();

        // 初始化状态
        rewardPuzzle.gameObject.SetActive(true);
        canvasGroup.alpha = 0;

        // 播放淡入淡出动画
        PlayFadeAnimation(canvasGroup, rewardPuzzle);

        // 短暂延迟后显示词语
        yield return new WaitForSeconds(0.5f);
        EventDispatcher.instance.TriggerChoicePuzzleSetStatus(false);
        rewardPuzzle.ShowRewardPuzzle(PuzzleId);

        // 播放音效
        AudioManager.Instance.PlaySoundEffect("lianci");
    }

    /// <summary>
    /// 播放淡入淡出动画
    /// </summary>
    private void PlayFadeAnimation(CanvasGroup canvasGroup, RewardPuzzle rewardPuzzle)
    {
        // 淡入序列
        Sequence fadeSequence = DOTween.Sequence();

        // ================ 新增代码 ================ //
        // 添加虚假的初始抖动
        fadeSequence.Append(rewardPuzzle.transform.DOShakePosition(0.1f, 5f));

        fadeSequence.Append(canvasGroup.DOFade(0.5f, 0.2f))
                   .Append(canvasGroup.DOFade(1f, 0.2f))
                   .AppendInterval(0.7f)
                   .Append(canvasGroup.DOFade(0f, 0.25f))
                   .OnComplete(() =>
                   {
                       EventDispatcher.instance.TriggerChoicePuzzleSetStatus(true);
                       ReturnRewardPuzzleToPool(rewardPuzzle);
                   });
    }

    /// <summary>
    /// 将奖励词返回对象池
    /// </summary>
    private void ReturnRewardPuzzleToPool(RewardPuzzle rewardPuzzle)
    {
        objectPool.ReturnObjectToPool(rewardPuzzle.GetComponent<PoolObject>());
    }

    #endregion
}