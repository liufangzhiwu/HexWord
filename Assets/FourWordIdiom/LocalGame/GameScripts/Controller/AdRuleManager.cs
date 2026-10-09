using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Middleware; // 你的命名空间


public class AdRuleManager : MonoBehaviour
{
    public static AdRuleManager Instance { get; private set; }
    private Dictionary<string, float> _adConfigMap = new Dictionary<string, float>();

    // ==========================================
    // 配表数值 (实际开发中这些值可以通过 ConfigManager 读取 JSON/CSV)
    // ==========================================
    // 【T系列：时间限制规则（单位：秒）】
    private float T1_RewardCD => GetConfig("T1", 120f);            // 互斥期：看完激励视频后，多久之内绝对不弹插屏
    private float T2_MinPlayTime => GetConfig("T2", 2400f);        // 新手期：总时长不足多久不弹插屏/Banner
    private float T3_PayProtect => GetConfig("T3", 86400f);        // 免广期：付费后保护多久不弹插屏
    private float T4_ResumeProtect => GetConfig("T4", 20f);        // 切回期：从桌面切回后保护多久不弹插屏
    private float T5_BaseInterstitialCD => GetConfig("T5", 180f);  // 基础CD：两次插屏之间最小间隔

    // 【A系列：疲劳度增减规则（单位：分）】
    private int A1_InterstitialFatigue => (int)GetConfig("A1", 3f);
    private int A2_RewardFatigue => (int)GetConfig("A2", 2f);
    private int A3_MaxFatigue => (int)GetConfig("A3", 100f);

    // 【L系列：根据疲劳度额外惩罚的插屏CD时间（单位：秒）】
    private float L1_ExtraCD => GetConfig("L1", 60f); // 疲劳度 0~30 分
    private float L2_ExtraCD => GetConfig("L2", 30f); // 疲劳度 31~60 分
    private float L3_ExtraCD => GetConfig("L3", 0f);  // 疲劳度 >60 分

    // 【D系列：每日首关概率规则】
    private float D1_FirstLevelAdProb => GetConfig("D", 30f);

    // 运行时状态 (不需要存档的 Session 级数据)
    private DateTime _lastAppResumeTime = DateTime.MinValue;
    private bool _isAppInBackground = false;


    // ==========================================
    // 【赠礼系统配置】（硬编码，不走配表）
    // ==========================================
    // G6: 首个广告（激励视频 or 插屏）一次性感谢金
    private const int G6_FirstAdGiftGold = 50;

    // R系列：灯泡激励视频每日赠礼（每天前3次，金额阶梯）
    private const int R1_DailyMaxRewardGift = 3;
    private const int R2_Gold_Step1 = 50;
    private const int R2_Gold_Step2 = 30;
    private const int R2_Gold_Step3 = 20;

    // I系列：插屏每日赠礼（每天前5次，30关后解锁）
    private const int I1_DailyMaxInterstitialGift = 5;
    private const int I2_InterstitialGiftGold = 10;
    private const int I3_InterstitialUnlockStage = 30;

    // 通用提示语
    private const string GIFT_TIP_TEXT = "广告并不友好，但您的确帮到了我们。";


