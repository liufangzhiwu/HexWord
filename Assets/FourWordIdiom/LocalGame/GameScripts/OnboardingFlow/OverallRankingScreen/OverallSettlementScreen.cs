using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class OverallSettlementScreen : UIWindow
{
    public const int MAX_ROWS = 6;
    
    [Header("第一步：排名结算")]
    [SerializeField] private GameObject step1Panel;
    [SerializeField] private Text step1TitleText;
    [SerializeField] private Text step1FooterText;
    [SerializeField] private Button step1Button;
    [SerializeField] private Text step1ButtonText;
    [SerializeField] private OverallRankItem[] rankRows;   // 编辑器里预摆 6 个 OverallRankItem
    
    [Header("第二步：恭喜获得")]
    [SerializeField] private GameObject step2Panel;
    [SerializeField] private Transform step2RewardsContent;
    [SerializeField] private RectTransform rewardSlotTemplate; // 子物体：Icon(Image) + Count(Text)，初始隐藏
    [SerializeField] private Button step2ContinueButton;
    [SerializeField] private GameObject step2ChestDecor;       // 图3 底部两箱，按美术稿为装饰/额外展示，无需动态填就常显
    [SerializeField] private Transform step2ChestIcon;  // 图3 中间大宝箱，飞金币起点

    private SettlementData _data;
    private bool isAnimating = false;
    private bool hasGrantedRewards = false;
    private int currentStep = 1;
    
    protected override void Awake() { base.Awake(); }
    protected override void InitializeUIComponents()
    {
        base.InitializeUIComponents();
        step1Button.AddClickAction(OnClickStep1);
        step2ContinueButton.AddClickAction(OnClickStep2Continue);
        if (rewardSlotTemplate != null) rewardSlotTemplate.gameObject.SetActive(false);
        
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        step1Panel.SetActive(true);
        step2Panel.SetActive(false);   // 每次打开先停在第一步，数据由 ShowSettlement 推
    }

    private void Start()
    {
        step1TitleText.text = MultilingualManager.Instance.GetString("LastEnd","hudie");
    }

    // ============== 入口：Manager 推数据 ==============
    public void ShowSettlement(SettlementData data)
    {
        _data = data;
        currentStep = 1;          // ← 补
        isAnimating = false;      // ← 补
        hasGrantedRewards = false;// ← 补
        step1Panel.SetActive(true);
        step2Panel.SetActive(false);
        BuildStep1List(data);

        // 按钮文案：有奖励=领取奖励，无奖励=点击继续
        step1ButtonText.text = data.HasReward
            ? MultilingualManager.Instance.GetString("ADPopReceive", "hudie")   // 领取奖励
            : MultilingualManager.Instance.GetString("Continue", "hudie");// 点击继续

        // 底部文案：上榜=赞美（按名次），未上榜=鼓励
        step1FooterText.text = GetPraiseText(data.myRank);  
    }
    
    // ============== 第一步列表：固定 6 行 ==============
    private void BuildStep1List(SettlementData data)
    {
        // 1) 合并列表，保证"上榜的自己"一定在里头（防止后端 list 没把自己带回来）
        List<LeaderboardEntry> merged = new List<LeaderboardEntry>(data.topList ?? new List<LeaderboardEntry>());
        if (data.myRank > 0 && !merged.Exists(e => e.rank == data.myRank))
        {
            merged.Add(data.myEntry);
            merged.Sort((a, b) => a.rank.CompareTo(b.rank));
        }

        // 2) 自己在前 6 → 6 个普通行；否则 → 5 普通行 + 1 金色占位行
        int normalTake = data.SelfInTop6 ? MAX_ROWS : MAX_ROWS - 1;
        var normalRows = merged.Take(normalTake).ToList();

        int idx = 0;
        for (int i = 0; i < normalRows.Count && idx < MAX_ROWS; i++, idx++)
        {
            rankRows[idx].gameObject.SetActive(true);
            rankRows[idx].SetRankInfo(MakeState(normalRows[i]), true,true); // 结算=月榜样式：前3宝箱 / 4+金币
        }

        // 3) 未上榜 或 上榜但名次>6 → 追加金色占位行
        bool needSelfRow = data.myRank <= 0 || data.myRank > MAX_ROWS;
        if (needSelfRow && idx < MAX_ROWS)
        {
            rankRows[idx].gameObject.SetActive(true);
            rankRows[idx].SetRankInfo(MakeState(data), true, true); // 见第四节
            idx++;
        }

        // 4) 多余行隐藏
        for (int i = idx; i < MAX_ROWS; i++) rankRows[i].gameObject.SetActive(false);
    }
    
    // ============== 第一步按钮：领奖励 / 翻篇 ==============
    private void OnClickStep1()
    {
        if (isAnimating) return;
        if (_data.HasReward)
        {
            FillStep2(_data.rewards);
            step1Panel.SetActive(false);
            step2Panel.SetActive(true);
            step2ContinueButton.GetComponentInChildren<Text>().text =
                MultilingualManager.Instance.GetString("Congrats","hudie"); // 恭喜获得
            currentStep = 2;
            StartCoroutine(PlayStep2Anim());
        }
        else
        {
            StartCoroutine(GrantAndCloseRoutine()); // 图2 无奖励路径
        }
    }
    
    // ============== 第二步：恭喜获得 ==============
    private void FillStep2(Dictionary<int, int> rewards)
    {
        for (int i = step2RewardsContent.childCount - 1; i >= 0; i--)
        {
            var c = step2RewardsContent.GetChild(i);
            if (c != rewardSlotTemplate.transform) Destroy(c.gameObject);
        }
        foreach (var kvp in rewards)
        {
            var slot = Instantiate(rewardSlotTemplate, step2RewardsContent);
            slot.gameObject.SetActive(true);
            Image icon = slot.transform.GetChild(0).GetComponent<Image>();

            icon.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(kvp.Key == 0 ? "gold" : "give_" + kvp.Key);
            icon.SetNativeSize();
            slot.GetComponentInChildren<Text>().text = "× " + kvp.Value;
        }
    }

    private void OnClickStep2Continue()
    {
        if (isAnimating) return;
        StartCoroutine(GrantAndCloseRoutine());
    }

    private IEnumerator PlayStep2Anim()
    {
        isAnimating = true;
        SetRankIcon(_data.myRank); 
        step2ChestIcon.localScale = Vector3.zero;       // 大宝箱
        step2RewardsContent.localScale = Vector3.zero;  // 3个道具容器

        Sequence seq = DOTween.Sequence();
        seq.Append(step2ChestIcon.DOScale(1f, 0.4f).SetEase(Ease.OutBack));      // 大宝箱"咚"弹出
        seq.AppendInterval(0.15f);
        seq.Append(step2RewardsContent.DOScale(1f, 0.4f).SetEase(Ease.OutBack)); // 3道具跟着弹出
        yield return seq.WaitForCompletion();
        isAnimating = false;
    }
    // ============== 统一收尾：失效缓存 + 关窗 ==============
    private void CloseAndFinish()
    {
        OverallRankingManager.Instance.InvalidateMonthlyCache(); // 关键：让主榜重拉新月数据
        SystemManager.Instance.HidePanel(PanelType.OverallSettlementScreen);
        // Manager.CheckMonthlySettlementRoutine 正 WaitUntil 这个面板关闭，会自动继续往下走
    }

    // ============== 工具 ==============
    private OverallRankState MakeState(LeaderboardEntry e)
    {
        return new OverallRankState
        {
            PlayerId = e.user_id, Rank = e.rank, Avatar = e.avatar,
            Name = e.nickname, Score = e.score,
            Reward = 0 // 前3由 OverallRankItem 显示宝箱
        };
    }
    private OverallRankState MakeState(SettlementData data)
    {
        return new OverallRankState
        {
            PlayerId = int.Parse(GameDataManager.Instance.UserData.PlayerId), // isMe=true → 金色底 + 棕字
            Rank     = data.myRank,    // 0=未上榜，>6=榜外真实名次
            Avatar   = data.myAvatar,
            Name     = GameDataManager.Instance.UserData.UserName,
            Score    = data.myScore,
            Reward   = 0
        };
    }

    private string GetPraiseText(int rank)
    {
        var desc = "";
        if (rank > 0 && rank <= 3)
        {
             desc = MultilingualManager.Instance.GetString($"Ranktext{rank}","hudie");
        }
        else
        {
            desc = MultilingualManager.Instance.GetString("NORank","hudie");
        }
        return desc;
    }
    // 发奖闭环（照搬段位 GrantAndCloseRoutine，接口换成月榜）
    private IEnumerator GrantAndCloseRoutine()
    {
        if (hasGrantedRewards) yield break;   // 防重复发奖
        isAnimating = true;
        hasGrantedRewards = true;

        bool isRequestDone = false;
        bool isSuccess = false;
        yield return APIGateway.Instance.LeaderboardApi.ClaimMonthlyReward((res) =>
        {
            isSuccess = res != null && res.status == "success";
            if (!isSuccess) Debug.LogError("[Settlement] 月榜领奖确认失败");
            isRequestDone = true;
        });
        yield return new WaitUntil(() => isRequestDone);

        if (isSuccess)
        {
            yield return StartCoroutine(GrantLocalRewardsRoutine()); // 服务端点头后才本地发资产
            CloseAndFinish();                                        // 内部 InvalidateMonthlyCache + HidePanel
        }
        else
        {
            hasGrantedRewards = false;  // 解锁允许重试
            isAnimating = false;
            var btn = (currentStep == 2 ? step2ContinueButton : step1Button);
            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null) txt.text = MultilingualManager.Instance.GetString("NetworkRetry"); // 网络异常，点击重试
        }
    }
    
    // 本地发资产（照搬段位，rewards 已是 Dictionary）
    private IEnumerator GrantLocalRewardsRoutine()
    {
        if (_data.rewards == null || _data.rewards.Count == 0) yield break;

        int gold = 0;
        foreach (var kvp in _data.rewards)
        {
            if (kvp.Key == 0) gold += kvp.Value;
            else
            {
                LimitRewordType toolType = ConvertIdToToolType(kvp.Key);
                GameDataManager.Instance.UserData.UpdateTool(toolType, kvp.Value, "月榜结算");
            }
        }
        if (gold > 0)
        {
            yield return ShowHeaderSection();
            bool flyFinished = false;
            CustomFlyInManager.Instance.FlyInGold(step2ChestIcon, () =>
            {
                GameDataManager.Instance.UserData.UpdateGold(gold, true, true, "月榜结算");
                GameDataManager.Instance.CommitGameData();
                flyFinished = true;
            });
            yield return new WaitUntil(() => flyFinished);
        }
    }
    // 以下两个直接从 ZenSettlementScreen 原样复制过来
    private LimitRewordType ConvertIdToToolType(int id)
    {
        return id switch
        {
            3 => LimitRewordType.AutoComplete,
            2 => LimitRewordType.Tipstool,
            1 => LimitRewordType.Butterfly,
            4 => LimitRewordType.AutoComplete,
            16 => LimitRewordType.MonthlyGold,
            17 => LimitRewordType.MonthlySilver,
            18 => LimitRewordType.MonthlyBronze,
            
            _ => default
        };
    }

    private IEnumerator ShowHeaderSection()
    {
        if (!SystemManager.Instance.PanelIsShowing(PanelType.HeaderSection))
            SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        yield return new WaitForSeconds(0.1f);
        EventDispatcher.instance.TriggerUpdateLayerCoin(true, false, false);
    }
    
    private void SetRankIcon(int rank)
    {
        Image chest = step2ChestIcon.GetComponent<Image>();
        chest.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("give_box_" + rank);
        chest.SetNativeSize();
    }
}
