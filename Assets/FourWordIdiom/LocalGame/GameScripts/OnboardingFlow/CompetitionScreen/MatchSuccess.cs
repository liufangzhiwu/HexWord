using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

public class MatchSuccess : UIWindow, IBackGestureClosePanel
{        
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Text wordtips;
    [SerializeField] private Image titleImage; 
    [SerializeField] private Transform boxImageParent;
    //[SerializeField] private Image closeboxImage;
    [SerializeField] private Image awardIconprofab; 
    [SerializeField] private Transform awarditemParent; 
    // 修正私有字段命名
    private GameObject _boxSpine; 
    private ObjectPool _objectPool; // 对象池实例
    private Transform awardObj;

    // 修正私有字段命名
    private List<Transform> _awardobjs = new List<Transform>();
    private List<List<int>> _awards = new List<List<int>>();

    // 关闭流程防重入（按钮连点 / 侧滑与按钮同时到达）
    private bool _closing;
    private bool _hasGold;
    // 本局金币奖励总额：金币先入账但不刷顶栏数字，等金币飞入到达后再一次性刷新，
    // 避免「打开结算时数字先滚一次 + 点领取金币再飞一次」被看成两段金币动效
    private int _goldReward;

    protected void Start()
    {
        // 初始化对象池
        _objectPool = new ObjectPool(awardIconprofab.gameObject, ObjectPool.CreatePoolContainer(transform, "RewardItemPool"));
        InitButton();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        _closing = false; // 界面复用时重置防重入标记
        _goldReward = 0;
        _hasGold = false;
        InitUI();
        StartCoroutine(PlayBoxSpineAnim());
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedWinMatch, 1);
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        
        AchievementManager.Instance.AddAvatarFrameItems(AvatarUnlockType.FishMatch,GameDataManager.Instance.FishUserSave.rank);
    }

    IEnumerator PlayBoxSpineAnim()
    {
        // 移除未使用的初始化值
        GameObject fispine;
        string spinePath = "Effect_FishBox01";
        // 修正局部变量命名
        Vector3 startVector = FishInfoController.Instance.dashparent.threeBoxImage.transform.position;
        switch (GameDataManager.Instance.FishUserSave.rank)
        {
            case 1:
                spinePath = "Effect_FishBox01";
                startVector = FishInfoController.Instance.dashparent.oneBoxImage.transform.position;
                break;
            case 2:
                spinePath = "Effect_FishBox02";
                startVector = FishInfoController.Instance.dashparent.twoBoxImage.transform.position;
                break;
            case 3:
                spinePath = "Effect_FishBox03";
                startVector = FishInfoController.Instance.dashparent.threeBoxImage.transform.position;
                break;
        }

        fispine = Resources.Load<GameObject>(spinePath);
        _boxSpine = Instantiate(fispine);
        
        _boxSpine.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        _boxSpine.transform.position = startVector;
        _boxSpine.gameObject.SetActive(true);
        AudioManager.Instance.PlaySoundEffect("FishBoxFly");
        _boxSpine.GetComponent<SkeletonAnimation>().AnimationState.SetAnimation(0, "out", false);
        _boxSpine.GetComponent<SkeletonAnimation>().DOPlay();
        _boxSpine.transform.DOScale(new Vector3(0.7f, 0.7f, 0.7f), 0.8f);
        yield return new WaitForSeconds(0.4f);
        _boxSpine.GetComponent<SkeletonAnimation>().AnimationState.SetAnimation(0, "idle01", true);
        _boxSpine.GetComponent<SkeletonAnimation>().DOPlay();
        yield return new WaitForSeconds(0.1f);
        _boxSpine.gameObject.SetActive(false);
        _boxSpine.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f) ;
        CustomFlyInManager.Instance.FlyIn(_boxSpine.transform,boxImageParent.transform,_boxSpine, () =>
        {
            _boxSpine.gameObject.SetActive(true);
            _boxSpine.transform.SetParent(boxImageParent);
            _boxSpine.transform.localScale = new Vector3(100, 100, 100);
            _boxSpine.transform.localPosition = Vector3.zero;
        },0.5f);
        yield return new WaitForSeconds(0.5f);
        _boxSpine.GetComponent<SkeletonAnimation>().AnimationState.SetAnimation(0, "end", false);
        _boxSpine.GetComponent<SkeletonAnimation>().DOPlay();
        yield return new WaitForSeconds(0.5f);
        AudioManager.Instance.PlaySoundEffect("FishBoxOpen");
        yield return new WaitForSeconds(0.5f);
        _boxSpine.GetComponent<SkeletonAnimation>().AnimationState.SetAnimation(0, "idle02", true);
        _boxSpine.GetComponent<SkeletonAnimation>().DOPlay();
    }

    private void InitUI()
    {
        closeBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ADPopReceive");
        wordtips.text = MultilingualManager.Instance.GetString("CarpMatchVictory");
        _awards = FishInfoController.Instance.GetAwardItems();
        GetAwardValue();

        StartCoroutine(PlayAwardAnim());
    }

    private void GetAwardValue()
    {
        foreach (var award in _awards)
        {
            LimitRewordType type = (LimitRewordType)award[0];
            string message = "竞速获得";
            switch (type)
            {
                case LimitRewordType.Coins:
                    // ★ 只入账、不刷 UI：顶栏数字留到金币飞入到达后再刷新（见 RefreshGoldUIAfterFlyIn）。
                    //   原来是 UpdateGold(count, isanim:true, updateui:true)，界面一打开数字就先滚一遍，
                    //   点「领取」金币再飞一遍，观感上就是「两段金币飞入」。
                    _goldReward += award[1];
                    GameDataManager.Instance.UserData.UpdateGold(award[1], false, false, message);
                    break;
                case LimitRewordType.Butterfly:
                    GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Butterfly, award[1],message);
                    break;
                case LimitRewordType.Tipstool:
                    GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.Tipstool, award[1],message);
                    break;
                case LimitRewordType.AutoComplete:
                    GameDataManager.Instance.UserData.UpdateTool(LimitRewordType.AutoComplete, award[1],message);
                    break;
            }
        }
    }

    /// <summary>
    /// 播放奖励动画
    /// </summary>
    /// <returns></returns>
    private IEnumerator PlayAwardAnim()
    {
        yield return new WaitForSeconds(0.01f);
        _hasGold = false;
        foreach (var award in _awards)
        {
            LimitRewordType type = (LimitRewordType)award[0];
            // 修正局部变量命名
            Transform awarditem = _objectPool.GetObject<Transform>(awarditemParent);
            awarditem.transform.localScale = Vector3.zero;
            awarditem.transform.position = boxImageParent.transform.position;
            
            Image awardIcon = awarditem.GetComponent<Image>();
            Text count = awarditem.GetComponentInChildren<Text>();
            count.text = award[1].ToString();
            switch (type)
            {
                case LimitRewordType.Coins:
                    awardIcon.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Coin2");
                    awardObj=awarditem;
                    _hasGold=true;
                    break;
                case LimitRewordType.Butterfly:
                    awardIcon.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Butterfly");
                    break;
                case LimitRewordType.Tipstool:
                    awardIcon.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Tips");
                    break;
                case LimitRewordType.AutoComplete:
                    awardIcon.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Reset");
                    break;
            }                
            
            awarditem.gameObject.SetActive(true);
            awarditem.transform.DOScale(Vector3.one, 0.4f);
            _awardobjs.Add(awarditem);
        }
    }

    /// <summary>
    /// 金币飞入金币栏（点「领取」时触发，只此一处）。落点用 ResolveAwardTarget 解析
    /// （注册表 → 商店图标 → 顶栏金币），解析不到就打日志并兜底刷新数字。
    /// 注意：这里刻意不做成 MonoBehaviour 协程 —— 面板紧跟着 Close，协程会被随对象禁用，
    /// 飞入与刷新回调都会静默丢失；飞行本身由常驻单例 CustomFlyInManager 的协程驱动。
    /// </summary>
    private void FlyCoinAward(Transform flyStart)
    {
        _hasGold = false;

        if (CustomFlyInManager.Instance == null)
        {
            Debug.LogWarning("[MatchSuccess] CustomFlyInManager 不存在，跳过金币飞入");
            RefreshGoldUIAfterFlyIn(); // 数字已经入账，兜底刷一次，避免顶栏一直显示旧值
            return;
        }
        Transform target = CustomFlyInManager.Instance.ResolveAwardTarget(LimitRewordType.Coins);
        if (target == null)
        {
            Debug.LogWarning("[MatchSuccess] 没有可用金币栏落点（GoldObj 未注册或不可见），跳过金币飞入");
            RefreshGoldUIAfterFlyIn();
            return;
        }

        Debug.Log($"[MatchSuccess] 金币飞入: start={flyStart.position} → target={target.position}");
        // 飞完之后再刷顶栏数字（与商店/TaskItem 同一套口径）：数字从旧值滚到新值，只做一次
        CustomFlyInManager.Instance.FlyInGoldToTarget(flyStart, target, RefreshGoldUIAfterFlyIn, 2);
    }

    /// <summary>
    /// 金币飞入到达后刷新金币栏：amount 传本局奖励额，数字从「旧值」滚到「新值」。
    /// 只在飞到之后调用（或没有落点时的兜底），保证界面生命周期内金币动效只有一段。
    /// </summary>
    private void RefreshGoldUIAfterFlyIn()
    {
        int amount = _goldReward;
        _goldReward = 0;
        if (amount <= 0) return;
        EventDispatcher.instance.TriggerChangeGoldUI(amount, true);
    }

    protected void InitButton()
    {
        closeBtn.AddVibraClickAction(OnCloseBtn); // 绑定关闭按钮事件
    }

    /// <summary>
    /// 侧滑（安卓返回键 / 鸿蒙返回手势）= 点「ADPopReceive」关闭按钮。
    ///
    /// 这个界面的收尾全在 OnCloseBtn 里：竞速时长打点（AnalyticMgr.ActivityComplete）、
    /// UpdateRound(1) 记局数、ResetFishData() 清本局数据、FishMatchOver() 交还竞速流程、
    /// 以及销毁飞入的宝箱 Spine。若侧滑让路由直接 HidePanel，这些一步都不会执行 ——
    /// 局数不加、本局数据不清、FishMatchOver 不触发，下一局竞速会带着上一局的状态进去。
    /// </summary>
    public void CloseFromBackGesture()
    {
        OnCloseBtn();
    }

    private void OnCloseBtn()
    {
        // 侧滑与按钮点击（或连点）可能在同一帧附近先后到达，防重复结算
        if (_closing) return;
        _closing = true;

        // 显式指定字符串区域性
        if (string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.roundstarttime)) 
            GameDataManager.Instance.FishUserSave.roundstarttime = DateTime.Now.ToString(System.Globalization.CultureInfo.InvariantCulture);
        
        TimeSpan ts = DateTime.Now.Subtract(DateTime.Parse(GameDataManager.Instance.FishUserSave.roundstarttime, System.Globalization.CultureInfo.InvariantCulture));
        //int progress = GameDataManager.MainInstance.FishUserSave.matchCount;
        AnalyticMgr.ActivityComplete("竞速活动",(int)ts.TotalSeconds);
        
        GameDataManager.Instance.FishUserSave.UpdateRound(1);
        GameDataManager.Instance.FishUserSave.ResetFishData();
        FishInfoController.Instance.FishMatchOver();
        
        // 宝箱 Spine 由 PlayBoxSpineAnim 异步创建，极端情况下（界面刚打开就被关）可能还没生成
        if (_boxSpine != null)
        {
            _boxSpine.gameObject.SetActive(false);
            Destroy(_boxSpine);
            _boxSpine = null;
        }
        
        // ★ 金币奖励：从奖励图标飞入金币栏（复用 CustomFlyInManager，落点用 ResolveAwardTarget 解析）
        //   起飞点必须在关面板之前取（奖励项随后回池），且刷数字由飞入回调完成 —— 全程只有一段金币动效
        if (_hasGold && awardObj != null)
            FlyCoinAward(awardObj);
        else
            RefreshGoldUIAfterFlyIn(); // 无金币奖励时 _goldReward=0，这里是安全空操作
      
        base.Close(); // 隐藏面板
    }

    private void HideAwardObjs()
    {
        foreach (var item in _awardobjs)
        {
            _objectPool.ReturnObjectToPool(item.GetComponent<PoolObject>());
            //Destroy(item);
        } 
    }

    public override void OnHideAnimationEnd()
    {
        HideAwardObjs();
        base.OnHideAnimationEnd();
    }

    // 移除冗余的重写方法
    // protected override void OnDisable()
    // {
    //     base.OnDisable();
    // }
}




