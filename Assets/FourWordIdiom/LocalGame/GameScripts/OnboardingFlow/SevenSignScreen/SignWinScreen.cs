using System;
using System.Collections;
using System.Collections.Generic;
using BestHTTP;
using DG.Tweening;
using Middleware;
using Spine.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.PlayerLoop;
using UnityEngine.UI;

public class SignWinScreen : UIWindow
{
   [SerializeField] private Button okBtn; // 显示日历按钮
   [SerializeField] private Text lastWinTimes; // 连胜签到次数
   [SerializeField] private Text WinTimes; // 连胜签到次数
   [SerializeField] private GameObject boxReward; // 连胜签到次数
   [SerializeField] private Text titleText;
   [SerializeField] private Text daytipText;
   [SerializeField] private GameObject spineitem;
   
   [SerializeField] private List<GameObject> daytitleText; // 累计签到天数
   [SerializeField] private List<DayCell> DayCells; // 签到日期格子
   
    void Start()
    {
     
    }


    protected override void OnEnable()
    {
        base.OnEnable();
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        InitUI();

        OnSignBtnClick();
        
        spineitem.GetComponent<SkeletonAnimation>().AnimationState.SetAnimation(0, "idle01", false);
        
        HTTPManager.OnUpdate();
        
        okBtn.gameObject.SetActive(false);
    }

    private void InitUI()
    {
        int curStreak = StreakManager.Instance.GetCurrentStreak();
        
        titleText.text = MultilingualManager.Instance.GetString("DailyVictory");
        int lastStreak = (curStreak - 1)<=0 ?0:(curStreak - 1);
        lastWinTimes.text = lastStreak.ToString();
        WinTimes.text = curStreak.ToString();

        int days=curStreak;
        
        if (curStreak > 7)
        {
            days=curStreak%7;

            if (curStreak % 7 == 0)
            {
                days = 7;
            }
        }
        
        string daytipkey = "Day"+days+"Text";
        string daytipvalue = MultilingualManager.Instance.GetString(daytipkey);

        if (daytipvalue.Length > 14)
        {
            daytipText.alignment = TextAnchor.MiddleLeft;
        }
        else
        {
            daytipText.alignment = TextAnchor.MiddleCenter;
        }
        
        daytipText.text = daytipvalue;
        

        UpdateWeekdayTitles();
        
    }
    
    private void UpdateWeekdayTitles()
    {
        if (daytitleText == null || daytitleText.Count < 7) return;

        // 1. 获取首次签到日期（若为0则使用今天）
        long firstDay = StreakManager.Instance.GetFirstSignDay();
        DateTime refDate;
        if (firstDay == 0)
            refDate = DateTime.Now; // 或 DateTime.Now，取决于业务
        else
            refDate = UIUtilities.DayIndexToDateTime(firstDay);

        // 2. 获取星期几（0=Sunday）
        int offset = (int)refDate.DayOfWeek;

        // 3. 从多语言获取7个星期缩写（顺序：周日~周六）
        string[] weekNames = new string[]
        {
            MultilingualManager.Instance.GetString("Su"),
            MultilingualManager.Instance.GetString("Mo"),
            MultilingualManager.Instance.GetString("Tu"),
            MultilingualManager.Instance.GetString("We"),
            MultilingualManager.Instance.GetString("Th"),
            MultilingualManager.Instance.GetString("Fr"),
            MultilingualManager.Instance.GetString("Sa")
        };

        // 4. 按偏移赋值
        for (int i = 0; i < 7; i++)
        {
            int index = (offset + i) % 7;
            daytitleText[i].GetComponentInChildren<Text>().text = weekNames[index];
            DayCells[i].basedayText.gameObject.SetActive(false);
        }
    }

    private void OnSignBtnClick()
    {
        bool success = StreakManager.Instance.ClaimDailyReward();
        if (success)
        {
            // 刷新当前月份视图
            InitUI();
            StartCoroutine(RefreshDayCells());
           
            // 可在此播放奖励动画或弹出提示
        }
        else
        {
            // 今日已签到，可给予提示（例如弹窗）
            Debug.Log("今日已签到");
        }

        StartCoroutine(ShowReward());
    }

    IEnumerator ShowReward()
    {
        yield return new WaitForSeconds(0.3f);
        AudioManager.Instance.PlaySoundEffect("signwinLeaf");
        yield return new WaitForSeconds(4.5f);
        
        int currentStreak = StreakManager.Instance.GetCurrentStreak();
        
        if (StreakManager.Instance.CheckSevenRewardsExist()&&currentStreak%7==0)
        {
            StreakManager.Instance.winType = WinType.SevenWin;
            SystemManager.Instance.ShowPanel(PanelType.SignAwardScreen);
            
            yield return new WaitUntil(() => !SystemManager.Instance.PanelIsShowing(PanelType.SignAwardScreen));
            
            okBtn.gameObject.SetActive(true);
        }
    }
    
