using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using FourWordIdiom.LocalGame.GameScripts.Controller;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

/**
 * 冰块破冰激励视频逻辑 (ChessPlayArea.IceBreak.cs)
 */
public partial class ChessPlayArea
{
    private const float ICE_COUNTDOWN_TOTAL = 18f;
    private const float NO_INITIAL_WORD_STUCK_TIME = 8f;
    private const float OPERATED_STUCK_TIME = 15f;
    
    private bool _isIceBreakShowing = false;            // 是否正在倒计时展示中
    private float _iceBreakCountdown = 0f;              // 18s 倒计时计时器

    private float _noInitialStuckTimer = 0f;            // 无初始字卡点计时器 (8s)
    private float _operatedStuckTimer = 0f;             // 交互后未答对计时器 (15s)
    private bool _hasPlayerOperatedThisLevel = false;   // 本关是否已有操作
    private bool _isIceBreakEventBound = false;         // 标记是否已经绑定过点击事件
    private CanvasGroup _iceBreakCanvasGroup;
    private CanvasGroup IceBreakCanvasGroup
    {
        get
        {
            if (_iceBreakCanvasGroup == null && IceBreakTable != null)
            {
                _iceBreakCanvasGroup = IceBreakTable.GetComponent<CanvasGroup>();
                if (_iceBreakCanvasGroup == null)
                {
                    _iceBreakCanvasGroup = IceBreakTable.gameObject.AddComponent<CanvasGroup>();
                }
            }
            return _iceBreakCanvasGroup;
        }
    }
    /// <summary>
    /// 初始化/重置破冰状态 (在 SetupGameData 开局时调用)
    /// </summary>
    private void ResetIceBreakState()
    {
        _isIceBreakShowing = false;
        _iceBreakCountdown = 0f;
        _noInitialStuckTimer = 0f;
        _operatedStuckTimer = 0f;
        _hasPlayerOperatedThisLevel = false;

        if (IceBreakTable != null)
        {
            IceBreakTable.gameObject.SetActive(false);
            IceBreakTable.transform.DOKill();
        }
        // 绑定点击事件
        if (!_isIceBreakEventBound && IceBreakTable != null)
        {
            IceBreakTable.AddClickAction(OnIceBreakClicked);
            _isIceBreakEventBound = true;
        }
    }

    /// <summary>
    /// Update 中驱动计时 (在 ChessPlayArea.Timer.cs 的 Update 尾部调用)
    /// </summary>
    private void UpdateIceBreakTimer(float dt)
    {
        // 1. 如果破冰入口正在展示，跑 18s 倒计时
        if (_isIceBreakShowing)
        {
            _iceBreakCountdown -= dt;
            if (iceTimerSlider != null)
            {
                iceTimerSlider.value = Mathf.Clamp01(_iceBreakCountdown / ICE_COUNTDOWN_TOTAL);
            }

            if (_iceBreakCountdown <= 0f)
            {
                HideIceBreakEntry();
            }
            return;
        }

        // 2. 检查是否允许触发
        if (CurrStageData.HasTriggeredIceBreakTip) return;

        // 前提条件 场上是否存在未破除的冰块
        if (!HasRemainingIceOnBoard()) return;
        
        // --- 条件 1: 场上剩余可答成语均没有初始字，且 8s 内未答出任何成语 ---
        if (CheckIfAllAvailableWordsHaveNoInitial())
        {
            _noInitialStuckTimer += dt;
            if (_noInitialStuckTimer >= NO_INITIAL_WORD_STUCK_TIME)
            {
                TriggerShowIceBreakEntry();
                return;
            }
        }
        else
        {
            _noInitialStuckTimer = 0f;
        }

        // --- 条件 2: 玩家有交互，但 15s 内未答对任何词 ---
        if (_hasPlayerOperatedThisLevel)
        {
            _operatedStuckTimer += dt;
            if (_operatedStuckTimer >= OPERATED_STUCK_TIME)
            {
                TriggerShowIceBreakEntry();
                return;
            }
        }
    }

    /// <summary>
    /// 触发弹出破冰广告入口
    /// </summary>
    private void TriggerShowIceBreakEntry()
    {
        CurrStageData.HasTriggeredIceBreakTip = true;
        CurrStageData.SaveToFile();
        _isIceBreakShowing = true;
        _iceBreakCountdown = ICE_COUNTDOWN_TOTAL;

        bool isFirstFree = !GameDataManager.Instance.OverallRank.hasUsedIceBreakFree;

        if (iceFreeObj != null) iceFreeObj.SetActive(isFirstFree);
        if (iceAdsObj != null) iceAdsObj.SetActive(!isFirstFree);

        if (IceBreakTable != null)
        {
            IceBreakTable.gameObject.SetActive(true);
            IceBreakTable.transform.DOKill();
            if (IceBreakCanvasGroup != null)
            {
                IceBreakCanvasGroup.DOKill();
                IceBreakCanvasGroup.alpha = 0f;
                IceBreakCanvasGroup.blocksRaycasts = true; // 开启响应
                IceBreakCanvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutQuad);
            }
            IceBreakTable.transform.localScale = Vector3.one * 0.85f;
            IceBreakTable.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
        }

