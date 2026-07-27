using System;
using System.Collections;
using System.Collections.Generic;
using Coffee.UIEffects;
using DG.Tweening;
using Middleware;
using UnityEngine;
using UnityEngine.UI;

public class SevenSignScreen : UIWindow
{
    [Header("Calendar UI")]
    [SerializeField] private ScrollRect calendarScrollRect;
    [SerializeField] private RectTransform calendarContent;
    [SerializeField] private Image fillSign;
    [SerializeField] private Image notSign;
    [SerializeField] private GameObject monthPrefab;
    [SerializeField] private Button prevMonthBtn;
    [SerializeField] private Button nextMonthBtn;
    [SerializeField] private Button oktBtn;
    [SerializeField] private Button closeBtn;
    [SerializeField] private Text titleText;
    [SerializeField] private Text winTimesDexText;
    [SerializeField] private Text currentMonthText;
    [SerializeField] private Text winTimes; // 累计签到天数
    [SerializeField] private Text breakWinTimes; // 累计签到天数
    [SerializeField] private Slider slider; // 累计签到天数
    [SerializeField] private List<GameObject> daytitleText; // 累计签到天数
    [SerializeField] private List<GameObject> rewardBoxs; // 累计签到天数

    // 数据
    private List<(int year, int month)> allMonths = new List<(int, int)>();
    private int currentMonthIndex = 0;
    private MonthView monthViews = new MonthView();
    private bool isRefreshing = false;
    private StreakManager streakManager;

    // ==================== 生命周期 ====================

