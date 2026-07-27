using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Sequence = DG.Tweening.Sequence;

public class ZenRewardData
{
    public int Id;     // 奖励项id
    public int State;  // 所属段位
    public int Rank;   // 排名
    public Dictionary<int, int> rewards;   // {0,100}{1,20} 奖励列表, 键是奖品id,值是奖品数量
}
public class ZenSettlementScreen : UIWindow
{
    [Header("步骤1, 展示段位")]
    [SerializeField] private CanvasGroup step1CanvasGroup;
    [SerializeField] private Text step1TitleText;
    [SerializeField] private Text step1RankDescText;
    [SerializeField] private Image step1LotusImage;
    [SerializeField] private Text step1LevelTagText;
    [SerializeField] private Text step1ContinueText;

    [Header("步骤2, 展示奖励")]
    [SerializeField] private CanvasGroup step2CanvasGroup;
    [SerializeField] private Transform rewardChestIcon;  // 🌟 动画目标1：顶部的宝箱/金币图标
    [SerializeField] private Text step2CoinText;
    [Header("奖励生成区域")]
    [SerializeField] private Transform rewardsContainer; // 🌟 动画目标2：下方的奖励列表容器
    [SerializeField] private GameObject rewardTemplate;  // 🌟 将容器里的第一个子物体拖给这个变量！
    
    [Header("交互控件")]
    [SerializeField] private Button nextBtn;
    
    // 缓存当前需要发放的奖励
    private Dictionary<int, int> currentRewards;
    private int currentRank;
    private string targetLevelName;
    private string currentSettlementType; // 🌟 缓存当前的结算类型(up, down, keep)
    
    private bool hasGrantedRewards = false; // 防连点保护
    private bool isAnimating = false;
    private int currentStep = 1;
    // 👇 新增一个变量缓存旧段位
    private string cachedOldLevelCode;
    
    // Start is called before the first frame update
    void Start()
    {
        nextBtn.AddVibraClickAction(OnNextButtonClicked);
        step1CanvasGroup.GetComponent<Button>().AddVibraClickAction(OnNextButtonClicked);
        // 初始隐藏模板
        if (rewardTemplate != null) rewardTemplate.SetActive(false);
        
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        
        AudioManager.Instance.PlaySoundEffect("ShowUI");
    
        step1CanvasGroup.gameObject.SetActive(false);
        step2CanvasGroup.gameObject.SetActive(false);
        isAnimating = false;
        hasGrantedRewards = false;
    }

