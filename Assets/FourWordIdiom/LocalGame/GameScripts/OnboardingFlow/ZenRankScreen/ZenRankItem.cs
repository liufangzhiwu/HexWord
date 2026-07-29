using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ZenRankState
{
    public int PlayerId;
    public int Rank;
    public int Avatar;
    public string Name;
    public string Level;
    public int Score;
    public int Reward;
}

public class ZenRankItem : MonoBehaviour
{
    [SerializeField] private GameObject Rank;
    [SerializeField] private Image Avatar;
    [SerializeField] private Text Name;
    [SerializeField] private Text Score;
    [SerializeField] private Button BoxReward;
    [SerializeField] private GameObject GoldReward;
    
    [Header("我的分数背景")]
    [SerializeField] private Image zenscoreBg;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite selectedSprite;
    
    // 🌟 暴露出分数和排名的 Text 组件，供外部控制动画
    public Text ScoreText => Score;
    public Text RankText { get; private set; }
    public Image RankIcon { get; private set; }
    
    private bool boxLocked = false;   // 🌟 宝箱动画锁

    
    private int currentRank;
    private bool isMe; // 🌟 标识这条数据是不是玩家自己
    private Sprite _sprite;

    private void Awake()
    {
        _sprite = GetComponent<Image>().sprite;
        RankText = Rank.GetComponentInChildren<Text>(true);
        RankIcon = Rank.GetComponentInChildren<Image>(true);
    }

    private void Start()
    {
        BoxReward.AddClickAction(OnBoxClicked);
    }
    
    public void SetRankInfo(ZenRankState state, bool isDisplayOnly = false)
    {
        currentRank = state.Rank;
        // GoPlay.gameObject.SetActive(false);
        //isMe = state.Name == GameDataManager.Instance.UserData.UserName;
        isMe = state.PlayerId == int.Parse(GameDataManager.Instance.UserData.PlayerId);
        if (isDisplayOnly)
        {
            BoxReward.interactable = false; // 禁用宝箱点击
        }
        else
        {
            BoxReward.interactable = true;
        }
        RankText.gameObject.SetActive(false);
        RankIcon.gameObject.SetActive(false);
        BoxReward.gameObject.SetActive(false);
        GoldReward.SetActive(false);
        
        if (state.Rank < 4)
        {
            RankIcon.gameObject.SetActive(true);
            RankIcon.sprite = LoadRankIcon(state.Rank,"Rankicon");
            BoxReward.GetComponent<Image>().sprite = LoadRankIcon(state.Rank, "RankBox");
            BoxReward.gameObject.SetActive(true);
        }
        else
        {
            RankText.gameObject.SetActive(true);
            RankText.text = state.Rank.ToString();
            if (state.Reward > 0 )
            {
                GoldReward.GetComponentInChildren<Text>(true).text = $"×{state.Reward}";
                GoldReward.SetActive(true);
            }
        }
        Avatar.sprite = LoadheadIcon(state.Avatar);
        Name.text = state.Name;
        Score.text = state.Score.ToString();

        if (isMe)
        {
            if (isDisplayOnly)
            {
                GetComponent<Image>().sprite =
                    AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("change_rank_me");
            }
            else
            {
                GetComponent<Image>().sprite =
                    AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("mide");
            }
           
            if (ColorUtility.TryParseHtmlString("#823F15", out Color newColor))
            {
                Name.color = newColor;
                Score.color = newColor;
                RankText.color = newColor;
            }

            zenscoreBg.sprite = selectedSprite;
        }
        else
        {
            GetComponent<Image>().sprite = _sprite;
            Name.color = Color.white;
            Score.color = Color.white;
            RankText.color = Color.white;
            zenscoreBg.sprite = normalSprite;
        }
            
    }
    
    // ==========================================
    // 🌟 新增：专供排行榜变动动画调用的动态刷新方法
    // 自动处理从文字变图标的跨界情况
    // ==========================================
    public void UpdateRankVisual(int newRank, bool isDisplayOnly = false)
    {
        currentRank = newRank;

        // 先把文字和图标都关掉
        if (RankText != null) RankText.gameObject.SetActive(false);
        if (RankIcon != null) RankIcon.gameObject.SetActive(false);
        if (BoxReward != null) BoxReward.gameObject.SetActive(false);
        if (GoldReward != null) GoldReward.SetActive(false);

        // 重新判定该显示什么
        if (newRank < 4 && newRank > 0)
        {
            if (RankIcon != null)
            {
                RankIcon.gameObject.SetActive(true);
                RankIcon.sprite = LoadRankIcon(newRank, "Rankicon");
            }
            // 如果从4名开外飞进了前3名，右侧奖励动态切成宝箱！
            if ((!isMe || isDisplayOnly) && BoxReward != null)
            {
                BoxReward.GetComponent<Image>().sprite = LoadRankIcon(newRank, "RankBox");
                BoxReward.gameObject.SetActive(true);
            }
        }
        else
        {
            if (RankText != null)
            {
                RankText.gameObject.SetActive(true);
                RankText.text = newRank <= 0 ? "-" : newRank.ToString();
            }
        }
    }

    private Sprite LoadheadIcon(int idx)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("head" + idx);
    }
    private Sprite LoadRankIcon(int idx, string iconName = "RankBox")
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(iconName + idx);
    }
    
    private void OnBoxClicked()
    {
        // 🌟 如果点的是其他玩家的宝箱（前3名）
        if (currentRank >= 4) return; 
        // ① 检查动画锁
        if (boxLocked) return;
        // 防狂点保护 1：如果在播放中，忽略点击
        if (DOTween.IsTweening(BoxReward.transform)) return;
        
        boxLocked = true;  // 加锁并开始动画
        
        // 防狂点保护 2：播放前强制归零，防止任何历史残留导致越变越大或永久歪斜
        BoxReward.transform.localRotation = Quaternion.identity;
        BoxReward.transform.localScale = Vector3.one;

        // 🌟 创建一个 DOTween 动画序列
        Sequence seq = DOTween.Sequence();

        // 步骤 1：先放大 (用 0.15 秒平滑放大到 1.2 倍)
        seq.Append(BoxReward.transform.DOScale(new Vector3(1.2f, 1.2f, 1f), 0.15f).SetEase(Ease.OutQuad));

        // 步骤 2：在放大的状态下进行 Q弹摇摆抖动 (持续 0.3 秒)
        // DOPunchRotation 播放完会自动回到 0 度，所以不用担心停在半空
        seq.Append(BoxReward.transform.DOPunchRotation(new Vector3(0, 0, 15f), 0.3f, 10, 1f));

        // 步骤 3：抖动结束后，缩小回原样 (用 0.15 秒平滑缩回 1 倍大小)
        seq.Append(BoxReward.transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.InQuad));
            
        seq.OnComplete(() => boxLocked = false);
        // 绑定目标，方便后续管理（可选）
        seq.SetTarget(BoxReward.transform); 
          
        
    }
}
