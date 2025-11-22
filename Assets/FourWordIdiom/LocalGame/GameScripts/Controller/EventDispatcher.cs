using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 集中管理游戏内事件的发布与订阅
/// </summary>
public class EventDispatcher:MonoBehaviour
{
    public static EventDispatcher instance;
    
    #region 事件声明区域
    private Action<string> _onShowSelectedPuzzle;
    private Action<string, List<int[]>> _onLetterSelected;
    private Action<List<int[]>, bool> _onPlayChoicePuzzle;
    private Action<int, bool> _onChangeGoldUI;
    private Action _onFakeBonusEvent;
    private Action<string> _onRemoveNotePuzzle;
    private Action<bool> _onUpdateRewardPuzzle;
    private Action<bool, bool> _onUpdateLayerCoin;
    
    private Action _onCheckShowChessTutorial; // 填字教程检查
    /// <summary>
    /// 设置选中词语展示区显状态
    /// </summary>
    private Action<bool> _onChoicePuzzleSetStatus;
    private Action _onCheckShowTutorial;
    private Action<bool> _onChangeTopRaycast;
    private Action _onUpdateGameLobbyUI;

    /// <summary>
    /// 更新词库按钮状态
    /// </summary>
    public Action OnWordVocabularyStatus;
    #endregion

    private void Awake()
    {
        instance = this;
    }

    #region 公共事件接口
    /// <summary>显示选中的词语事件</summary>
    public event Action<string> OnShowSelectedPuzzle
    {
        add => _onShowSelectedPuzzle += value;
        remove => _onShowSelectedPuzzle -= value;
    }

    /// <summary>字母选中事件</summary>
    public event Action<string, List<int[]>> OnLetterSelected
    {
        add => _onLetterSelected += value;
        remove => _onLetterSelected -= value;
    }

    /// <summary>播放字块矩阵动画事件</summary>
    public event Action<List<int[]>, bool> OnPlayChoicePuzzle
    {
        add => _onPlayChoicePuzzle += value;
        remove => _onPlayChoicePuzzle -= value;
    }

    /// <summary>金币数量更新事件</summary>
    public  event Action<int, bool> OnChangeGoldUI
    {
        add => _onChangeGoldUI += value;
        remove => _onChangeGoldUI -= value;
    }

    /// <summary>虚拟奖励事件</summary>
    public event Action OnFakeBonusEvent
    {
        add => _onFakeBonusEvent += value;
        remove => _onFakeBonusEvent -= value;
    }

    /// <summary>移出生词本词语事件</summary>
    public  event Action<string> OnRemoveNotePuzzle
    {
        add => _onRemoveNotePuzzle += value;
        remove => _onRemoveNotePuzzle -= value;
    }

    /// <summary>更新奖励词语事件</summary>
    public event Action<bool> OnUpdateRewardPuzzle
    {
        add => _onUpdateRewardPuzzle += value;
        remove => _onUpdateRewardPuzzle -= value;
    }

    /// <summary>更新金币层级事件</summary>
    public event Action<bool, bool> OnUpdateLayerCoin
    {
        add => _onUpdateLayerCoin += value;
        remove => _onUpdateLayerCoin -= value;
    }

    /// <summary>设置词语展示区状态事件</summary>
    public event Action<bool> OnChoicePuzzleSetStatus
    {
        add => _onChoicePuzzleSetStatus += value;
        remove => _onChoicePuzzleSetStatus -= value;
    }

    /// <summary>检查新手引导事件</summary>
    public event Action OnCheckShowTutorial
    {
        add => _onCheckShowTutorial += value;
        remove => _onCheckShowTutorial -= value;
    }

    /// <summary>切换顶部射线检测事件</summary>
    public event Action<bool> OnChangeTopRaycast
    {
        add => _onChangeTopRaycast += value;
        remove => _onChangeTopRaycast -= value;
    }
    
    /// <summary>
    /// 头像切换时UI界面刷新
    /// </summary>
    public event Action OnUpdateGameLobbyUI  
    {
        add => _onUpdateGameLobbyUI += value;
        remove => _onUpdateGameLobbyUI -= value;
    }
    
    /// <summary>检查填字新手引导事件</summary>
    public event Action OnCheckShowChessTutorial
    {
        add => _onCheckShowChessTutorial += value;
        remove => _onCheckShowChessTutorial -= value;
    }
    
    #endregion

    #region 事件触发方法
    public void TriggerShowSelectedPuzzle(string puzzle)
        => _onShowSelectedPuzzle?.Invoke(puzzle);

    public void TriggerLetterSelected(string letter, List<int[]> positions)
        => _onLetterSelected?.Invoke(letter, positions);

    public void TriggerPlayChoicePuzzle(List<int[]> positions, bool state)
        => _onPlayChoicePuzzle?.Invoke(positions, state);

    public void TriggerChangeGoldUI(int amount, bool animate)
        => _onChangeGoldUI?.Invoke(amount, animate);

    public  void TriggerFakeBonusEvent()
        => _onFakeBonusEvent?.Invoke();

    public void TriggerRemoveNotePuzzle(string puzzle)
        => _onRemoveNotePuzzle?.Invoke(puzzle);

    public void TriggerUpdateRewardPuzzle(bool state)
        => _onUpdateRewardPuzzle?.Invoke(state);

    public void TriggerUpdateLayerCoin(bool immediate, bool animate)
        => _onUpdateLayerCoin?.Invoke(immediate, animate);

    public void TriggerChoicePuzzleSetStatus(bool visible)
        => _onChoicePuzzleSetStatus?.Invoke(visible);

    public void TriggerCheckShowTutorial()
        => _onCheckShowTutorial?.Invoke();

    public void TriggerChangeTopRaycast(bool enable)
        => _onChangeTopRaycast?.Invoke(enable);
    
   
    
    public void TriggerOnUpdateGameLobbyUI()
        => _onUpdateGameLobbyUI?.Invoke();
    
    
    /// <summary>
    /// 触发填字检查
    /// </summary>
    public void TriggerCheckShowChessTutorial()
        => _onCheckShowChessTutorial?.Invoke();
    
    #endregion
}