    protected override void Awake()
    {
        streakManager = StreakManager.Instance;
        if (streakManager != null)
            streakManager.OnSignSuccess += OnSignSuccess;

        base.Awake();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance?.PlaySoundEffect("ShowUI");
        
        StreakManager.Instance.UpdateWinStreak();

        // 初始化 UI 显示
        UpdateWinTimes();

        // 构建日历
        BuildCalendar();

        InitUI();

        StartCoroutine(ShowReward());
        oktBtn.gameObject.SetActive(false);
        
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,true);
    }

    private void InitUI()
    {
        titleText.text = MultilingualManager.Instance.GetString("DailyVictory");
        winTimesDexText.text = MultilingualManager.Instance.GetString("WinDays");
        daytitleText[0].GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("Su");
        daytitleText[1].GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("Mo");
        daytitleText[2].GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("Tu");
        daytitleText[3].GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("We");
        daytitleText[4].GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("Th");
        daytitleText[5].GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("Fr");
        daytitleText[6].GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("Sa");
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (streakManager != null)
            streakManager.OnSignSuccess -= OnSignSuccess;
    }

    protected override void InitializeUIComponents()
    {
        oktBtn.AddVibraClickAction(Close);
        prevMonthBtn.AddClickAction(OnPrevMonth);
        nextMonthBtn.AddClickAction(OnNextMonth);
        closeBtn.AddVibraClickAction(Close);
    }

    // ==================== UI 更新 ====================

    private void UpdateWinTimes()
    {
       int curStreak = streakManager.GetCurrentStreak();
       bool isbreak = streakManager.CheckWinStreakBreak();

       breakWinTimes.gameObject.SetActive(isbreak);
       winTimes.gameObject.SetActive(!isbreak);
       
       notSign.gameObject.SetActive(isbreak);
       fillSign.gameObject.SetActive(!isbreak);
       
       if (isbreak)
       {
           breakWinTimes.text = curStreak.ToString();
           winTimesDexText.color = new Color(0.509804f, 0.509804f, 0.509804f,1f);
           winTimesDexText.GetComponent<Outline>().effectColor = new Color(0.509804f, 0.509804f, 0.509804f,1f);
       }
       else
       {
           winTimes.text = curStreak.ToString();
           winTimesDexText.color = new Color(0.6666667f, 0.1764706f, 0.06666667f,1f);
           winTimesDexText.GetComponent<Outline>().effectColor = new Color(0.6666667f, 0.1764706f, 0.06666667f,1f);
       }
    }

    // ==================== 日历构建 ====================

    private void BuildCalendar()
    {
        if (streakManager == null)
        {
            Debug.LogError("SevenSignScreen: StreakManager 未设置！");
            return;
        }

        // 获取所有有数据的月份
        allMonths = streakManager.GetAllMonthsWithData();
        if (allMonths.Count == 0)
        {
            DateTime now = DateTime.UtcNow;
            allMonths.Add((now.Year, now.Month));
        }

        // 清空旧视图
        foreach (Transform child in calendarContent)
            Destroy(child.gameObject);
        
        // 默认定位到当前月份（最后一个）
        currentMonthIndex = allMonths.Count - 1;

        // 生成每个月份的视图
        var (year, month) = allMonths[currentMonthIndex];
        {
            GameObject monthGO = Instantiate(monthPrefab, calendarContent);
            monthViews = monthGO.GetComponent<MonthView>();
            if (monthViews == null)
            {
                Debug.LogError("月份预制体缺少 MonthView 组件！");
            }

            List<int> signedDays = streakManager.GetSignedDaysInMonth(year, month);
            monthViews.Setup(year, month, signedDays);
        }
        
        UpdateNavigationButtons();
        UpdateMonthTitle();
    }

    // ==================== 月份切换 ====================
    
    private void UpdateToMonth(int index)
    {
        if (index < 0) index = 0;
        if (index >= allMonths.Count) index = allMonths.Count - 1;

        if (currentMonthIndex == index)
        {
            // 如果已经是当前月份，仍然刷新数据（保证最新）
            RefreshCurrentMonthOnly();
            return;
        }

        currentMonthIndex = index;
        RefreshCurrentMonthOnly();

        UpdateNavigationButtons();
        UpdateMonthTitle();
    }

    // private void ScrollToMonth(int index, bool animate = true)
    // {
    //     if (index < 0) index = 0;
    //     if (index >= allMonths.Count) index = allMonths.Count - 1;
    //
    //     if (currentMonthIndex == index && monthViews.Count > 0)
    //     {
    //         // 如果已经是当前月份，仍然刷新数据（保证最新）
    //         RefreshCurrentMonthOnly();
    //         return;
    //     }
    //
    //     currentMonthIndex = index;
    //
    //     // 定位到目标月份（水平滚动）
    //     if (allMonths.Count > 1)
    //     {
    //         float targetPos = (float)index / (allMonths.Count - 1);
    //         if (animate)
    //             DOTween.To(() => calendarScrollRect.horizontalNormalizedPosition,
    //                        x => calendarScrollRect.horizontalNormalizedPosition = x,
    //                        targetPos, 0.3f).OnComplete(() => RefreshCurrentMonthOnly());
    //         else
    //         {
    //             calendarScrollRect.horizontalNormalizedPosition = targetPos;
    //             RefreshCurrentMonthOnly();
    //         }
    //     }
    //     else
    //     {
    //         calendarScrollRect.horizontalNormalizedPosition = 0;
    //         RefreshCurrentMonthOnly();
    //     }
    //
    //     UpdateNavigationButtons();
    //     UpdateMonthTitle();
    // }

    private void OnPrevMonth()
    {
        if (currentMonthIndex > 0)
            UpdateToMonth(currentMonthIndex - 1);
    }

    private void OnNextMonth()
    {
        if (currentMonthIndex < allMonths.Count - 1)
            UpdateToMonth(currentMonthIndex + 1);
    }

    private void UpdateNavigationButtons()
    {
        //nextMonthBtn.interactable = (currentMonthIndex < allMonths.Count - 1);
        if (prevMonthBtn != null)
            prevMonthBtn.gameObject.SetActive(currentMonthIndex > 0);
        if (nextMonthBtn != null)
            nextMonthBtn.gameObject.SetActive(currentMonthIndex < allMonths.Count - 1);
    }

    private void UpdateMonthTitle()
    {
        if (currentMonthText != null && allMonths.Count > 0 && currentMonthIndex < allMonths.Count)
        {
            var (year, month) = allMonths[currentMonthIndex];
            
            string monthstr= month<10 ? "0"+month : month.ToString();
             
            currentMonthText.text = $"{monthstr}/{year}";
        }
    }
    
    IEnumerator ShowReward()
    {
        
        if (GameDataManager.Instance.UserData._signSaveData.curAwardid == 1)
        {
            slider.value = 0;
        }
        
        for (int i = 0; i < 3 ; i++)
        {
            rewardBoxs[i].transform.GetChild(0).gameObject.SetActive(true);
            rewardBoxs[i].transform.GetChild(1).gameObject.SetActive(false);
        }
        
        yield return new WaitForSeconds(0.2f);
     
        int curAwardid = StreakManager.Instance.GetCurAwardid();
        
        bool isClaim = GameDataManager.Instance.UserData._signSaveData.CheckWinClaim();
        
        if (isClaim)
        {
            UpdateBoxsStatus(curAwardid);
        }
        
        yield return new WaitForSeconds(0.3f);
        
        float value=curAwardid/30.0f;

        slider.DOValue(value, 0.3f);
       
        
        yield return new WaitForSeconds(0.5f);
        
        if (StreakManager.Instance.CheckBoxRewardsExist()&&curAwardid%10==0&&!isClaim&&curAwardid>0)
        {
            UpdateBoxsStatus(curAwardid);
            
            StreakManager.Instance.winType = WinType.StreakWin;
            SystemManager.Instance.ShowPanel(PanelType.SignAwardScreen);
            
            yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.SignAwardScreen));
            
            oktBtn.gameObject.SetActive(true);
        }
        
    }

    private void UpdateBoxsStatus(int curAwardid)
    {
        int maxindex= curAwardid/10<=3?curAwardid/10:3;
            
        for (int i = 0; i < maxindex ; i++)
        {
            rewardBoxs[i].transform.GetChild(0).gameObject.SetActive(false);
            rewardBoxs[i].transform.GetChild(1).gameObject.SetActive(true);
        }
    }


    private void OnSignSuccess(int newStreak)
    {
        // 由事件触发时刷新（避免重复）
        RefreshCurrentMonthOnly();
        UpdateWinTimes();
    }

    /// <summary>
    /// 刷新当前显示的月份视图（从 StreakManager 获取最新数据）
    /// </summary>
    private void RefreshCurrentMonthOnly()
    {
        //var view = monthViews;
        var (year, month) = allMonths[currentMonthIndex];
        List<int> signedDays = streakManager.GetSignedDaysInMonth(year, month);
        monthViews.Refresh(year, month,signedDays); // MonthView 的 Refresh 方法需实现
    }

    // ==================== 关闭 ====================

    public override void Close()
    {
        base.Close();
    }

    public override void OnHideAnimationEnd()
    {
        // if (GameDataManager.Instance.UserData._signSaveData.curAwardid == 0)
        // {
        //     slider.value = 0;
        // }
        EventDispatcher.instance.TriggerChangeGoldUI(0, false);
        base.OnHideAnimationEnd();
    }

    // ==================== 可选：滑动结束吸附 ====================

    public void OnScrollEnd()
    {
        int totalMonths = allMonths.Count;
        if (totalMonths <= 1) return;

        float progress = Mathf.Clamp01(calendarScrollRect.horizontalNormalizedPosition);
        int targetIndex = Mathf.RoundToInt(progress * (totalMonths - 1));
        if (targetIndex != currentMonthIndex)
            UpdateToMonth(targetIndex);
    }
}