using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class OverallRankingBannerUI : MonoBehaviour
{
    [Header("Interaction (交互)")]
    [SerializeField] private Button openNamesListButton; // 在 Inspector 中拖入用来点击的按钮
    [SerializeField] private Text currentTip;
    [SerializeField] private Text nextTip;
    [Header("Top Info (顶部信息)")]
    [SerializeField] private Text levelTitleText; // 左上角，例如 "第1级 蝉眠"
    [SerializeField] private GameObject timerRoot; // 右上角，倒计时节点的父物体
    [SerializeField] private Text timerText;       // 右上角倒计时文本，例如 "12天07时"

    [Header("Center Score Info (核心分数与描述)")]
    [SerializeField] private Text totalScoreText;  // 中间大字分数，例如 "43200000"
    [SerializeField] private Text stateDescText;   // 中间描述，例如 "禅意之境： 蛰伏待机"

    [Header("Progress Info (底部进度信息)")]
    [SerializeField] private Text currentLevelText; // 左下当前等级名，例如 "蝉眠"
    [SerializeField] private Text nextLevelText;    // 右下下一等级名，例如 "初觉"
    [SerializeField] private Slider progressBar;     // 进度条 Image (需设置为 Fill 模式)
    [SerializeField] private Text progressText;     // 进度文本，例如 "850 / 1500"

    private Coroutine _timerCoroutine;

    private void Awake()
    {
        if (openNamesListButton != null)
        {
            openNamesListButton.AddClickAction(() => 
            {
                SystemManager.Instance.ShowPanel(PanelType.OverallRankingNames);
            });
        }
    }

    private void Start()
    {
        currentTip.text = MultilingualManager.Instance.GetString("Current", "hudie");
        nextTip.text = MultilingualManager.Instance.GetString("NextLevel", "hudie");
    }

    /// <summary>
    /// 刷新 Banner 的基础境界信息
    /// </summary>
    public void RefreshBannerInfo()
    {
        // 获取玩家当前总分 (假设存在 UserData 中，与你之前的逻辑一致)
        int myScore = GameDataManager.Instance.UserData.overallZenScore;
        totalScoreText.text = myScore.ToString();

        // 从你写的 Manager 中获取配置表
        var realmList = OverallRankingManager.Instance.RealmLevelList;
        if (realmList == null || realmList.Count == 0) return;

        // 寻找第一个目标分数大于我当前分数的等级
        int currentLevel = OverallRankingManager.Instance.GetZenLevelByScore(myScore);
        var currentRealm = realmList.FirstOrDefault(r => r.Level == currentLevel);
       
        if (currentRealm != null)
        {
            // 获取多语言文本
            string currentName = MultilingualManager.Instance.GetString(currentRealm.NameKey, "hudie");
            string feelDesc = MultilingualManager.Instance.GetString(currentRealm.FeelKey, "hudie");
            string llDesc = MultilingualManager.Instance.GetString("Level", "hudie");
            
            // 左上角和左下角名字
            levelTitleText.text = $"{string.Format(llDesc, currentRealm.Level)} {currentName}";
            currentLevelText.text = currentName;
            
            // 禅意之境描述
            string prefix = MultilingualManager.Instance.GetString("ZenState", "hudie") ?? "禅意之境：";
            stateDescText.text = $"{prefix} {feelDesc}";

            // 判断是否还有下一级 (查表看有没有 Level + 1)
            var nextRealm = realmList.FirstOrDefault(r => r.Level == currentRealm.Level + 1);
            if (nextRealm != null)
            {
                nextLevelText.text = MultilingualManager.Instance.GetString(nextRealm.NameKey, "hudie");
                OverallRankingManager.Instance.GetZenProgress(myScore, out int curScore, out int maxScore);
                progressText.text = $"{curScore} / {maxScore}";
                
                // 防止除以0的防御性代码
                if (maxScore > 0)
                {
                    progressBar.value = Mathf.Clamp01((float)curScore / maxScore);
                }
            }
            else
            {
                // 已满级处理
                nextLevelText.text = "已满级";
                progressBar.value = 1f;
                progressText.text = "MAX";
            }
        }
    }
    
    /// <summary>
    /// 控制右侧倒计时的显隐与运行（切换 Tab 时调用）
    /// </summary>
    /// <param name="isActive">是否显示</param>
    /// <param name="remainingSeconds">剩余秒数</param>
    public void SetTimer(bool isActive, int remainingSeconds = 0)
    {
        if (_timerCoroutine != null)
        {
            StopCoroutine(_timerCoroutine);
            _timerCoroutine = null;
        }

        timerRoot.SetActive(isActive);

        if (isActive && remainingSeconds > 0)
        {
            _timerCoroutine = StartCoroutine(TimerRoutine(remainingSeconds));
        }
    }
    
    private IEnumerator TimerRoutine(int seconds)
    {
        WaitForSeconds wait = new WaitForSeconds(1f);
        
        // 缓存多语言，避免每秒去字典里查找
        string dayStr = MultilingualManager.Instance.GetString("TimeD") ?? "天";
        string hourStr = MultilingualManager.Instance.GetString("TimeH") ?? "时";
        string minStr = MultilingualManager.Instance.GetString("TimeM") ?? "分";
        string secStr = MultilingualManager.Instance.GetString("TimeS") ?? "秒";

        while (seconds > 0)
        {
            timerText.text = FormatTime(seconds, dayStr, hourStr, minStr, secStr);
            yield return wait;
            seconds--;
        }
        
        timerText.text = MultilingualManager.Instance.GetString("LotusRankingEnd") ?? "结算中...";
    }
    
    // 复用你的时间格式化逻辑
    private string FormatTime(int seconds, string d, string h, string m, string s)
    {
        TimeSpan ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalDays >= 1) return $"{ts.Days}{d}{ts.Hours:D2}{h}";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}{h}{ts.Minutes:D2}{m}";
        return $"{ts.Minutes:D2}{m}{ts.Seconds:D2}{s}";
    }
}
