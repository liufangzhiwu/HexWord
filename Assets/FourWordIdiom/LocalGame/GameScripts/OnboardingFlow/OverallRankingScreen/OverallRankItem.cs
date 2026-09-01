using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public struct OverallRankState
{
    public int PlayerId;
    public int Rank;
    public int Avatar;
    public string Frame;
    public string Name;
    public int Score;
    public int Reward;
}

public class OverallRankItem : MonoBehaviour
{
    [Header("Rank UI")]
    [SerializeField] private Image iconBg;         // 对应 Rank/IconBg (前3名的底板)
    [SerializeField] private Image rankIcon;       // 对应 Rank/IconBg/Image (前3名的勋章)
    [SerializeField] private Text rankText;        // 对应 Rank/Text (4名以后的数字)
    [Header("Info UI")]
    [SerializeField] private Image avatar;
    [SerializeField] private Image frame;
    [SerializeField] private Text nameText;
    [SerializeField] private Text scoreText;
    [Header("Reward UI")]
    [SerializeField] private GameObject rewardRoot;
    [SerializeField] private Image boxReward;      // 对应 Reward/Box
    [SerializeField] private GameObject goldRoot;  // 对应 Reward/Gold
    [SerializeField] private Text goldText;        // 对应 Reward/Gold/Text
    
    private bool boxLocked = false;   // 宝箱动画锁
    
    private int currentRank;
    private bool isMe; // 标识这条数据是不是玩家自己
    private Sprite _normalBgSprite; // 用于缓存默认的背景图
    private int _playerId; // 缓存当前玩家的ID，用于查询资料
    private bool _isDisplayOnly; // 缓存当前条目是否为"仅展示"(即是否在结算页)
    private void Awake()
    {
        _normalBgSprite = GetComponent<Image>().sprite;
    }
    private void Start()
    {
        // 1. 绑定宝箱点击事件
        Button boxBtn = boxReward.GetComponent<Button>();
        if (boxBtn != null)
        {
            boxBtn.AddClickAction(OnBoxClicked);
        }
        // 2. 绑定头像和名称的点击事件
        BindClickEvent(avatar);
        BindClickEvent(nameText);
    }

