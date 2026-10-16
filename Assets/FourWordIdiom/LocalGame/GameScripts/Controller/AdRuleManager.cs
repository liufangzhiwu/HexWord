using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Middleware; // 你的命名空间

/// <summary>
/// 广告规则配置行（对应 adv_general_config 表的一行）。
/// 各字段含义（需求文档）：
///   T1: 秒时间内展示过激励视频的情况下不播插屏
///   T2: 游戏时间 T2 秒不足时，不展示插屏和 Banner
///   T3: 付费以后，T3 内不播插屏
///   T4: 用户从后台切回游戏，T4 秒以内绝对不弹广告
///   T5: 所有插屏，共享时间间隔 T5
///   A1: 疲劳度加 A1 分：播一个插屏广告
///   A2: 疲劳度加 A2 分：看完一个激励视频
///   A3: 疲劳度上限
///   L1: 新用户保护，疲劳度 0-30 分时的额外惩罚 CD
///   L2: 过渡期，疲劳度 31-60 分时的额外惩罚 CD
///   L3: 成熟期，疲劳度 60+ 分时的额外惩罚 CD
///   D:  每日第一关通关后出插屏的概率（0~1）
///   InterAdsSwitch: 插屏开关（0=关闭插屏和 Banner，1=开启）
///   UserLayer: 对应 ShopLayerConfig.InterAdsGroup
/// </summary>
[Serializable]
public class AdGeneralConfigRow
{
    public string UserLayer;
    public float T1;
    public float T2;
    public float T3;
    public float T4;
    public float T5;
    public int A1;
    public int A2;
    public int A3;
    public float L1;
    public float L2;
    public float L3;
    public float D;
    public int InterAdsSwitch;
}

public class AdRuleManager : MonoBehaviour
{
    public static AdRuleManager Instance { get; private set; }

    /// <summary>所有分层配置：UserLayer → 配置行</summary>
    private Dictionary<string, AdGeneralConfigRow> _adConfigRows = new Dictionary<string, AdGeneralConfigRow>();

    /// <summary>默认分层 Key（adv 表中"标准"行对应的 UserLayer 值）</summary>
    private const string DefaultUserLayerKey = "1";

    /// <summary>内置默认配置（表缺失或读不到时的兜底，"标准"参数）</summary>
    private readonly AdGeneralConfigRow _defaultConfig = new AdGeneralConfigRow
    {
        UserLayer = DefaultUserLayerKey,
        T1 = 120f,
        T2 = 2400f,
        T3 = 86400f,
        T4 = 60f,
        T5 = 240f,
        A1 = 3,
        A2 = 2,
        A3 = 100,
        L1 = 60f,
        L2 = 30f,
        L3 = 0f,
        D = 0.30f,
        InterAdsSwitch = 1,
    };

    // ==========================================
    // 【赠礼系统配置】（硬编码，不走配表）
    // ==========================================
    private const int G6_FirstAdGiftGold = 50;
    private const int R1_DailyMaxRewardGift = 3;
    private const int R2_Gold_Step1 = 50;
    private const int R2_Gold_Step2 = 30;
    private const int R2_Gold_Step3 = 20;
    private const int I1_DailyMaxInterstitialGift = 5;
    private const int I2_InterstitialGiftGold = 10;
    private const int I3_InterstitialUnlockStage = 30;
    private const string GIFT_TIP_TEXT = "广告并不友好，但您的确帮到了我们。";

    // 运行时状态 (不需要存档的 Session 级数据)
    private DateTime _lastAppResumeTime = DateTime.MinValue;
    private bool _isAppInBackground = false;

