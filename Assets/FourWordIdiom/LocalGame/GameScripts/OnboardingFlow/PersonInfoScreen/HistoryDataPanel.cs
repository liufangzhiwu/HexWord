using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Linq;

// 用于传递历史数据的结构体
public struct HistoryData
{
    public int avatar;
    public string playerName;
    public int zenLevel;
    public long zenScore;
    public int maxStreakDays;
    public int coins;
    public int crosswordProgress;
    public string registerDate; // 或者使用 DateTime
}

public class HistoryDataPanel : UIWindow
{
    [Header("User Info UI")] 
    [SerializeField] private Text title;
    [SerializeField] private Text tipText;
    [SerializeField] private Image avatarImage;
    [SerializeField] private Text nameText;

    [Header("Stats UI")] 
    [SerializeField] private Text zenLevelName;
    [SerializeField] private Text zenLevelText;
    [SerializeField] private Text zenScoreName;
    [SerializeField] private Text zenScoreText;
    [SerializeField] private Text maxStreakDaysText;
    [SerializeField] private Text maxStreakText;
    [SerializeField] private Text coinName;
    [SerializeField] private Text coinsText;
    [SerializeField] private Text crosswordProgressName;
    [SerializeField] private Text crosswordProgressText;
    [SerializeField] private Text registerDateName;
    [SerializeField] private Text registerDateText;
    [Header("Buttons")]
    [SerializeField] private Button discardButton;
    [SerializeField] private Button applyButton;

    // 回调事件
    private Action onDiscardAction;
    private Action<HistoryData> onApplyAction;
    
    // 当前缓存的数据
    private HistoryData currentData;

    protected override void Awake()
    {
        // 绑定按钮事件
        discardButton.AddClickAction(OnDiscardClicked);
        applyButton.AddClickAction(OnApplyClicked);
        title.text = MultilingualManager.Instance.GetString("HistoricalData");
        tipText.text = MultilingualManager.Instance.GetString("DataEffect");
        zenLevelName.text = MultilingualManager.Instance.GetString("ZenLevel");
        zenScoreName.text = MultilingualManager.Instance.GetString("ZenScore");
        maxStreakDaysText.text = MultilingualManager.Instance.GetString("LongestWinDay");
        // coinName.text = MultilingualManager.Instance.GetString("CoinName");
        crosswordProgressName.text = MultilingualManager.Instance.GetString("LevelProgress");
        // registerDateName.text = MultilingualManager.Instance.GetString("RegisterDate");
        
        discardButton.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("GiveUp");
        applyButton.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("Use");
    }

    /// <summary>
    /// 初始化并显示历史数据面板
    /// </summary>
    /// <param name="data">需要显示的数据</param>
    /// <param name="onDiscard">点击放弃的回调</param>
    /// <param name="onApply">点击应用的回调</param>
    public void ShowPanel(HistoryData data, Action onDiscard, Action<HistoryData> onApply)
    {
        currentData = data;
        onDiscardAction = onDiscard;
        onApplyAction = onApply;

        RefreshUI(data);
    }
    
    private void RefreshUI(HistoryData data)
    {
        // 如果有头像系统，可以解除下方注释
        avatarImage.sprite = LoadheadIcon(data.avatar);
        nameText.text = data.playerName;
        string finalLevelName = data.zenLevel.ToString();
        // 确保排行榜配置已加载
        if (OverallRankingManager.Instance != null && OverallRankingManager.Instance.RealmLevelList != null)
        {
            // 从配置表里找到跟当前等级匹配的数据
            var realmData = OverallRankingManager.Instance.RealmLevelList.FirstOrDefault(r => r.Level == data.zenLevel);
        
            if (realmData != null && !string.IsNullOrEmpty(realmData.NameKey))
            {
                // 用查到的 NameKey (如 ZL01) 去多语言表翻译，注意传入 "hudie" 模块名
                finalLevelName = MultilingualManager.Instance.GetString(realmData.NameKey, "hudie");
            }
        }
        zenLevelText.text = finalLevelName;
        zenScoreText.text = data.zenScore.ToString();
        maxStreakText.text = data.maxStreakDays.ToString();
        coinsText.text = data.coins.ToString();
        crosswordProgressText.text = data.crosswordProgress.ToString();
        registerDateText.text = data.registerDate;
    }

    private void OnDiscardClicked()
    {
        onDiscardAction?.Invoke();
        Close();
    }

    private void OnApplyClicked()
    {
        onApplyAction?.Invoke(currentData);
        Close();
    }
    
    private Sprite LoadheadIcon(int idx)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("head" + idx,"UserHeadIcons");
    }
}