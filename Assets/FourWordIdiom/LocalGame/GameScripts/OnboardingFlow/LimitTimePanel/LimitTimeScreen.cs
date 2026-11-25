using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class LimitTimeScreen : UIWindow, IBackGestureClosePanel
{        
    /// <summary>自动过关开启时，活动页自动关闭前的停留时长（秒）。</summary>
    private const float AutoCloseDelayWhenAutoPlay = 10f;

    [SerializeField] private GameObject minTimeObj; // 关闭按钮
    [SerializeField] private Button helpBtn; // 关闭按钮
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Button hideBtn; // 关闭按钮
    [SerializeField] private Slider slider; 
    [SerializeField] private Image titleImage; 
    [SerializeField] private Image fantitleImage;
    [SerializeField] private Text txttips;
    [SerializeField] private Text txtmintime; 
    [SerializeField] private Text txttime; 
    [SerializeField] private Text txtprogress;
    
    //当前限时奖励物品数据
    LimitDataItem limitData;
    public List<LightItem> LightItems;
    private bool firstenter =true;    
  
    protected override void OnEnable()
    {
        base.OnEnable();
        _claiming = false;   // 界面复用时清掉上一次未走完的结算标记
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        LimitTimeManager.Instance.OnLimitTimeUpdated += UpdateTimeDisplay; // 订阅事件
        // 老存档兼容：上一版本领完最后一段后 timerePuzzleid 会越界，先归位到幸运循环再刷 UI
        LimitTimeManager.Instance.EnsureEndlessState();
        InitLightItems();
        StartCoroutine(InitUI());
        slider.transform.parent.gameObject.SetActive(!LimitTimeManager.Instance.IsComplete());
        // if (SaveSystem.Instance.UserData.LanguageCode == "ChineseTraditional")
        // {
        //     fantitleImage.gameObject.SetActive(true);
        //     titleImage.gameObject.SetActive(false);
        // }
        // else
        // {
        //     fantitleImage.gameObject.SetActive(false);
        //     titleImage.gameObject.SetActive(true);
        // }      
        
        EventDispatcher.instance.TriggerUpdateLayerCoin(true,false,true);
    }

    IEnumerator InitUI()
    {
        int wordcount = LimitTimeManager.Instance.GetCurWordCount();
        limitData = LimitTimeManager.Instance.CurlimitData;

        if (limitData == null) yield break;        

        //进入游戏后首次开启界面
        if (firstenter||wordcount <= 0)
        {
            txtprogress.text = "0/" + limitData.num;
            slider.value = 0;
            txttips.text = string.Format(MultilingualManager.Instance.GetString("limitedRewardsDes06"), limitData.num - wordcount);
        }
        
        // if (GameDataManager.MainInstance.UserData.isNeedShowHelp)
        //     closeBtn.enabled = false;
        
        UpdateMinTimeDisplay();
        yield return new WaitForSeconds(1.2f);
        
        UpdateProgress();

        // 自动过关（挂机）状态：不再 1.7s 就把活动页弹走，停留 AutoCloseDelayWhenAutoPlay 秒后再自动收尾，
        // 期间玩家仍可手动关闭/查看帮助；若期间已手动关闭则不再重复关闭。
        if (GameCoreManager.Instance != null && GameCoreManager.Instance.IsTrueAuto)
        {
            yield return new WaitForSeconds(AutoCloseDelayWhenAutoPlay);

            if (gameObject != null && gameObject.activeInHierarchy)
            {
                OnCloseBtn();
            }
        }
    }

    private void InitLightItems()
    {
        List<LimitDataItem> items = LimitTimeManager.Instance.GetLimitItems();
        if (items == null) return;

        for (int i = 0; i < LightItems.Count && i < items.Count; i++)
        {
            LimitDataItem tDataItem=items[i];
            LightItem lightItem = LightItems[i];
            lightItem.SetUI(tDataItem);
        }

        // 进入最后一段后，最后一个灯笼改为显示最后一段（幸运循环段）的奖励配置
        if (GameDataManager.Instance.UserData.timerePuzzleid > LightItems.Count - 1)
        {
            int lastId = LimitTimeManager.Instance.GetLastLimitId();
            if (lastId >= 0)
            {
                LimitTimeManager.Instance.GetCurWordCount();   // 同步 CurlimitData + 老存档归位
                LightItems[LightItems.Count - 1].SetUI(LimitTimeManager.Instance.GetLimitItem(lastId));
            }
        }
    }

    /// <summary>
    /// 幸运循环里，最后一个灯笼每次领完都要回到「待完成」状态：
    /// 重新 SetUI 一遍（奖励图标复原、清掉打勾、显示“幸运循环”），进度条由随后的 UpdateProgress(true) 归零。
    /// 只在已进入幸运循环时生效，普通段不受影响。
    /// </summary>
    private void RefreshLastLanternIfLuckyLoop()
    {
        if (!LimitTimeManager.Instance.IsLuckyLoop()) return;
        if (LightItems == null || LightItems.Count == 0) return;

        int lastId = LimitTimeManager.Instance.GetLastLimitId();
        if (lastId < 0) return;

        // 最后一个灯笼承载最后一段配置（第 11 个灯笼 → 配置 id 11）
        int lastIndex = Mathf.Min(LightItems.Count - 1, lastId);
        LightItems[lastIndex].SetUI(LimitTimeManager.Instance.GetLimitItem(lastId));
    }

    /// <summary>
    /// 是否正在播放「某一段领完」的结算动画（点亮灯笼 → 推进一段 → 进度条归位/续走）。
    /// 用来挡住 slider 的重复 OnComplete：旧实现里重复回调会让 id 连着推进两次，
    /// 中间那段灯笼既不亮、奖励也不发，看起来就是「跳过下一个灯笼，点亮下下一个」。
    /// </summary>
    private bool _claiming;

    private void UpdateProgress(bool isreset=false)
    {
        if (LimitTimeManager.Instance.IsComplete()) return;
        int wordcount = LimitTimeManager.Instance.GetCurWordCount();
        limitData = LimitTimeManager.Instance.CurlimitData;
        if (limitData == null) return;   // 阶段越界/配置缺失时先不刷，避免空引用

        float durtime = wordcount==0?0.1f:0.5f;
        if(isreset) slider.value = 0;
        txttips.text = string.Format(MultilingualManager.Instance.GetString("limitedRewardsDes06"), limitData.num- wordcount);
        
        float progress = (float)wordcount/limitData.num;

        // ★ 先杀掉 slider 上的旧 tween：面板重复打开 / 递归刷新时会叠多条 DOValue，
        //   每条都会走 OnComplete ⇒ 同一次达标被结算多次（一次点亮好几个灯笼）。
        slider.DOKill();
        slider.DOValue(progress,durtime).OnComplete(OnProgressAnimEnd);

        UpdateMinTimeDisplay();
    }

    /// <summary>
    /// 进度条动画结束：只结算「当前这一段」——点亮该段的灯笼 → 推进一段 → 进度条归位后再回 UpdateProgress。
    /// 玩家一次攒够多段时，靠 UpdateProgress 的递归一段一段点亮，不会一次跨两段。
    /// </summary>
    private void OnProgressAnimEnd()
    {
        if (_claiming) return;   // 上一段结算动画还没走完，忽略重复回调

        int wordcount = LimitTimeManager.Instance.GetCurWordCount();
        limitData = LimitTimeManager.Instance.CurlimitData;
        if (limitData == null) return;

        if (wordcount < limitData.num)
        {
            txtprogress.text = wordcount + "/" + limitData.num;
            return;
        }

        _claiming = true;

        // ★ 点亮用的必须推进「之前」的 id；推进之后再取就是下一个灯笼了（这正是跳格的原因之一）
        int stageId = GameDataManager.Instance.UserData.timerePuzzleid;
        int index = stageId >= LightItems.Count ? LightItems.Count - 1 : stageId;
        LightItems[index].UpdateRewardValue();

        // ★ 推进只在这里发生一次。原来紧跟的 ClaimCurrentReward() 内部在「有溢出词数」时
        //   会再调一次 AdvanceStageAfterClaim() ⇒ id 一次跳两段，中间那段的灯笼永远不亮、奖励也不发。
        //   （它的截断逻辑 totalBefore+currentNeed+overflow 恒等于原累计值，是空操作，可以安全去掉。）
        LimitTimeManager.Instance.AdvanceStageAfterClaim();
        LimitTimeManager.Instance.UpdateLimitTimeBtnUI();
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedLightLimit,1);

        txtprogress.text = wordcount + "/" + limitData.num;

        slider.transform.DOScaleZ(1, 0.2f).OnComplete(() =>
        {
            // 面板可能在结算动画途中被关掉：此时不再回头刷 UI / 递归，避免在已隐藏的界面上继续领
            if (this == null || !gameObject.activeInHierarchy) { _claiming = false; return; }
            // 最后一个灯笼领完后：把灯笼重新刷成“待完成 + 幸运循环”状态
            RefreshLastLanternIfLuckyLoop();
            _claiming = false;
            UpdateProgress(true);   // 若下一段也已达标，这里会点亮下一段（逐段）
        });
    }

    private void QuickComplete(int guard = 0)
    {
        if (LimitTimeManager.Instance.IsComplete()) return;
        if (guard > 50) return; // 防御：数据异常时避免无限递归

        int wordcount = LimitTimeManager.Instance.GetCurWordCount();
        limitData = LimitTimeManager.Instance.CurlimitData;
        // slider.DOValue((float)wordcount / limitData.num, 0);
        if (limitData == null) return;
        
        if (wordcount >= limitData.num)
        {
            if (GameDataManager.Instance.UserData.timerePuzzleid >= LightItems.Count)
            {
                int index = LightItems.Count-1;
                LightItems[index].UpdateRewardValue();
            }
            else
            {
                LightItems[GameDataManager.Instance.UserData.timerePuzzleid].UpdateRewardValue();
            }

            // 推进阶段：最后一段会进入幸运循环并把该段进度清零，下一轮递归里 wordcount 归 0 自然退出
            LimitTimeManager.Instance.AdvanceStageAfterClaim();
            DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedLightLimit,1);
            
            QuickComplete(guard + 1);
        }
        //txtprogress.text = wordcount + "/" + limitData.num;
    }

    private void UpdateMinTimeDisplay()
    {
        bool canshow = LimitTimeManager.Instance.LimitTimeCanShow();
      
        if (canshow&&!LimitTimeManager.Instance.IsComplete())
        {
            minTimeObj.gameObject.SetActive(true);
            int min = LimitTimeManager.Instance.GetLimitWordMinTime();
            txtmintime.text = $"<size=48>x2</size>\n{min}<size=30>分钟</size>";
        }
        else
        {
            minTimeObj.gameObject.SetActive(false);
        }
    }
    
    private void UpdateTimeDisplay(string time)
    {
        if (!string.IsNullOrEmpty(time))
        {
            txttime.text = time; // 更新文本
        }
    }
    
    protected override void InitializeUIComponents()
    {
        hideBtn.onClick.AddListener(OnCloseBtn); // 绑定关闭按钮事件
        closeBtn.onClick.AddListener(OnCloseBtn); // 绑定关闭按钮事件
        helpBtn.AddClickAction(OnHelpBtn);
    }
    
    private void OnHelpBtn()
    {
        SystemManager.Instance.ShowPanel(PanelType.LimitHelpScreen);
    }

    /// <summary>侧滑 = 点关闭按钮（含 QuickComplete 等收尾逻辑）。</summary>
    public void CloseFromBackGesture()
    {
        OnCloseBtn();
    }

    private void OnCloseBtn()
    {
        AudioManager.Instance.TriggerVibration(40, 50);
        QuickComplete();
        base.Close(); // 隐藏面板
        firstenter=false;
    }
    
    public override void OnHideAnimationEnd()
    {
        base.OnHideAnimationEnd();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        LimitTimeManager.Instance.UpdateLimitTimeBtnUI();
        EventDispatcher.instance.TriggerUpdateLayerCoin(false,true,false);

        LimitTimeManager.Instance.OnLimitTimeUpdated -= UpdateTimeDisplay; // 订阅事件
    }
}