    private void Awake()
    {
        // 1. 标准单例防重复检查
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        //DontDestroyOnLoad(gameObject);

        // 2. 冷启动保护：应用刚启动时加上安全期
        _lastAppResumeTime = DateTime.Now;
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);
        string csvData = null;
        bool isCsvDone = false;
        
#if Unity_Release
        StartCoroutine(APIGateway.Instance.GameConfigApi.GetGameConfig("adv_general_config",
            onSuccess: (response) => { csvData = response.CsvString; isCsvDone = true; },
            onError: (error) => { isCsvDone = true; Debug.Log("服务器拉取 广告 配置失败，准备兜底 " + error); }
        ));
        float timeout = 2f;
        while (!isCsvDone && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
#endif

        if (string.IsNullOrEmpty(csvData))
        {
            TextAsset textAsset = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "adv_general_config");
            csvData = textAsset?.text;
        }
        if (!string.IsNullOrEmpty(csvData))
        {
            LoadConfigFromCSV(csvData);
        }
        else
        {
            Debug.LogError("Failed to load CSV data.");
        }
    }

    /// <summary>
    /// 获取配置核心方法：表里有配就用表里的，否则用 defaultValue 兜底。
    /// </summary>
    private float GetConfig(string key, float defaultValue)
    {
        if (_adConfigMap.TryGetValue(key, out float val))
            return val;
        return defaultValue;
    }

    /// <summary>
    /// 加载 CSV 配置
    /// </summary>
    public void LoadConfigFromCSV(string csvText)
    {
        if (string.IsNullOrEmpty(csvText)) return;

        string[] lines = csvText.Replace("\r", "").Split('\n');

        if (lines.Length >= 3)
        {
            string[] keys = lines[1].Split(',');
            string[] values = lines[2].Split(',');

            _adConfigMap.Clear();
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i].Trim();
                if (!string.IsNullOrEmpty(key) && i < values.Length)
                {
                    if (float.TryParse(values[i].Trim(), out float val))
                    {
                        _adConfigMap[key] = val;
                    }
                }
            }
            Debug.Log("[AdRule] 广告规则配表加载成功！共加载配置项：" + _adConfigMap.Count);
        }
    }

    private void Update()
    {
        // G2: 累计游戏时间 (切后台不计入)
        if (!_isAppInBackground)
        {
            GameDataManager.Instance.UserData.TotalPlayTimeSeconds += Time.deltaTime;
        }
    }

    // G4: 切后台与切回来的时间记录
    private void OnApplicationFocus(bool hasFocus)
    {
        _isAppInBackground = !hasFocus;
        if (hasFocus)
        {
            _lastAppResumeTime = DateTime.Now;
        }
    }

    // ==========================================
    // 广告展示入口
    // ==========================================

    /// <summary>
    /// 展示插屏广告
    /// 流程：拦截审核 → 播广告 → onComplete 立即回调（业务继续）→ 再尝试弹赠礼
    /// </summary>
    public void TryShowInterstitial(Action<bool> onComplete)
    {
        // 1. 拦截审核
        if (!CanShowInterstitial())
        {
            Debug.Log($"[AdRule] 插屏拦截掉了");
            onComplete?.Invoke(false);
            return;
        }

#if !UNITY_OPENHARMONY
        AnalyticMgr.InsetAdStart("关卡插屏");
#endif

        // 2. 播广告
        Game.self.Ads.ShowInterstitial((success) =>
        {
            if (success)
            {
                ReportAdShown(Define.AdType.Interstitial);
            }

            // ① 先让业务立即继续（广告播完的"立即执行"语义）
            onComplete?.Invoke(success);

            // ② 玩家关闭插屏后，才尝试弹赠礼（不阻塞业务）
            if (success)
            {
                TryShowInterstitialGift();
            }
        });
    }

    /// <summary>
    /// 展示 Banner
    /// </summary>
    public void TryShowBanner()
    {
        if (!CanShowBanner()) return;
        Game.self.Ads.ShowBanner();
    }

    /// <summary>
    /// 展示激励视频
    /// 注意：赠礼不由这里触发——因为需求是"玩家领奖后，灯泡道具发生作用并完成提示效果后"才弹，
    /// 所以由业务侧在道具生效完成后主动调用 TryShowRewardVideoGift()。
    /// </summary>
    public void TryShowRewardVideo(Define.AdKey adKey, Action<bool> onComplete)
    {
        Game.self.Ads.ShowReward(adKey, (success) =>
        {
            if (success)
            {
                ReportAdShown(Define.AdType.Reward);
            }

            onComplete?.Invoke(success);
        });
    }

    /// <summary>
    /// 展示提示灯道具激励视频
    /// </summary>
    public void TryShowTipToolRewardVideo(Define.AdKey adKey, Action<bool> onComplete)
    {
        Game.self.Ads.ShowTipToolReward(adKey, (success) =>
        {
            if (success)
            {
                ReportAdShown(Define.AdType.Reward);

                StartCoroutine(WaitShowRewardVideoGift());
            }

            onComplete?.Invoke(success);
        });
    }

    IEnumerator WaitShowRewardVideoGift()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        
        TryShowRewardVideoGift();
    }


    // ==========================================
    // 拦截审核
    // ==========================================

    /// <summary>
    /// 拦截审核：当前是否允许播放插屏？
    /// </summary>
    public bool CanShowInterstitial()
    {
        var userData = GameDataManager.Instance.UserData;
        DateTime now = DateTime.Now;

        Debug.Log($"[AdRule] 进入插屏拦截逻辑");

        // 【D规则】每日首关插屏概率保护
        if (userData.dayPassStageCount == 0)
        {
            if (!userData.isDayFirstLevelAdChecked)
            {
                userData.isDayFirstLevelAdChecked = true;
                float rand = UnityEngine.Random.Range(0f, 100f);
                userData.isDayFirstLevelAdAllowed = rand < D1_FirstLevelAdProb;

                Debug.Log($"[AdRule] (D规则) 每日首关插屏判定：配置概率 {D1_FirstLevelAdProb}%, 随机点数 {rand:F1}, 是否允许: {userData.isDayFirstLevelAdAllowed}");

                GameDataManager.Instance.CommitGameData();
            }

            if (!userData.isDayFirstLevelAdAllowed)
            {
                Debug.Log("[AdRule] 被拦截(D规则)：每日首关插屏概率未命中");
                return false;
            }
        }

        // 【G2】游戏时间不足 T2
        if (userData.TotalPlayTimeSeconds < T2_MinPlayTime)
        {
            Debug.Log($"[AdRule] 被拦截(G2)：累计时长不足 {T2_MinPlayTime}s");
            return false;
        }

        // 【G3】付费保护
        if (userData.LastPayTimeTicks > 0)
        {
            DateTime lastPayTim = new DateTime(userData.LastPayTimeTicks);
            TimeSpan paySpan = now.Subtract(lastPayTim);
            if (paySpan.TotalSeconds < T3_PayProtect)
            {
                Debug.Log("[AdRule] 被拦截(G3)：处于付费保护期 保护期时长：" + T3_PayProtect + "(秒) 上次付费时间:" + lastPayTim);
                return false;
            }
        }

        // 【G4】切回前台保护
        TimeSpan resumeSpan = now - _lastAppResumeTime;
        if (resumeSpan.TotalSeconds < T4_ResumeProtect)
        {
            Debug.Log($"[AdRule] 被拦截(G4)：刚切回前台不足 {T4_ResumeProtect}s");
            return false;
        }

        // 【G1】激励视频互斥
        if (userData.LastRewardAdTimeTicks > 0)
        {
            TimeSpan rewardSpan = now - new DateTime(userData.LastRewardAdTimeTicks);
            if (rewardSpan.TotalSeconds < T1_RewardCD)
            {
                Debug.Log($"[AdRule] 被拦截(G1)：距离上次激励视频不足 {T1_RewardCD}s");
                return false;
            }
        }

        // 【G5 + 疲劳度】插屏冷却
        float extraCD = 0f;
        if (userData.AdFatigueScore <= 30) extraCD = L1_ExtraCD;
        else if (userData.AdFatigueScore <= 60) extraCD = L2_ExtraCD;
        else extraCD = L3_ExtraCD;

        float totalCD = T5_BaseInterstitialCD + extraCD;

        if (userData.LastInterstitialTimeTicks > 0)
        {
            TimeSpan interstitialSpan = now - new DateTime(userData.LastInterstitialTimeTicks);
            if (interstitialSpan.TotalSeconds < totalCD)
            {
                Debug.Log($"[AdRule] 被拦截(G5)：冷却中。需 {totalCD}s，当前仅过 {interstitialSpan.TotalSeconds:F0}s");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 拦截审核：当前是否允许展示 Banner
    /// </summary>
    public bool CanShowBanner()
    {
        if (GameDataManager.Instance.UserData.TotalPlayTimeSeconds < T2_MinPlayTime)
            return false;

        return true;
    }


    // ==========================================
    // 广告账单上报
    // ==========================================
    public void ReportAdShown(Define.AdType type)
    {
        var userData = GameDataManager.Instance.UserData;

        if (type == Define.AdType.Interstitial)
        {
            userData.AdFatigueScore = Mathf.Min(A3_MaxFatigue, userData.AdFatigueScore + A1_InterstitialFatigue);
            userData.LastInterstitialTimeTicks = DateTime.Now.Ticks;
            Debug.Log($"[AdRule] 记录插屏，当前疲劳度：{userData.AdFatigueScore}");
        }
        else if (type == Define.AdType.Reward)
        {
            userData.AdFatigueScore = Mathf.Min(A3_MaxFatigue, userData.AdFatigueScore + A2_RewardFatigue);
            userData.LastRewardAdTimeTicks = DateTime.Now.Ticks;
            Debug.Log($"[AdRule] 记录激励视频，当前疲劳度：{userData.AdFatigueScore}");
        }

        GameDataManager.Instance.CommitGameData();
    }


    /// <summary>
    /// 灯泡激励视频赠礼
    /// 调用时机：玩家领奖 + 灯泡道具发生作用 + 完成提示效果之后，由业务侧调用。
    /// </summary>
    /// <param name="onGiftClosed">玩家关闭赠礼弹窗后的回调（可空）</param>
    public void TryShowRewardVideoGift(Action onGiftClosed = null)
    {
        var userData = GameDataManager.Instance.UserData;

        // 优先级 1：首个广告一次性 50 金币（激励视频 / 插屏谁先触发归谁）
        if (!userData.HasShownFirstAdGift)
        {
            userData.HasShownFirstAdGift = true;
            GameDataManager.Instance.CommitGameData();
            ShowGiftWindow(true,G6_FirstAdGiftGold, onGiftClosed);
            return;
        }

        // 优先级 2：每日前 3 次灯泡激励视频赠礼
        if (userData.DayRewardVideoGiftCount >= R1_DailyMaxRewardGift)
        {
            onGiftClosed?.Invoke();
            return;
        }

        int giftGold;
        switch (userData.DayRewardVideoGiftCount)
        {
            case 0: giftGold = R2_Gold_Step1; break;
            case 1: giftGold = R2_Gold_Step2; break;
            case 2: giftGold = R2_Gold_Step3; break;
            default: giftGold = 0; break;
        }
        userData.DayRewardVideoGiftCount++;
        GameDataManager.Instance.CommitGameData();

        ShowGiftWindow(false, giftGold, onGiftClosed);
    }

    /// <summary>
    /// 插屏赠礼
    /// 调用时机：由 TryShowInterstitial 内部在 onComplete 之后自动调用（玩家关闭插屏后）。
    /// </summary>
    /// <param name="onGiftClosed">玩家关闭赠礼弹窗后的回调（可空）</param>
    public void TryShowInterstitialGift(Action onGiftClosed = null)
    { 
        var userData = GameDataManager.Instance.UserData;

        // 优先级 1：首个广告一次性 50 金币
        if (!userData.HasShownFirstAdGift)
        {
            userData.HasShownFirstAdGift = true;
            GameDataManager.Instance.CommitGameData();
            ShowGiftWindow(true,G6_FirstAdGiftGold, onGiftClosed);
            return;
        }

        // 关卡门槛：填字玩法 30 关之后才触发
        // ⚠️ 字段名请按项目实际替换（这里假设是累计通关数 TotalPassStageCount）
        if (userData.CurrentChessStage < I3_InterstitialUnlockStage)
        {
            onGiftClosed?.Invoke();
            return;
        }

        // 每日前 5 次插屏
        if (userData.DayInterstitialGiftCount >= I1_DailyMaxInterstitialGift)
        {
            onGiftClosed?.Invoke();
            return;
        }

        userData.DayInterstitialGiftCount++;
        GameDataManager.Instance.CommitGameData();

        ShowGiftWindow(false,I2_InterstitialGiftGold, onGiftClosed);
    }

    /// <summary>
    /// 打开赠礼小弹窗
    /// </summary>
    /// <param name="gold">赠送金币数</param>
    /// <param name="onGiftClosed">玩家关闭赠礼弹窗后的回调（可空）</param>
    private void ShowGiftWindow(bool firstAds,int gold, Action onGiftClosed = null)
    {
        // ⚠️ UIManager.OpenWindow 请按项目实际 API 替换
        var window = SystemManager.Instance.ShowPanel(PanelType.AdsAwardScreen).GetComponent<AdsAwardScreen>();
        window.SetContent(firstAds,gold, GIFT_TIP_TEXT, () =>
        {
            // 发金币（关闭按钮和下方按钮走同一回调，效果一致）
            // ⚠️ 金币字段名请按项目实际替换
            GameDataManager.Instance.UserData.Gold += gold;
            GameDataManager.Instance.CommitGameData();
            Debug.Log($"[AdRule] 赠礼发放：+{gold} 金币");
            onGiftClosed?.Invoke();
        });
    }
}