    // ==========================================
    // 当前生效的配置（按 ShopManager 的 InterAdsGroup 从 _adConfigRows 中选）
    // ==========================================
    private AdGeneralConfigRow CurrentConfig
    {
        get
        {
            string key = DefaultUserLayerKey;

            // 1. 从 ShopManager 读取当前分层对应的 InterAdsGroup
            if (ShopManager.shopManager != null)
            {
                string interAdsGroup = ShopManager.shopManager.GetInterAdsGroup();
                if (!string.IsNullOrEmpty(interAdsGroup))
                    key = interAdsGroup;
            }

            // 2. 按 key 查配置行
            AdGeneralConfigRow row;
            if (_adConfigRows.TryGetValue(key, out row) && row != null)
                return row;

            // 3. 回退到"标准"行
            if (_adConfigRows.TryGetValue(DefaultUserLayerKey, out row) && row != null)
                return row;

            // 4. 再回退到内置默认值
            return _defaultConfig;
        }
    }

    // ==========================================
    // 便捷访问属性（供内部与其他模块调用）
    // ==========================================
    private float T1_RewardCD => CurrentConfig.T1;
    private float T2_MinPlayTime => CurrentConfig.T2;
    private float T3_PayProtect => CurrentConfig.T3;
    private float T4_ResumeProtect => CurrentConfig.T4;
    private float T5_BaseInterstitialCD => CurrentConfig.T5;
    private int A1_InterstitialFatigue => CurrentConfig.A1;
    private int A2_RewardFatigue => CurrentConfig.A2;
    private int A3_MaxFatigue => CurrentConfig.A3;
    private float L1_ExtraCD => CurrentConfig.L1;
    private float L2_ExtraCD => CurrentConfig.L2;
    private float L3_ExtraCD => CurrentConfig.L3;
    private float D1_FirstLevelAdProb => CurrentConfig.D * 100f;   // 存的是 0.30，转成 30%
    private int InterAdsSwitch => CurrentConfig.InterAdsSwitch;

