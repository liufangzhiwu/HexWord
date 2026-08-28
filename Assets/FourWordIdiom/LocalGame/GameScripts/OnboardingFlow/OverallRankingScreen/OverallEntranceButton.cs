using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class OverallEntranceButton : MonoBehaviour
{
    [SerializeField] private Button entranceButton;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text levelText;
    [SerializeField] private Text nameText;
    [SerializeField] private GameObject newBadge;
    [SerializeField] private GameObject noNetIcon;

    private void Awake()
    {
        entranceButton.AddVibraClickAction(OnEntranceClicked);
    }

    // Start is called before the first frame update
    private void OnEnable()
    {
        RefreshState();
    }

    public void RefreshState()
    {
        int myScore = GameDataManager.Instance.UserData.overallZenScore;
        
        // 1. 解锁控制：禅意值不为0时解锁
        gameObject.SetActive(myScore != 0);
        if (myScore == 0) return;

        // 2. 刷新分数与等级信息
        scoreText.text = myScore.ToString();
        var realmList = OverallRankingManager.Instance.RealmLevelList;
        if (realmList == null || realmList.Count == 0) return;
        
        // 动态计算当前称号
        int currentLevel = OverallRankingManager.Instance.GetZenLevelByScore(myScore);
        var currentRealm = realmList.FirstOrDefault(r => r.Level == currentLevel);
        if (currentRealm != null)
        {
            string desc = MultilingualManager.Instance.GetString("Level","hudie");
            // levelText.text =  desc.Replace("X", currentRealm.Level.ToString());
            levelText.text =  string.Format(desc, currentRealm.Level);
            nameText.text = MultilingualManager.Instance.GetString(currentRealm.NameKey, "hudie");
        }

        // 3. 网络状态
        bool isNoNet = !GameCoreManager.Instance.IsNetworkActive;
        noNetIcon.SetActive(isNoNet);

        // 4. “新”角标控制 (检查本地是否已经点过)
        if (isNoNet)
        {
            newBadge.SetActive(false);
        }
        else
        {
            bool hasClickedBefore = GameDataManager.Instance.OverallRank.HasClickedZenEntrance;
            // 检查总榜是否刚解锁且未点击
            bool isTotalUnlocked = OverallRankingManager.Instance.IsTotalRankUnlocked(myScore);
            bool needShowTotalBadge = isTotalUnlocked && !GameDataManager.Instance.OverallRank.HasViewedTotalRankUnlock;
            // 检查月榜是否刚解锁且未点击
            bool isMonthlyUnlocked = OverallRankingManager.Instance.IsMonthlyRankUnlocked(myScore);
            bool needShowMonthlyBadge = isMonthlyUnlocked && !GameDataManager.Instance.OverallRank.HasViewedMonthlyRankUnlock;
            
            newBadge.SetActive(!hasClickedBefore || needShowTotalBadge || needShowMonthlyBadge);
        }
    }
    
    private void OnEntranceClicked()
    {
        if (!GameCoreManager.Instance.IsNetworkActive)
        {
            string tip = MultilingualManager.Instance.GetString("PoorNetwork","hudie");
            MessageSystem.Instance.ShowTip(tip);
            bool hasAnyCache = OverallRankingManager.Instance.IsTotalRankCached() || 
                               OverallRankingManager.Instance.IsMonthlyRankCached() || 
                               OverallRankingManager.Instance.IsHallOfFameCached();
            if(!hasAnyCache)  return;
        }
        // 消除角标并持久化保存
        if (newBadge.activeSelf)
        {
            newBadge.SetActive(false);
            GameDataManager.Instance.OverallRank.HasClickedZenEntrance = true;
            int myScore = GameDataManager.Instance.UserData.overallZenScore;
            // 如果此时总榜是解锁的，就将总榜的红点状态标记为已看
            if (OverallRankingManager.Instance.IsTotalRankUnlocked(myScore))
            {
                if (!GameDataManager.Instance.OverallRank.HasViewedTotalRankUnlock)
                {
                    GameDataManager.Instance.OverallRank.HasViewedTotalRankUnlock = true;
                }
            }

            // 如果此时月榜是解锁的，就将月榜的红点状态标记为已看
            if (OverallRankingManager.Instance.IsMonthlyRankUnlocked(myScore))
            {
                if (!GameDataManager.Instance.OverallRank.HasViewedMonthlyRankUnlock)
                {
                    GameDataManager.Instance.OverallRank.HasViewedMonthlyRankUnlock = true;
                }
            }
            GameDataManager.Instance.CommitGameData();
        }
        SystemManager.Instance.HidePanel(PanelType.HeaderSection);
        SystemManager.Instance.HidePanel(PanelType.PrimaryInterface);
        // 打开排行榜主界面
        SystemManager.Instance.ShowPanel(PanelType.OverallRankingScreen);
    }
}