    public void SetRankInfo(OverallRankState state,bool isMonthly, bool isDisplayOnly = false)
    {
        _playerId = state.PlayerId; // 记录PlayerId
        _isDisplayOnly = isDisplayOnly; // 存下来，留给点击头像时判断使用
        currentRank = state.Rank;
        isMe = !string.IsNullOrEmpty(GameDataManager.Instance.UserData.PlayerId) && 
               state.PlayerId == int.Parse(GameDataManager.Instance.UserData.PlayerId);
        Button boxBtn = boxReward.GetComponent<Button>();
        if (boxBtn != null)
        {
            boxBtn.interactable = !isDisplayOnly;
        }
        // 初始化：全部隐藏
        rankText.gameObject.SetActive(false);
        rankIcon.gameObject.SetActive(false);
        iconBg.gameObject.SetActive(false);
        boxReward.gameObject.SetActive(false);
        goldRoot.SetActive(false);
        // 前三名展示图标和宝箱
        if (state.Rank < 4 && state.Rank > 0)
        {
            rankIcon.gameObject.SetActive(true);
            iconBg.gameObject.SetActive(true);
            iconBg.sprite = LoadRankIcon(state.Rank, "OverRankBg");
            rankIcon.sprite = LoadRankIcon(state.Rank, "OverRankIcon");
            if (isMonthly)
            {
                boxReward.sprite = LoadRankIcon(state.Rank, "OverRankBox");
                boxReward.gameObject.SetActive(true);
            }
        }
        else // 四名及以后展示文字排名和金币
        {
            rankText.gameObject.SetActive(true);
            rankText.text = OverallRankingManager.Format(state.Rank);
            // if (isMonthly && state.Reward > 0)
            // {
            //     goldText.text = $"×{state.Reward}";
            //     goldRoot.SetActive(true);
            // }
        }
        
        // 设置基础信息
        avatar.sprite = LoadheadIcon("head" + state.Avatar);
        frame.sprite = LoadheadIcon("AvatarFrameIcon" + state.Frame);
        string displayName = state.Name;
        if (!string.IsNullOrEmpty(displayName) && displayName.Length > 8)
        {
            displayName = displayName.Substring(0, 8) + "..";
        }
        nameText.text = displayName;
        scoreText.text = state.Score.ToString();
        
        // "自己" 与 "其他玩家" 的样式区分
        Image bgImage = GetComponent<Image>();
        if (isMe)
        {
            // 结算界面的"我" vs 榜单界面的"我" 背景不同
            // if (isDisplayOnly)
            // {
            //     bgImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("rank_item_di", "OnboardingFlow");
            // }
            // else
            // {
                bgImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("over_rank_me", "OnboardingFlow");
            // }
           
            // 修改自己排行的文字颜色
            if (ColorUtility.TryParseHtmlString("#823F15", out Color newColor))
            {
                nameText.color = newColor;
                scoreText.color = newColor;
                rankText.color = newColor;
            }
        }
        else
        {
            if (ColorUtility.TryParseHtmlString("#3D3D3D", out Color normalColor))
            {
                nameText.color = normalColor;
                scoreText.color = normalColor;
                rankText.color = normalColor;
            }
            // 恢复普通玩家的背景和文字颜色
            bgImage.sprite = _normalBgSprite;
        }
    }
    private Sprite LoadheadIcon(string showIcon)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon,"UserHeadIcons");
    }
    
    private Sprite LoadRankIcon(int idx, string iconName = "RankBox")
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(iconName + idx);
    }
    private void OnBoxClicked()
    {
        // 如果点的是其他玩家的金币（前3名是宝箱）
        if (currentRank >= 4) return; 
        if (_isDisplayOnly) return;
        // ① 检查动画锁
        if (boxLocked) return;
        boxLocked = true;  // 加锁并开始动画
        
        // 防狂点保护 2：播放前强制归零，防止任何历史残留导致越变越大或永久歪斜
        boxReward.transform.localRotation = Quaternion.identity;
        boxReward.transform.localScale = Vector3.one;

        // 创建一个 DOTween 动画序列
        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(true);
        // 步骤 1：先放大 (用 0.15 秒平滑放大到 1.2 倍)
        seq.Append(boxReward.transform.DOScale(new Vector3(1.2f, 1.2f, 1f), 0.15f).SetEase(Ease.OutQuad));

        // 步骤 2：在放大的状态下进行 Q弹摇摆抖动 (持续 0.3 秒)
        // DOPunchRotation 播放完会自动回到 0 度，所以不用担心停在半空
        seq.Append(boxReward.transform.DOPunchRotation(new Vector3(0, 0, 15f), 0.3f, 10, 1f));

        // 步骤 3：抖动结束后，缩小回原样 (用 0.15 秒平滑缩回 1 倍大小)
        seq.Append(boxReward.transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.InQuad));
            
        seq.OnComplete(() =>
        {
            boxLocked = false;
            boxReward.transform.localRotation = Quaternion.identity;
            boxReward.transform.localScale = Vector3.one;
        });
        seq.OnKill(() => 
        {
            boxLocked = false;
        });
        // 绑定目标，方便后续管理（可选）
        seq.SetTarget(boxReward.transform); 
    }
    
    // 辅助方法：给UI元素自动检查并绑定点击事件
    private void BindClickEvent(Component uiElement)
    {
        if (uiElement == null) return;
        
        Button btn = uiElement.GetComponent<Button>();
        if (btn == null)
        {
            // 如果预制体上没有挂载Button，代码自动添加一个
            btn = uiElement.gameObject.AddComponent<Button>();
            // 注意：自动添加的Button由于没有设置Target Graphic，可能没有点击按压的颜色变化
            // 如果需要颜色变化，建议在Prefab上手动挂载Button组件并设置过渡
        }
        
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnUserProfileClicked);
    }
    
    private void OnUserProfileClicked()
    {
        // 如果是在结算页（仅展示），直接返回，不触发点击
        if (_isDisplayOnly) return;
        // 防错：如果是空数据则不响应
        if (_playerId <= 0) return;

        UnityEngine.Debug.Log($"[RankItem] 点击了玩家，准备弹出气泡。 玩家ID: {_playerId}");

        if (_playerId.ToString() == GameDataManager.Instance.UserData.PlayerId)
        {
            SystemManager.Instance.ShowPanel(PanelType.PersonInfoScreen);
            return;
        };
        
        
        StartCoroutine(APIGateway.Instance.SocialApi.GetPublicProfile(_playerId.ToString(),(res) =>
        {
            GameCoreManager.Instance.otherPersonProfile = res;
            Debug.Log("其他用户信息 " + res);
            
            if (GameCoreManager.Instance.otherPersonProfile != null)
            {
                SystemManager.Instance.ShowPanel(PanelType.OtherPeopleScreen);
            }
            else
            {
                MessageSystem.Instance.ShowTip("用户信息未找到！");
            }
        }));
    }
}