    // ==========================================
    // 生命周期
    // ==========================================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
        }   
    }

    private void Start()
    {
        string csvData = null;
        bool isCsvDone = false;

// #if Unity_Release
//         StartCoroutine(APIGateway.Instance.GameConfigApi.GetGameConfig("adv_general_config",
//             onSuccess: (response) => { csvData = response.CsvString; isCsvDone = true; },
//             onError: (error) => { isCsvDone = true; Debug.Log("服务器拉取 广告 配置失败，准备兜底 " + error); }
//         ));
//         float timeout = 2f;
//         while (!isCsvDone && timeout > 0)
//         {
//             timeout -= Time.deltaTime;
//             yield return null;
//         }
// #endif

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
            Debug.LogError("[AdRule] Failed to load CSV data.");
        }
    }

    private void Update()
    {
        if (!_isAppInBackground)
        {
            GameDataManager.Instance.UserData.TotalPlayTimeSeconds += Time.deltaTime;
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        _isAppInBackground = !hasFocus;
        if (hasFocus)
        {
            _lastAppResumeTime = DateTime.Now;
        }
    }

    // ==========================================
    // CSV 加载（按 UserLayer 逐行读取）
    // ==========================================

    /// <summary>
    /// 加载 adv_general_config 表。
    /// 表结构：
    ///   - 字段名行（含 "T1" 和 "UserLayer"）：T1, T2, T3, T4, T5, A1, A2, A3, L1, L2, L3, D, InterAdsSwitch, UserLayer
    ///   - 数据行：每个分层一行，UserLayer 列的值为 "1"/"2"/"3"/"0"（对应 ShopLayerConfig.InterAdsGroup）
    /// </summary>
    public void LoadConfigFromCSV(string csvText)
    {
        if (string.IsNullOrEmpty(csvText))
        {
            Debug.LogError("[AdRule] CSV 为空");
            return;
        }

        string[] lines = csvText.Replace("\r", "").Split('\n');
        if (lines.Length < 2)
        {
            Debug.LogError("[AdRule] CSV 行数不足");
            return;
        }

        // 定位字段名行：含 "T1" 且含 "UserLayer"
        int headerRow = -1;
        for (int i = 0; i < Math.Min(lines.Length, 4); i++)
        {
            if (lines[i].Contains("T1") && lines[i].Contains("UserLayer"))
            {
                headerRow = i;
                break;
            }
        }

        if (headerRow < 0)
        {
            Debug.LogError("[AdRule] 未找到字段名行（需同时含 T1 和 UserLayer）");
            return;
        }

        // 构建列名 → 列索引映射
        string[] headers = lines[headerRow].Split(',');
        Dictionary<string, int> colMap = new Dictionary<string, int>();
        for (int i = 0; i < headers.Length; i++)
        {
            string h = headers[i].Trim();
            if (!string.IsNullOrEmpty(h) && !colMap.ContainsKey(h))
                colMap[h] = i;
        }

        if (!colMap.ContainsKey("UserLayer"))
        {
            Debug.LogError("[AdRule] 字段名行缺少 UserLayer 列");
            return;
        }

        _adConfigRows.Clear();

        for (int i = headerRow + 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] vals = line.Split(',');
            if (vals.Length < 14) continue;

            // UserLayer 值（例如 "1"/"2"/"3"/"0"）作为字典 key
            string userLayerKey = GetCell(vals, colMap, "UserLayer");
            if (string.IsNullOrEmpty(userLayerKey)) continue;

            AdGeneralConfigRow row = new AdGeneralConfigRow
            {
                UserLayer = userLayerKey,
                T1 = ParseFloat(vals, colMap, "T1"),
                T2 = ParseFloat(vals, colMap, "T2"),
                T3 = ParseFloat(vals, colMap, "T3"),
                T4 = ParseFloat(vals, colMap, "T4"),
                T5 = ParseFloat(vals, colMap, "T5"),
                A1 = (int)ParseFloat(vals, colMap, "A1"),
                A2 = (int)ParseFloat(vals, colMap, "A2"),
                A3 = (int)ParseFloat(vals, colMap, "A3"),
                L1 = ParseFloat(vals, colMap, "L1"),
                L2 = ParseFloat(vals, colMap, "L2"),
                L3 = ParseFloat(vals, colMap, "L3"),
                D = ParsePercent(vals, colMap, "D"),
                InterAdsSwitch = (int)ParseFloat(vals, colMap, "InterAdsSwitch"),
            };

            _adConfigRows[userLayerKey] = row;

            Debug.Log($"[AdRule] 加载配置行: UserLayer={userLayerKey}, " +
                      $"T1={row.T1}, T2={row.T2}, T3={row.T3}, T4={row.T4}, T5={row.T5}, " +
                      $"A1={row.A1}, A2={row.A2}, A3={row.A3}, " +
                      $"L1={row.L1}, L2={row.L2}, L3={row.L3}, D={row.D:F2}, InterAdsSwitch={row.InterAdsSwitch}");
        }

        Debug.Log($"[AdRule] 广告规则配表加载完成, 共 {_adConfigRows.Count} 个分层: [{string.Join(",", _adConfigRows.Keys)}]");
    }

    private string GetCell(string[] vals, Dictionary<string, int> colMap, string colName)
    {
        int idx;
        if (!colMap.TryGetValue(colName, out idx)) return "";
        if (idx < 0 || idx >= vals.Length) return "";
        return (vals[idx] ?? "").Trim();
    }

    private float ParseFloat(string[] vals, Dictionary<string, int> colMap, string colName)
    {
        string s = GetCell(vals, colMap, colName);
        float v;
        if (float.TryParse(s, out v)) return v;
        return 0f;
    }

    /// <summary>解析百分比列（如 "30%" → 0.30；"0" → 0；"0.5" → 0.5）</summary>
    private float ParsePercent(string[] vals, Dictionary<string, int> colMap, string colName)
    {
        string s = GetCell(vals, colMap, colName);
        if (string.IsNullOrEmpty(s)) return 0f;

        bool hasPercentSign = s.Contains("%");
        s = s.Replace("%", "").Trim();

        float v;
        if (!float.TryParse(s, out v)) return 0f;

        // "30%" → 0.30；"0.3" → 0.3；"0" → 0
        if (hasPercentSign) return v / 100f;
        // 无百分号且值 > 1 时也视为百分数（例如 "30" 表示 30%）
        if (v > 1f) return v / 100f;
        return v;
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
            Debug.Log($"[AdRule] 插屏拦截掉了 (当前分层配置 InterAdsSwitch={InterAdsSwitch})");
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

            onComplete?.Invoke(success);

            if (success)
            {
                //TryShowInterstitialGift();
            }
        });
    }

    /// <summary>展示 Banner</summary>
    public void TryShowBanner()
    {
        if (!CanShowBanner())
        {
            Debug.Log("[AdRule] Banner 拦截掉了");
            return;
        }
        Game.self.Ads.ShowBanner();
    }

    /// <summary>关闭 Banner</summary>
    public void HideBannerAd()
    {
        Game.self.Ads.HideBanner();
    }

    /// <summary>展示激励视频</summary>
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

    /// <summary>展示提示灯道具激励视频</summary>
    public void TryShowTipToolRewardVideo(Define.AdKey adKey, Action<bool> onComplete)
    {
        Game.self.Ads.ShowTipToolReward(adKey, (success) =>
        {
            if (success)
            {
                ReportAdShown(Define.AdType.Reward);
            }

            onComplete?.Invoke(success);
        });
    }


    // ==========================================
    // 拦截审核
    // ==========================================

    /// <summary>
    /// 当前是否处于"去广告"生效期间。
    /// </summary>
    private bool IsRemoveAdsActive()
    {
        if (ShopManager.shopManager == null) return false;
        return ShopManager.shopManager.IsRemoveAdsOwned() || ShopManager.shopManager.IsRemoveAdsActive();
    }

    /// <summary>
    /// 拦截审核：当前是否允许播放插屏？
    /// </summary>
    public bool CanShowInterstitial()
    {
        var userData = GameDataManager.Instance.UserData;
        DateTime now = DateTime.Now;

        string layerKey = "1";
        if (ShopManager.shopManager != null)
            layerKey = ShopManager.shopManager.GetInterAdsGroup();

        Debug.Log($"[AdRule] 进入插屏拦截逻辑 | InterAdsGroupId={layerKey}, InterAdsSwitch={InterAdsSwitch}, " +
                  $"T1={T1_RewardCD}, T2={T2_MinPlayTime}, T3={T3_PayProtect}, T4={T4_ResumeProtect}, T5={T5_BaseInterstitialCD}, " +
                  $"L1={L1_ExtraCD}, L2={L2_ExtraCD}, L3={L3_ExtraCD}, D={D1_FirstLevelAdProb:F0}%");

        // 【★ 开关拦截】当前分层配置 InterAdsSwitch=0（如 IAP14） → 完全关闭插屏和 Banner
        if (InterAdsSwitch == 0)
        {
            Debug.Log($"[AdRule] 被拦截(开关)：当前分层 InterAdsSwitch=0，关闭插屏与 Banner");
            return false;
        }

        // 【★ 去广告拦截】去广告生效期间不播插屏
        if (IsRemoveAdsActive())
        {
            bool owned = ShopManager.shopManager != null && ShopManager.shopManager.IsRemoveAdsOwned();
            double remain = ShopManager.shopManager != null ? ShopManager.shopManager.GetRemoveAdsRemainSeconds() : 0;
            Debug.Log($"[AdRule] 被拦截(去广告)：玩家已拥有永久去广告={owned}, 限时去广告剩余={remain:F0}s");
            return false;
        }

        // 【D规则】每日首关插屏概率保护
        if (userData.dayPassStageCount == 0)
        {
            if (!userData.isDayFirstLevelAdChecked)
            {
                userData.isDayFirstLevelAdChecked = true;
                float rand = UnityEngine.Random.Range(0f, 100f);
                userData.isDayFirstLevelAdAllowed = rand < D1_FirstLevelAdProb;

                Debug.Log($"[AdRule] (D规则) 每日首关插屏判定：配置概率 {D1_FirstLevelAdProb:F0}%, 随机点数 {rand:F1}, 是否允许: {userData.isDayFirstLevelAdAllowed}");

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
            Debug.Log($"[AdRule] 被拦截(G2)：累计时长 {userData.TotalPlayTimeSeconds:F0}s 不足 {T2_MinPlayTime}s");
            return false;
        }

        // 【G3】付费保护
        if (userData.LastPayTimeTicks > 0)
        {
            DateTime lastPayTim = new DateTime(userData.LastPayTimeTicks);
            TimeSpan paySpan = now.Subtract(lastPayTim);
            if (paySpan.TotalSeconds < T3_PayProtect)
            {
                Debug.Log($"[AdRule] 被拦截(G3)：处于付费保护期，保护期 {T3_PayProtect}s，上次付费 {lastPayTim}");
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
                Debug.Log($"[AdRule] 被拦截(G5)：冷却中。需 {totalCD}s（基础{T5_BaseInterstitialCD}+额外{extraCD}），当前仅过 {interstitialSpan.TotalSeconds:F0}s");
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
        // 【★ 开关拦截】当前分层关闭广告
        if (InterAdsSwitch == 0)
        {
            Debug.Log($"[AdRule] Banner 被拦截(开关)：当前分层 InterAdsSwitch=0");
            return false;
        }

        // 【★ 去广告拦截】
        if (IsRemoveAdsActive())
        {
            bool owned = ShopManager.shopManager != null && ShopManager.shopManager.IsRemoveAdsOwned();
            double remain = ShopManager.shopManager != null ? ShopManager.shopManager.GetRemoveAdsRemainSeconds() : 0;
            Debug.Log($"[AdRule] Banner 被拦截(去广告)：玩家已拥有永久去广告={owned}, 限时去广告剩余={remain:F0}s");
            return false;
        }

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
            Debug.Log($"[AdRule] 记录插屏，当前疲劳度：{userData.AdFatigueScore} (上限{A3_MaxFatigue})");
        }
        else if (type == Define.AdType.Reward)
        {
            userData.AdFatigueScore = Mathf.Min(A3_MaxFatigue, userData.AdFatigueScore + A2_RewardFatigue);
            userData.LastRewardAdTimeTicks = DateTime.Now.Ticks;
            Debug.Log($"[AdRule] 记录激励视频，当前疲劳度：{userData.AdFatigueScore} (上限{A3_MaxFatigue})");
        }

        GameDataManager.Instance.CommitGameData();
    }

    // ==========================================
    // 赠礼系统（保持原逻辑）
    // ==========================================

    public void TryShowRewardVideoGift(Action onGiftClosed = null)
    {
        var userData = GameDataManager.Instance.UserData;

        if (!userData.HasShownFirstAdGift)
        {
            userData.HasShownFirstAdGift = true;
            GameDataManager.Instance.CommitGameData();
            ShowGiftWindow(true, G6_FirstAdGiftGold, onGiftClosed);
            return;
        }

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
    }

    public void TryShowInterstitialGift(Action onGiftClosed = null)
    {
        var userData = GameDataManager.Instance.UserData;

        if (!userData.HasShownFirstAdGift)
        {
            userData.HasShownFirstAdGift = true;
            GameDataManager.Instance.CommitGameData();
            ShowGiftWindow(true, G6_FirstAdGiftGold, onGiftClosed);
            return;
        }

        if (userData.CurrentChessStage < I3_InterstitialUnlockStage)
        {
            onGiftClosed?.Invoke();
            return;
        }

        if (userData.DayInterstitialGiftCount >= I1_DailyMaxInterstitialGift)
        {
            onGiftClosed?.Invoke();
            return;
        }

        userData.DayInterstitialGiftCount++;
        GameDataManager.Instance.CommitGameData();
    }

    private void ShowGiftWindow(bool firstAds, int gold, Action onGiftClosed = null)
    {
        var window = SystemManager.Instance.ShowPanel(PanelType.AdsAwardScreen).GetComponent<AdsAwardScreen>();
        window.SetContent(firstAds, gold, GIFT_TIP_TEXT, () =>
        {
            GameDataManager.Instance.UserData.Gold += gold;
            GameDataManager.Instance.CommitGameData();
            Debug.Log($"[AdRule] 赠礼发放：+{gold} 金币");
            onGiftClosed?.Invoke();
        });
    }
}