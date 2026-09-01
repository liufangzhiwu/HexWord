using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 🌟 定义一个数据结构来接收传入的玩家信息
public class MonthlyTopPlayer
{
    public int Rank;      // 排名 (1, 2, 或 3)
    public int Avatar;    // 头像ID
    public string Frame;
    public string Name;   // 玩家昵称
    public int Score;     // 禅意分数
}
public class MonthlyRankingRecord : MonoBehaviour
{
    [Header("Title")]
    [SerializeField] private Text dateText; // 对应 DateText (例如 "2026-07")
    
    // 🌟 将单个名次需要控制的 UI 打包在一起，方便在 Inspector 面板中拖拽和管理
    [System.Serializable]
    public class RankUIElements
    {
        public GameObject RankRoot;      // 对应 Rank1 / Rank2 / Rank3 根节点
        public Text NameText;            // 对应 RankXNameText
        public Text ScoreText;           // 对应 RankXScoreText
        public Image AvatarImage;        // 对应 RankXPohto 下的 Image
        public Image FrameImage;
    }
    
    [Header("Top 3 Players UI")]
    [SerializeField] private RankUIElements rank1UI;
    [SerializeField] private RankUIElements rank2UI;
    [SerializeField] private RankUIElements rank3UI;
    
    // 内部数组，方便使用循环进行批量操作
    private RankUIElements[] _rankUIs;
    
    private void Awake()
    {
        // 初始化数组，索引 0 对应第一名，1 对应第二名，2 对应第三名
        _rankUIs = new RankUIElements[] { rank1UI, rank2UI, rank3UI };
    }
    /// <summary>
    /// 外部调用此方法来刷新整个月份的数据卡片
    /// </summary>
    /// <param name="date">月份字符串，例如 "2026-07"</param>
    /// <param name="topPlayers">本月排名前三的玩家数据列表</param>
    public void SetRecordData(string date, List<MonthlyTopPlayer> topPlayers)
    {
        // 1. 设置顶部日期
        if (dateText != null)
        {
            dateText.text = date;
        }

        // 2. 为了防止数据不满3个人的情况（比如新开服只有2个人），先将所有排名的根节点隐藏
        for (int i = 0; i < _rankUIs.Length; i++)
        {
            if (_rankUIs[i].RankRoot != null)
            {
                _rankUIs[i].RankRoot.SetActive(false);
            }
        }

        if (topPlayers == null || topPlayers.Count == 0) return;

        // 3. 遍历传入的玩家数据，激活对应的 UI 并赋值
        foreach (var player in topPlayers)
        {
            // 防御性判断，确保排名在 1-3 之间
            if (player.Rank < 1 || player.Rank > 3) continue;

            // 数组的索引等于 排名减1 (Rank 1 -> Index 0)
            int index = player.Rank - 1;
            RankUIElements ui = _rankUIs[index];

            if (ui.RankRoot != null)
            {
                ui.RankRoot.SetActive(true); // 激活该名次的 UI 显示
                
                string displayName = player.Name;
                if (!string.IsNullOrEmpty(displayName) && displayName.Length > 6)
                {
                    displayName = displayName.Substring(0, 5) + "..";
                }
                ui.NameText.text = displayName;
                ui.ScoreText.text = player.Score.ToString();
                // 加载头像
                ui.AvatarImage.sprite = LoadHeadIcon("head" + player.Avatar);
                    // string avaStr = UIUtilities.ExtractNumber(player.Avatar);
                    // int avaIdx = string.IsNullOrEmpty(avaStr) ? 0 : int.Parse(avaStr);
               if(player.Frame != null) ui.FrameImage.sprite = LoadHeadIcon("AvatarFrameIcon" + player.Frame);
            }
        }
    }

    // 复用之前的头像加载方法
    private Sprite LoadHeadIcon(string idx)
    {
        // 请确保 AdvancedBundleLoader 在当前环境可用
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(idx, "UserHeadIcons");
    }
}