        // 播放破冰锤小动效
        if (IceHammerObj != null)
        {
            IceHammerObj.transform.DOKill();
            IceHammerObj.transform.localEulerAngles = Vector3.zero;
            IceHammerObj.transform.DORotate(new Vector3(0, 0, -25f), 0.25f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }
    }

    /// <summary>
    /// 倒计时结束或成功使用后隐藏
    /// </summary>
    private void HideIceBreakEntry()
    {
        _isIceBreakShowing = false;
        if (IceHammerObj != null) IceHammerObj.transform.DOKill();

        if (IceBreakTable != null && IceBreakTable.gameObject.activeSelf)
        {
            IceBreakTable.transform.DOKill();
            
            if (IceBreakCanvasGroup != null)
            {
                IceBreakCanvasGroup.DOKill();
                IceBreakCanvasGroup.blocksRaycasts = false; // 正在退场时立即拦截点击，防止玩家误触

                // 透明度淡出 + 轻微缩小
                IceBreakCanvasGroup.DOFade(0f, 0.2f).SetEase(Ease.InQuad);
            }
            IceBreakTable.transform.DOScale(Vector3.one * 0.85f, 0.2f)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    IceBreakTable.gameObject.SetActive(false);
                    IceBreakTable.transform.localScale = Vector3.one; // 还原尺寸供下次使用
                });
        }
    }

    /// <summary>
    /// 点击破冰按钮
    /// </summary>
    private void OnIceBreakClicked()
    {
        NotifyPlayerInteraction();
        bool isFirstFree = !GameDataManager.Instance.OverallRank.hasUsedIceBreakFree;

        if (isFirstFree)
        {
            GameDataManager.Instance.OverallRank.hasUsedIceBreakFree = true;
            ExecuteIceBreakReward();
        }
        else
        {
            AnalyticMgr.VideoAdClick("破冰广告");
            // 呼叫激励视频播放
            AdRuleManager.Instance.TryShowRewardVideo(Define.AdKey.RewardAdIdStoreGold,(success) =>
            {
                if (success)
                { 
                    ExecuteIceBreakReward();
                    AnalyticMgr.VideoAdSuccess("破冰广告");
                }
                else
                {
                    MessageSystem.Instance.ShowTip("广告加载失败，请稍后重试。");
                    AnalyticMgr.VideoAdFail("破冰广告");
                }
            });
        }
    }

    /// <summary>
    /// 核心效果：破除场上所有冰块
    /// </summary>
    private void ExecuteIceBreakReward()
    {
        HideIceBreakEntry();

        if (chessboardGrid == null || chessboardGrid.GridList == null) return;

        // 收集所有带冰的格子
        var icedTiles = chessboardGrid.GridList.Values
            .Where(tile => tile.chesspiece != null && tile.chesspiece.hasIce)
            .ToList();

        if (icedTiles.Count == 0) return;

        // AudioManager.Instance.PlaySoundEffect("ice_break"); // 播放冰块碎裂音效

        foreach (var tile in icedTiles)
        {
            tile.chesspiece.hasIce = false;
            // 播放碎冰特效与隐藏冰块层
            StartCoroutine(tile.PlayIceBreakAnim()); 
            ChessStageController.Instance.ModifyChreepiece(tile.chesspiece);
        }

        // 计入成就
        CurrStageData.CurBreakIceCount += icedTiles.Count;
    }

    /// <summary>
    /// 场上是否还有未破除的冰块
    /// </summary>
    private bool HasRemainingIceOnBoard()
    {
        if (chessboardGrid == null || chessboardGrid.GridList == null) return false;
        return chessboardGrid.GridList.Values.Any(v => v.chesspiece != null && v.chesspiece.hasIce);
    }

    /// <summary>
    /// 校验场上剩余“可答成语”是否全部没有初始字
    /// 可答成语定义：未答出、所属全部格子均无冰块覆盖、且全部待填格无初始固定字
    /// </summary>
    private bool CheckIfAllAvailableWordsHaveNoInitial()
    {
        if (CurrStageInfo == null || CurrStageInfo.PhraseGroups == null) return false;

        var availableGroups = new List<PhraseGroup>();

        foreach (var group in CurrStageInfo.PhraseGroups)
        {
            string phrase = string.Join("", group.chesspieces.Select(p => p.letter));
            if (UsedPuzzles.Contains(phrase)) continue; // 已答对跳过

            // 检查该成语是否被冰块覆盖
            bool isBlockedByIce = group.chesspieces.Any(cp =>
            {
                if (chessboardGrid.GridList.TryGetValue((cp.row, cp.col), out var view))
                {
                    return view.chesspiece != null && view.chesspiece.hasIce;
                }
                return false;
            });

            if (!isBlockedByIce)
            {
                availableGroups.Add(group);
            }
        }

        if (availableGroups.Count == 0) return false;

        // 必须所有“可答成语”都不含任何初始字 (isInitialFixed == false)
        return availableGroups.All(group => group.chesspieces.All(p => !p.isInitialFixed));
    }
}