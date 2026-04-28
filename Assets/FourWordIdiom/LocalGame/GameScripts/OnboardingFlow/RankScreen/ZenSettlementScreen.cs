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
    [Header("步骤1, 展示奖励")]
    [SerializeField] private CanvasGroup step1CanvasGroup;
    [SerializeField] private Transform rewardChestIcon;  // 🌟 动画目标1：顶部的宝箱/金币图标
    [SerializeField] private Text step1CoinText;
    [Header("奖励生成区域")]
    [SerializeField] private Transform rewardsContainer; // 🌟 动画目标2：下方的奖励列表容器
    [SerializeField] private GameObject rewardTemplate;  // 🌟 将容器里的第一个子物体拖给这个变量！
    
    [Header("步骤2, 展示段位")]
    [SerializeField] private CanvasGroup step2CanvasGroup;
    [SerializeField] private Transform tierChangeTitle;  // 🌟 动画目标3："跃升境界" 或 "段位下滑" 的提示图/字
    [SerializeField] private Transform tierResultContent;// 🌟 动画目标4：实际展示新段位徽章/名字的容器
    [SerializeField] private Text levelText;
    
    [Header("交互控件")]
    [SerializeField] private Button nextBtn;
    
    // 缓存当前需要发放的奖励
    private Dictionary<int, int> currentRewards;
    private string targetLevelName;
    private string currentSettlementType; // 🌟 缓存当前的结算类型(up, down, keep)
    
    private bool hasGrantedRewards = false; // 防连点保护
    private bool isAnimating = false;
    private int currentStep = 1;
    // Start is called before the first frame update
    void Start()
    {
        nextBtn.AddClickAction(OnNextButtonClicked);
        // 初始隐藏模板
        if (rewardTemplate != null) rewardTemplate.SetActive(false);
        
    }

    protected override void OnEnable()
    {
        base.OnEnable();
    
        step1CanvasGroup.gameObject.SetActive(false);
        step2CanvasGroup.gameObject.SetActive(false);
        step1CoinText.gameObject.SetActive(false);
        isAnimating = false;
        hasGrantedRewards = false;
        StartCoroutine(ShowHeaderSection());
    }

    private IEnumerator ShowHeaderSection()
    {
        if (!SystemManager.Instance.PanelIsShowing(PanelType.HeaderSection))
        {
            SystemManager.Instance.ShowPanel(PanelType.HeaderSection);
        }
        yield return new WaitForSeconds(0.8f);
        EventDispatcher.instance.TriggerUpdateLayerCoin(true,false);
    }

    /// <summary>
    /// 外部调用：打开结算界面并传入奖励数据, 
    /// </summary>
    public void ShowSettlement(int rank, Dictionary<int, int> rewards, string nextLevel, string settlementType)
    {
        currentRewards = rewards;
        targetLevelName = nextLevel; // 缓存传进来的新段位名称
        currentSettlementType = settlementType; // 记录结算状态
        bool hasRewards = rewards != null && rewards.Count > 0;
        
        if (hasRewards)
        {
            currentStep = 1;
            step1CanvasGroup.gameObject.SetActive(true);
            step1CanvasGroup.alpha = 1;
            
            // 1. 设置宝箱ICON数据
            SetRankIcon(rank);
            bool onlyHasGold = rewards.Count == 1 && rewards.ContainsKey(0);
            if (onlyHasGold)
            {
                step1CoinText.text = "× " +rewards[0].ToString();
                step1CoinText.gameObject.SetActive(true);
                rewardsContainer.gameObject.SetActive(false);
            }
            else
            {
                rewardsContainer.gameObject.SetActive(true);
                step1CoinText.gameObject.SetActive(false);
                // 2. 清理并生成新奖励列表
                foreach (Transform child in rewardsContainer)
                {
                    if (child.gameObject != rewardTemplate) Destroy(child.gameObject);
                }
                foreach (var kvp in rewards)
                {
                    GameObject itemObj = Instantiate(rewardTemplate, rewardsContainer);
                    itemObj.SetActive(true);
                    Image icon = itemObj.transform.GetChild(0).GetComponent<Image>();
                    icon.sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("give_"+kvp.Key);
                    icon.SetNativeSize();
                    itemObj.GetComponentInChildren<Text>().text = "× " + kvp.Value;
                }
            }
            // 🌟 数据设置完毕，开始播放步骤 1 炫酷动画！
            StartCoroutine(PlayStep1Anim());
        }
        else
        {
            // 无奖励的情况
            if (currentSettlementType == "up")
            {
                // 无奖励但升段了，跳过步骤1直接展示步骤2
                currentStep = 2;
                StartCoroutine(PlayStep2Anim());
            }
            else
            {
                // 无奖励，且没升段（保级/降级），这不该有弹窗，直接领奖关闭保护
                StartCoroutine(GrantAndCloseRoutine());
            }
        }
    }
    // ==========================================
    // 🎬 动画序列 1：宝箱先出 -> 列表弹出
    // ==========================================
    private IEnumerator PlayStep1Anim()
    {
        isAnimating = true; // 上锁防点击
        
        // 初始状态：把宝箱和列表都缩小到看不见
        // rewardChestIcon.localScale = Vector3.zero;
        rewardsContainer.localScale = Vector3.zero;

        // 使用 Sequence 编排动画
        Sequence seq = DOTween.Sequence();
        
        // 1. 宝箱“咚”地弹出来 (OutBack带一点回弹效果非常生动)
        // seq.Append(rewardChestIcon.DOScale(1f, 0.4f).SetEase(Ease.OutBack));
        
        // 2. 停顿 0.15 秒，营造期待感
        seq.AppendInterval(0.15f);
        
        // 3. 奖励列表整体弹出来
        seq.Append(rewardsContainer.DOScale(1f, 0.4f).SetEase(Ease.OutBack));

        // 等待整个序列播完
        yield return seq.WaitForCompletion();
        
        isAnimating = false; // 解锁，允许玩家点继续
    }
    // ==========================================
    // 🎬 动画序列 2：过渡淡出 -> 升段提示 -> 新段位徽章
    // ==========================================
    private IEnumerator PlayStep2Anim()
    {
        isAnimating = true; // 上锁

        // 阶段 1 过渡处理
        if (currentStep == 1) 
        {
            // GrantLocalRewards(); // 把奖励发进背包
            yield return StartCoroutine(GrantLocalRewardsRoutine());
            step1CanvasGroup.DOFade(0, 0.3f); // 淡出步骤1
            yield return new WaitForSeconds(0.3f);
            step1CanvasGroup.gameObject.SetActive(false);
            currentStep = 2; // 正式进入步骤2
        }

        // 步骤2 UI 初始化
        step2CanvasGroup.gameObject.SetActive(true);
        step2CanvasGroup.alpha = 1;
        
        // 文本赋值
        if (levelText != null) 
            levelText.text = MultilingualManager.Instance.GetString(targetLevelName) ?? targetLevelName;

        // 初始状态：升段提示 和 实际段位 都隐藏
        tierChangeTitle.gameObject.SetActive(true);
        tierResultContent.gameObject.SetActive(false);
        // tierChangeTitle.localScale = Vector3.zero;
        tierResultContent.localScale = Vector3.zero;

        // 开始编排步骤2动画
        Sequence seq = DOTween.Sequence();

        // 1. 弹出 "跃升境界!" 提示字样
        // seq.Append(tierChangeTitle.DOScale(1f, 0.4f).SetEase(Ease.OutBack));
        
        // 2. 停顿 0.8 秒，让玩家看清楚
        seq.AppendInterval(0.8f);
        
        // 3. 收回 "跃升境界!" 提示 (InBack 往里收缩)
        seq.Append(tierChangeTitle.DOScale(0f, 0.3f).SetEase(Ease.InBack));
        
        // 4. 回调：隐藏提示，显示段位容器
        seq.AppendCallback(() => {
            tierChangeTitle.gameObject.SetActive(false);
            tierResultContent.gameObject.SetActive(true);
        });
        
        // 5. 砸出真实的新段位徽章/文本
        seq.Append(tierResultContent.DOScale(1f, 0.5f).SetEase(Ease.OutBack));

        yield return seq.WaitForCompletion();

        isAnimating = false; // 解锁，允许玩家点继续以关闭界面
    }
    
    // ==========================================
    // 👆 按钮点击统筹
    // ==========================================
    private void OnNextButtonClicked()
    {
        if (isAnimating) return; // 动画期间绝对禁止点击！

        if (currentStep == 1)
        {
            // 🌟 核心修改：只有段位提升 (up) 才进入阶段 2，否则直接发奖关闭 
            if (currentSettlementType == "up")
            {
                //在步骤 1 等待完毕时点击，触发步骤 2 的动画
                StartCoroutine(PlayStep2Anim());
            }
            else
            {
                StartCoroutine(GrantAndCloseRoutine());
            }
        }
        else if (currentStep == 2)
        {
            // 在步骤 2 等待完毕时点击，关闭界面
            Close();
        }
    }
    // 🌟 专用于不展示第二阶段时，等待金币飞完直接关闭界面
    private IEnumerator GrantAndCloseRoutine()
    {
        isAnimating = true; // 锁定点击
        yield return StartCoroutine(GrantLocalRewardsRoutine());
        Close();
    }
    private IEnumerator GrantLocalRewardsRoutine()
    {
        if (hasGrantedRewards) yield break;
        hasGrantedRewards = true;

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
            else
            {
                // 如果没有金币只有道具，直接提交即可
                GameDataManager.Instance.CommitGameData();
            }
        }
    }
    
    private void SetRankIcon(int rank)
    {
        switch (rank)
        {
            case 1: rewardChestIcon.GetComponent<Image>().sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("give_coin_1"); break;
            case 2: rewardChestIcon.GetComponent<Image>().sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("give_coin_2"); break;
            case 3: rewardChestIcon.GetComponent<Image>().sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("give_coin_3"); break;
            default: rewardChestIcon.GetComponent<Image>().sprite = AssetBundleLoader.SharedInstance.GetSpriteFromAtlas("give_coin_0"); break;
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

    protected override void OnDisable()
    {
        EventDispatcher.instance.TriggerUpdateLayerCoin(true,true);
        if (SystemManager.Instance.PanelIsShowing(PanelType.ZenRankScreen))
        {
            SystemManager.Instance.HidePanel(PanelType.HeaderSection);
        }
     
        base.OnDisable();
    }
}