    /// <summary>
    /// 外部调用：打开结算界面并传入奖励数据, 
    /// </summary>
    public void ShowSettlement(int rank, Dictionary<int, int> rewards, string oldLevel, string nextLevel, string settlementType)
    {
        currentRank = rank;
        currentRewards = rewards;
        cachedOldLevelCode = oldLevel; // 缓存传进来的旧段位 (比如 "ZenState01")
        targetLevelName = nextLevel; // 缓存传进来的新段位名称
        currentSettlementType = settlementType; // 记录结算状态
        currentStep = 1;
        StartCoroutine(PlayStep1Anim());
        
    }
    // ==========================================
    // 🎬 动画序列 2：过渡淡出 -> 升段提示 -> 新段位徽章
    // ==========================================
    private IEnumerator PlayStep1Anim()
    {
        isAnimating = true; // 上锁
        step1CanvasGroup.gameObject.SetActive(true);
        step1CanvasGroup.alpha = 0;
        step1CanvasGroup.DOFade(1, 0.3f); 
        
        step1ContinueText.text = MultilingualManager.Instance.GetString("ClickContinue");
        step1TitleText.text = MultilingualManager.Instance.GetString("ZenRankingEnd");
        // 获取多语言的旧段位名称和新段位名称
        string oldLevelName = MultilingualManager.Instance.GetString(cachedOldLevelCode) ?? cachedOldLevelCode;
        string newLevelName = MultilingualManager.Instance.GetString(targetLevelName) ?? targetLevelName;
        
        // 👇=== 2. 核心文案逻辑：判断是否有排名 ===👇
        if (currentRank > 0)
        {
            // 有排名的正常情况
            string desc = "";
            if (currentSettlementType == "up") desc = MultilingualManager.Instance.GetString("ZenRise");
            else if (currentSettlementType == "down") desc = MultilingualManager.Instance.GetString("ZenDecline");
            else desc = MultilingualManager.Instance.GetString("ZenHold");
            
            // 格式化输出：例如 "你在【枯淡界】排名第5，成功晋级！"
            step1RankDescText.text = string.Format(desc, currentRank, newLevelName);
        }
        else
        {
            // 没有排名的缺席/降级情况 (请确保在多语言表中配置 "ZenUnranked" 字段)
            // 中文配表参考："您上期在【{0}】中未上榜，段位下降！"
            string unrankedDesc = MultilingualManager.Instance.GetString("ZenDecline2");
            if (unrankedDesc == "ZenDecline2")
            {
                unrankedDesc = "您上期在【{0}】中未上榜，段位下降至【{1}】！";
            }
            step1RankDescText.text = string.Format(unrankedDesc, oldLevelName, newLevelName);
        }
        
        step1LevelTagText.text = newLevelName;
        string zenLevel = UIUtilities.ExtractNumber(GameDataManager.Instance.UserData.Zenlevel);
        step1LotusImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("lotus_p"+zenLevel);
        
        // 初始状态：升段提示 和 实际段位 都隐藏
        // 开始编排步骤2动画
        Sequence seq = DOTween.Sequence();
        // 弹出 "跃升境界!" 提示字样
        seq.Append(step1RankDescText.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack));
        //   砸出真实的新段位徽章/文本
        seq.Append(step1LevelTagText.transform.parent.DOScale(1f, 0.5f).SetEase(Ease.OutBack));
        seq.AppendInterval(0.8f);
        yield return seq.WaitForCompletion();
        isAnimating = false; // 解锁，允许玩家点继续以关闭界面
    }

    // ==========================================
    // 🎬 动画序列 1：宝箱先出 -> 列表弹出
    // ==========================================
    private IEnumerator PlayStep2Anim()
    {
        isAnimating = true; // 上锁防点击
        bool hasRewards = currentRewards != null && currentRewards.Count > 0;
        if (!hasRewards)
        {
            OnClose();
            yield break;
        }
        hasGrantedRewards = true;
        step1CanvasGroup.DOFade(0, 0.3f); // 淡出步骤1
        yield return new WaitForSeconds(0.3f);
        step1CanvasGroup.gameObject.SetActive(false);
        step2CanvasGroup.gameObject.SetActive(true);
        nextBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ADPopReceive");
        // 1. 设置宝箱ICON数据
        SetRankIcon(currentRank);
        bool onlyHasGold = currentRewards.Count == 1 && currentRewards.ContainsKey(0);
        if (onlyHasGold)
        {
            step2CoinText.text = "× " +currentRewards[0].ToString();
            step2CoinText.gameObject.SetActive(true);
            rewardsContainer.gameObject.SetActive(false);
        }
        else
        {
            rewardsContainer.gameObject.SetActive(true);
            step2CoinText.gameObject.SetActive(false);
            // 2. 清理并生成新奖励列表
            foreach (Transform child in rewardsContainer)
            {
                if (child.gameObject != rewardTemplate) Destroy(child.gameObject);
            }
            foreach (var kvp in currentRewards)
            {
                GameObject itemObj = Instantiate(rewardTemplate, rewardsContainer);
                itemObj.SetActive(true);
                Image icon = itemObj.transform.GetChild(0).GetComponent<Image>();
                icon.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("give_"+kvp.Key);
                icon.SetNativeSize();
                itemObj.GetComponentInChildren<Text>().text = "× " + kvp.Value;
            }
        }
        step2CanvasGroup.alpha = 1;
        currentStep = 2; // 正式进入步骤2
        
        // 初始状态：把宝箱和列表都缩小到看不见
        // rewardChestIcon.localScale = Vector3.zero;
        rewardsContainer.localScale = Vector3.zero;
        // 使用 Sequence 编排动画
        Sequence seq = DOTween.Sequence();
        // 1. 宝箱“咚”地弹出来 (OutBack带一点回弹效果非常生动)
        seq.Append(rewardChestIcon.DOScale(1f, 0.4f).SetEase(Ease.OutBack));
        // 2. 停顿 0.15 秒，营造期待感
        seq.AppendInterval(0.15f);
        // 3. 奖励列表整体弹出来
        seq.Append(rewardsContainer.DOScale(1f, 0.4f).SetEase(Ease.OutBack));
        // 等待整个序列播完
        yield return seq.WaitForCompletion();
        isAnimating = false; // 解锁，允许玩家点继续
    }
   
    // ==========================================
    // 👆 按钮点击统筹
    // ==========================================
    private void OnNextButtonClicked()
    {
        if (isAnimating) return; // 动画期间绝对禁止点击！

        if (currentStep == 1)
        {
            bool hasRewards = currentRewards != null && currentRewards.Count > 0;
            if (hasRewards)
            {
                // 有奖励，进入展示奖励宝箱阶段
                StartCoroutine(PlayStep2Anim());
            }
            else
            {
                // 没奖励（比如未上榜降级），跳过步骤2，直接执行发放并关闭
                StartCoroutine(GrantAndCloseRoutine());
            }
        }
        else if (currentStep == 2)
        {
            // 在步骤 2 等待完毕时点击，关闭界面
            StartCoroutine(GrantAndCloseRoutine());
        }
    }
    // 🌟 专用于不展示第二阶段时，等待金币飞完直接关闭界面
    private IEnumerator GrantAndCloseRoutine()
    {
        isAnimating = true; // 锁定点击
        bool isRequestDone = false;
        bool isSuccess = false;
        yield return APIGateway.Instance.LeaderboardApi.ClaimZenReward((res) =>
        {
            if (res != null && res.status == "success")
            {
                // 服务器确认发放成功，拿到最新的真实段位
                GameDataManager.Instance.UserData.Zenlevel = res.new_level;
                
                // 🌟 核心状态切换：踢出榜单，准备重新匹配
                GameDataManager.Instance.UserData.isJoinedZenRank = false; 
                ZenRankManager.Instance.ClearRankCache();
                isSuccess = true;
            }
            else
            {
                Debug.LogError("服务器领奖确认失败！");
            }
            isRequestDone = true;
        });
        // 死等网络请求返回
        yield return new WaitUntil(() => isRequestDone);

        if (isSuccess)
        {
            // 2. 🌟 服务器点头了，本地才真正开始发资产、飞金币！
            yield return StartCoroutine(GrantLocalRewardsRoutine());
            
            // 3. 关闭结算面板
            OnClose();
        }
        else
        {
            // 如果请求失败（比如断网），解锁界面让玩家重试
            isAnimating = false;
            if (nextBtn != null) 
                nextBtn.GetComponentInChildren<Text>().text = "网络异常，点击重试";
        }
    }
    private IEnumerator ShowHeaderSection()
    {
        if (!SystemManager.Instance.PanelIsShowing(PanelType.HeaderSection))
        {
            SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        }
        yield return new WaitForSeconds(0.1f);
        EventDispatcher.instance.TriggerUpdateLayerCoin(true,false,false);
    }
    private IEnumerator GrantLocalRewardsRoutine()
    {
        if (currentRewards != null && currentRewards.Count > 0)
        {
            
            int gold = 0;
            foreach (var kvp in currentRewards)
            {
                int itemId = kvp.Key;
                int count = kvp.Value;

                if (itemId == 0) gold += count;
                else
                {
                    LimitRewordType toolType = ConvertIdToToolType(itemId);
                    GameDataManager.Instance.UserData.UpdateTool(toolType, count, "排行榜赛季结算");
                }
            }
            if (gold > 0)
            {
                yield return ShowHeaderSection();
                bool flyFinished = false; // 标记动画是否结束

                CustomFlyInManager.Instance.FlyInGold(rewardChestIcon, () =>
                {
                    GameDataManager.Instance.UserData.UpdateGold(gold, true, true, "排行榜赛季结算");
                    GameDataManager.Instance.CommitGameData();
                    flyFinished = true; // 动画播完，标记完成！
                });

                // 🌟 死等！直到 flyFinished 变成 true 才会往下走
                yield return new WaitUntil(() => flyFinished);
            }
        }
    }
    
    private void SetRankIcon(int rank)
    {
        switch (rank)
        {
            case 1: rewardChestIcon.GetComponent<Image>().sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("give_coin_1"); break;
            case 2: rewardChestIcon.GetComponent<Image>().sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("give_coin_2"); break;
            case 3: rewardChestIcon.GetComponent<Image>().sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("give_coin_3"); break;
            default: rewardChestIcon.GetComponent<Image>().sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("give_coin_0"); break;
        }
        rewardChestIcon.GetComponent<Image>().SetNativeSize();
    }
    
    /// <summary>
    /// 辅助方法：将配置表里的数字 ID 转换为对应的道具枚举
    /// </summary>
    private LimitRewordType ConvertIdToToolType(int id)
    {
        return id switch
        {
            3 => LimitRewordType.Resettool,     // 重置
            2 => LimitRewordType.Tipstool,      // 提示
            1 => LimitRewordType.Butterfly,     // 蝴蝶
            4 => LimitRewordType.AutoComplete,  // 自动拼字
            _ => default
        };
    }
    private Sprite SelectItemIcon(int itemId)
    {
        Sprite sprite = null;
        switch (itemId)
        {
            case 0:
                sprite =  AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("gold");
                break;
            case 1:
                sprite =  AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("UI_Icon_Butterfly");
                break;
            case 2:
                sprite =  AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Tips");
                break;
        }

        return sprite;
    }

    private void OnClose()
    {
        isAnimating = false;
        SystemManager.Instance.HidePanel(PanelType.ZenSettlementScreen);
        // SystemManager.Instance.HidePanel(PanelType.ZenSettlementScreen, true, () =>
        // {
        //     SystemManager.Instance.ShowPanel(PanelType.ZenRankStartScreen);
        // });
    }
    protected override void OnDisable()
    {
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,true);
        if (SystemManager.Instance.PanelIsShowing(PanelType.ZenRankScreen))
        {
            SystemManager.Instance.HidePanel(PanelType.HeaderSection);
        }
     
        base.OnDisable();
    }
}
