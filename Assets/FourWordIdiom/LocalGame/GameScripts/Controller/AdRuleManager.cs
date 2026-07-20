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
    private float T1_RewardCD => GetConfig("T1", 120f);         // 互斥期：看完激励视频后，多久之内绝对不弹插屏 (防打扰)
    private float T2_MinPlayTime => GetConfig("T2", 2400f);     // 新手期：玩家总游戏时长不足多久时，绝对不弹插屏/Banner
    private float T3_PayProtect => GetConfig("T3", 86400f);     // 免广期：玩家氪金付费后，保护多久不弹插屏 (默认24小时)
    private float T4_ResumeProtect => GetConfig("T4", 20f);     // 切回期：玩家从手机桌面切回游戏时，保护多久不弹插屏 (防骑脸)
    private float T5_BaseInterstitialCD => GetConfig("T5", 180f); // 基础CD：两次插屏广告之间，最基本的冷却等待时间

    // 【A系列：疲劳度增减规则（单位：分）】
    private int A1_InterstitialFatigue => (int)GetConfig("A1", 3f); // 播一次【插屏广告】给玩家增加几点疲劳度
    private int A2_RewardFatigue => (int)GetConfig("A2", 2f);       // 播一次【激励视频】给玩家增加几点疲劳度
    private int A3_MaxFatigue => (int)GetConfig("A3", 100f);        // 疲劳度满级上限 (超过这个分数不再累加)
    
    // 【L系列：根据疲劳度，额外惩罚的插屏CD时间（单位：秒）】
    // 策划目的：疲劳度越低说明是新用户，要加长CD保护他们；疲劳度高说明是老油条，CD短一点多弹广告。
    private float L1_ExtraCD => GetConfig("L1", 60f); // 新手保护：疲劳度在 0~30 分时，插屏冷却时间要额外加多少秒
    private float L2_ExtraCD => GetConfig("L2", 30f); // 过渡期：疲劳度在 31~60 分时，插屏冷却时间额外加多少秒
    private float L3_ExtraCD => GetConfig("L3", 0f);  // 成熟期：疲劳度大于 60 分时，无额外惩罚（插屏会弹得更频繁）
    // 👇 新增：【D系列：每日首关概率规则】
    // 每日玩家打第一关时，展示插屏的概率。假设配表填 0~100 的数值，默认给 100 代表 100% 弹。
    private float D1_FirstLevelAdProb => GetConfig("D", 100f);
    // 运行时状态 (不需要存档的 Session 级数据)
    private DateTime _lastAppResumeTime = DateTime.MinValue;
    private bool _isAppInBackground = false;

    private void Awake()
    {
        // 🌟 1. 标准单例防重复检查
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 🌟 2. 冷启动保护：给应用刚启动时加上 30 秒绝对安全期
        _lastAppResumeTime = DateTime.Now;
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);
        string csvData = null;
        bool isCsvDone = false;
        StartCoroutine( APIGateway.Instance.GameConfigApi.GetGameConfig("adv_general_config",
            onSuccess: (response) => { csvData = response.CsvString; isCsvDone = true;},
            onError:   (error) => { isCsvDone = true; Debug.Log("服务器拉取 广告 配置失败，准备兜底 " + error); }
        ));
        float timeout = 5f;
        while (!isCsvDone && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

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
    /// 获取配置核心方法：如果表里有配，用表里的；如果表里找不到，用 defaultValue 兜底保命。
    /// </summary>
    private float GetConfig(string key, float defaultValue)
    {
        if (_adConfigMap.TryGetValue(key, out float val))
            return val;
        return defaultValue;
    }
    
    /// <summary>
    /// 加载 CSV 配置（请在游戏初始化 ConfigManager 时调用此方法传入 CSV 文本）
    /// </summary>
    public void LoadConfigFromCSV(string csvText)
    {
        if (string.IsNullOrEmpty(csvText)) return;

        // 统一处理换行符
        string[] lines = csvText.Replace("\r", "").Split('\n');
        
        // 确保至少有 3 行（1行注释，1行Key，1行Value）
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
        // 🌟 G2: 累计游戏时间计算 (切后台不计入)
        if (!_isAppInBackground)
        {
            GameDataManager.Instance.UserData.TotalPlayTimeSeconds += Time.deltaTime;
        }
    }

    // 🌟 G4: 切后台与切回来的时间记录
    private void OnApplicationFocus(bool hasFocus)
    {
        _isAppInBackground = !hasFocus;
        if (hasFocus)
        {
            _lastAppResumeTime = DateTime.Now; // 记录切回前台的瞬间
        }
    }

    public void TryShowInterstitial(Action<bool> onComplete)
    {
        // 1. 先问大脑让不让播
        if (!CanShowInterstitial())
        {
            Debug.Log($"[AdRule] 插屏拦截掉了");
            onComplete?.Invoke(false); // 拦截掉了，直接执行回调，让游戏继续
            return;
        }

        // 2. 大脑放行了，调底层 SDK (不管是鸿蒙、安卓还是 iOS)
        Game.self.Ads.ShowInterstitial((success) => 
        {
            if (success)
            {
                // 3. 播成功了，向大脑报账，增加疲劳度并记录时间！
                ReportAdShown(Define.AdType.Interstitial);
            }
            onComplete?.Invoke(success);
        });
    }
    // 展示 Banner 时的标准写法
    public void TryShowBanner()
    {
        if (!CanShowBanner()) return;
        Game.self.Ads.ShowBanner();
    }
    // 展示激励视频时的标准写法
    public void TryShowRewardVideo(Define.AdKey adKey, Action<bool> onComplete)
    {
        // 1. 激励视频一般不拦截，直接让玩家看
        Game.self.Ads.ShowReward(adKey, (success) => 
        {
            if (success)
            {
                // 2. 🌟 核心：播成功了，向大脑报账！
                // 这句代码执行后：
                // - 疲劳度会自动 +2
                // - LastRewardAdTimeTicks 会刷新
                // - G1 规则 (120秒内不准弹插屏) 会瞬间生效！
                ReportAdShown(Define.AdType.Reward);
            }
    
            // 3. 把结果传给原有的业务层（比如发金币、发道具）
            onComplete?.Invoke(success);
        });
    }
    /// <summary>
    /// 🌟 拦截审核：当前是否允许播放插屏？
    /// </summary>
    public bool CanShowInterstitial()
    {
        var userData = GameDataManager.Instance.UserData;
        DateTime now = DateTime.Now;
        
        Debug.Log($"[AdRule] 进入插屏拦截逻辑");
        
        // 👇 新增：【D规则】每日首关插屏概率保护
        // 依据 dayPassStageCount == 0 代表玩家今天一关都还没通关，处于“每日第一个关卡”状态
        if (userData.dayPassStageCount == 0)
        {
            // 如果今天还没进行过判定，则掷骰子
            if (!userData.isDayFirstLevelAdChecked)
            {
                userData.isDayFirstLevelAdChecked = true;
                float rand = UnityEngine.Random.Range(0f, 100f);
                userData.isDayFirstLevelAdAllowed = rand < D1_FirstLevelAdProb;
                
                Debug.Log($"[AdRule] (D规则) 每日首关插屏判定：配置概率 {D1_FirstLevelAdProb}%, 随机点数 {rand:F1}, 是否允许: {userData.isDayFirstLevelAdAllowed}");
                
                // ⚠️ 立即存盘！防止玩家发现首关有广告，直接杀后台重开游戏反复刷概率避开广告
                GameDataManager.Instance.CommitGameData(); 
            }

            // 如果判定的结果是不允许，则直接拦截
            if (!userData.isDayFirstLevelAdAllowed)
            {
                Debug.Log("[AdRule] 被拦截(D规则)：每日首关插屏概率未命中");
                return false;
            }
        }

        // 【G2】游戏时间不足 T2，不展示
        if (userData.TotalPlayTimeSeconds < T2_MinPlayTime)
        {
            Debug.Log($"[AdRule] 被拦截(G2)：累计时长不足 {T2_MinPlayTime}s");
            return false;
        }

        // 【G3】付费保护：付费后 T3 内不播
        if (userData.LastPayTimeTicks > 0)
        {
            TimeSpan paySpan = now - new DateTime(userData.LastPayTimeTicks);
            if (paySpan.TotalSeconds < T3_PayProtect)
            {
                Debug.Log("[AdRule] 被拦截(G3)：处于付费保护期");
                return false;
            }
        }

        // 【G4】切回前台保护：T4 内不播
        TimeSpan resumeSpan = now - _lastAppResumeTime;
        if (resumeSpan.TotalSeconds < T4_ResumeProtect)
        {
            Debug.Log($"[AdRule] 被拦截(G4)：刚切回前台不足 {T4_ResumeProtect}s");
            return false;
        }

        // 【G1】激励视频互斥：看过激励视频后 T1 内不播插屏
        if (userData.LastRewardAdTimeTicks > 0)
        {
            TimeSpan rewardSpan = now - new DateTime(userData.LastRewardAdTimeTicks);
            if (rewardSpan.TotalSeconds < T1_RewardCD)
            {
                Debug.Log($"[AdRule] 被拦截(G1)：距离上次激励视频不足 {T1_RewardCD}s");
                return false;
            }
        }

        // 【G5 + 疲劳度】插屏冷却时间计算
        float extraCD = 0f;
        if (userData.AdFatigueScore <= 30) extraCD = L1_ExtraCD;
        else if (userData.AdFatigueScore <= 60) extraCD = L2_ExtraCD;
        else extraCD = L3_ExtraCD; // 60分以上成熟用户，不再增加额外CD

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

        return true; // 恭喜，通过所有审核！
    }

    /// <summary>
    /// 🌟 拦截审核：当前是否允许展示 Banner
    /// </summary>
    public bool CanShowBanner()
    {
        // 【G2】游戏时间不足 T2，不展示
        if (GameDataManager.Instance.UserData.TotalPlayTimeSeconds < T2_MinPlayTime)
            return false;

        return true;
    }

    // ==========================================
    // 广告播完后的账单上报（用于累加疲劳和重置时间）
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

        // 重要：每次修改这些关键数据后，最好触发一次存档操作，防止杀后台丢数据
        GameDataManager.Instance.CommitGameData(); 
    }
}