    /// <summary>
    /// 刷新 7 个日期格子，使其显示包含今天的连续 7 天，
    /// 且索引 0 对应的星期与首次签到的那一天对齐。
    /// </summary>
    IEnumerator RefreshDayCells()
    {
        if (DayCells == null || DayCells.Count < 7) yield break;

        long todayIndex = UIUtilities.GetCurrentDayIndex();
        DateTime todayDate = UIUtilities.DayIndexToDateTime(todayIndex);
        int todayDayOfWeek = (int)todayDate.DayOfWeek;

        long firstDay = StreakManager.Instance.GetFirstSignDay();

        DateTime firstDate = UIUtilities.DayIndexToDateTime(firstDay);
        int firstDayOfWeek = (int)firstDate.DayOfWeek;

        int todayIndexInWeek = (todayDayOfWeek - firstDayOfWeek + 7) % 7;
        DateTime startDate = todayDate.AddDays(-todayIndexInWeek);
        
        for (int i = 0; i < 7; i++)
        {
            DateTime date = startDate.AddDays(i);
            long dayIndex = UIUtilities.GetSomeDayIndex(date);
           
            // 判断是否在签到区间内（从首次签到到今天）
            bool isSigned = (dayIndex >= firstDay && dayIndex <= todayIndex);

            if (dayIndex != todayIndex)
            {
                DayCells[i].SetSignedState(isSigned);
                DayCells[i].GetComponent<Animator>().enabled = false;
            }
            else 
            {
                DayCells[i].signMark.gameObject.SetActive(false);
            }
        }
        
          
        if (GameDataManager.Instance.UserData._signSaveData.currentStreak%7!=0)
        {
            boxReward.transform.GetChild(1).gameObject.SetActive(false);
        }
        
        yield return new WaitForSeconds(4f);
        
        for (int i = 0; i < 7; i++)
        {
            DateTime date = startDate.AddDays(i);
            long dayIndex = UIUtilities.GetSomeDayIndex(date);

            if (dayIndex == todayIndex)
            {
                DayCells[i].GetComponent<Animator>().enabled = true;
                DayCells[i].signMark.gameObject.SetActive(GameDataManager.Instance.UserData._signSaveData.currentStreak%7!=0);
                AudioManager.Instance.PlaySoundEffect("winday");
                //DayCells[i].signMark.gameObject.SetActive(GameDataManager.Instance.UserData._signSaveData.currentStreak != 7);
            }
        }
        
        if (GameDataManager.Instance.UserData._signSaveData.currentStreak%7==0)
        {
            boxReward.transform.GetChild(1).gameObject.SetActive(true);
        }
        else
        {
             okBtn.gameObject.SetActive(true);
        }
    }
   
    protected override void InitializeUIComponents()
    {
        okBtn.AddVibraClickAction(ClickOKBtn); // 绑定关闭按钮事件
    }

    private void ClickOKBtn()
    {
        int curStreak = StreakManager.Instance.GetCurrentStreak();
        int GetcurAwardid = StreakManager.Instance.GetcurAwardid();
        int offlineSeconds= PlayerPrefs.GetInt("offline_Seconds", 0);
        int goldcount= GameDataManager.Instance.UserData.Gold;
        int todaywinTime = GameDataManager.Instance.UserData.chessdayPassStageCount;
        int toolcount = GameDataManager.Instance.UserData.toolInfo[102].count +
                        GameDataManager.Instance.UserData.toolInfo[104].count;

        bool isTriggerFirstWin = offlineSeconds >= 80 && todaywinTime <= 1 &&goldcount<= 300
                                 && toolcount <= 3 && curStreak == 1;
        
        Debug.Log("是否触发回归奖励: "+isTriggerFirstWin+"离线时间(秒数): "+offlineSeconds+"今日拼字玩法通关次数: "+todaywinTime+"金币数量: "+goldcount+"道具数量: "+toolcount+"连胜天数: "+curStreak);
        
        switch ((LevelType)GameDataManager.Instance.UserData.levelMode)
        {
            case LevelType.BlockWord:
                if(GetcurAwardid%10==0)
                {
                    SystemManager.Instance.ShowPanel(PanelType.SevenSignScreen);
                }
                break;
            case LevelType.ChessWord:
                if (isTriggerFirstWin)
                {
                    SystemManager.Instance.ShowPanel(PanelType.ReturnFirstWinScreen);
                }
                else if(GetcurAwardid%10==0)
                {
                    SystemManager.Instance.ShowPanel(PanelType.SevenSignScreen);
                }
                break;
            case LevelType.HexWord:
                if(GetcurAwardid%10==0)
                {
                    SystemManager.Instance.ShowPanel(PanelType.SevenSignScreen);
                }
                break;
        }
       
        
        OnCloseBtn();
    }

   
    private void OnCloseBtn()
    {
        base.Close(); // 隐藏面板
    }

    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
    }
}
