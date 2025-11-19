using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

public class MatchSuccess : UIWindow
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
    //private GameObject awardObj;

    // 修正私有字段命名
    private List<Transform> _awardobjs = new List<Transform>();
    private List<List<int>> _awards = new List<List<int>>();

    protected void Start()
    {
        // 初始化对象池
        _objectPool = new ObjectPool(awardIconprofab.gameObject, ObjectPool.CreatePoolContainer(transform, "RewardItemPool"));

        //switch (GameDataManager.instance.UserData.LanguageCode)
        //{
        //    case "Japanese":
        //        titleImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("riMatchWin");
        //        break;
        //    case "ChineseTraditional":
        //        titleImage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fanMatchWin");
        //        break;
        //}
        InitButton();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        InitUI();
        StartCoroutine(PlayBoxSpineAnim());
        DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedWinMatch, 1);
        AudioManager.Instance.PlaySoundEffect("ShowUI");
    }

    IEnumerator PlayBoxSpineAnim()
    {
        // 移除未使用的初始化值
        // GameObject fispine;
        // string spinePath = "Effect_FishBox01";
        // // 修正局部变量命名
        // Vector3 startVector = FishInfoController.Instance.dashparent.threeBoxImage.transform.position;
        // // switch (GameDataManager.instance.FishUserSave.rank)
        // // {
        // //     case 1:
        // //         spinePath = "Effect_FishBox01";
        // //         startVector = FishInfoController.Instance.dashparent.oneBoxImage.transform.position;
        // //         break;
        // //     case 2:
        // //         spinePath = "Effect_FishBox02";
        // //         startVector = FishInfoController.Instance.dashparent.twoBoxImage.transform.position;
        // //         break;
        // //     case 3:
        // //         spinePath = "Effect_FishBox03";
        // //         startVector = FishInfoController.Instance.dashparent.threeBoxImage.transform.position;
        // //         break;
        // // }
        //
        // fispine = Resources.Load<GameObject>(spinePath);
        // _boxSpine = Instantiate(fispine);
        //
        // _boxSpine.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        // _boxSpine.transform.position = startVector;
        // _boxSpine.gameObject.SetActive(true);
        // AudioManager.Instance.PlaySoundEffect("FishBoxFly");
        // _boxSpine.GetComponent<SkeletonAnimation>().AnimationState.SetAnimation(0, "out", false);
        // _boxSpine.GetComponent<SkeletonAnimation>().DOPlay();
        // _boxSpine.transform.DOScale(new Vector3(0.7f, 0.7f, 0.7f), 0.8f);
         yield return new WaitForSeconds(0.4f);
        // _boxSpine.GetComponent<SkeletonAnimation>().AnimationState.SetAnimation(0, "idle01", true);
        // _boxSpine.GetComponent<SkeletonAnimation>().DOPlay();
        // yield return new WaitForSeconds(0.1f);
        // _boxSpine.gameObject.SetActive(false);
        // _boxSpine.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f) ;
        // CustomFlyInManager.Instance.FlyIn(_boxSpine.transform,boxImageParent.transform,_boxSpine, () =>
        // {
        //     _boxSpine.gameObject.SetActive(true);
        //     _boxSpine.transform.SetParent(boxImageParent);
        //     _boxSpine.transform.localScale = new Vector3(100, 100, 100);
        //     _boxSpine.transform.localPosition = Vector3.zero;
        // },0.5f);
        // yield return new WaitForSeconds(0.5f);
        // _boxSpine.GetComponent<SkeletonAnimation>().AnimationState.SetAnimation(0, "end", false);
        // _boxSpine.GetComponent<SkeletonAnimation>().DOPlay();
        // yield return new WaitForSeconds(0.5f);
        // AudioManager.Instance.PlaySoundEffect("FishBoxOpen");
        // yield return new WaitForSeconds(0.5f);
        // _boxSpine.GetComponent<SkeletonAnimation>().AnimationState.SetAnimation(0, "idle02", true);
        // _boxSpine.GetComponent<SkeletonAnimation>().DOPlay();
    }

    private void InitUI()
    {
        closeBtn.GetComponentInChildren<Text>().text = MultilingualManager.Instance.GetString("ADPopReceive");
        wordtips.text = MultilingualManager.Instance.GetString("CarpMatchVictory");
        //_awards = FishInfoController.Instance.GetAwardItems();
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
                    GameDataManager.instance.UserData.UpdateGold(award[1],true,false,message);
                    break;
                case LimitRewordType.Butterfly:
                    GameDataManager.instance.UserData.UpdateTool(LimitRewordType.Butterfly, award[1],message);
                    break;
                case LimitRewordType.Tipstool:
                    GameDataManager.instance.UserData.UpdateTool(LimitRewordType.Tipstool, award[1],message);
                    break;
                case LimitRewordType.Resettool:
                    GameDataManager.instance.UserData.UpdateTool(LimitRewordType.Resettool, award[1],message);
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
                    break;
                case LimitRewordType.Butterfly:
                    awardIcon.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Butterfly");
                    break;
                case LimitRewordType.Tipstool:
                    awardIcon.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Tips");
                    break;
                case LimitRewordType.Resettool:
                    awardIcon.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("Reset");
                    break;
            }                
            
            awarditem.gameObject.SetActive(true);
            awarditem.transform.DOScale(Vector3.one, 0.4f);
            _awardobjs.Add(awarditem);
        }
    }

    protected void InitButton()
    {
        closeBtn.AddClickAction(OnCloseBtn); // 绑定关闭按钮事件
    }
    
    private void OnCloseBtn()
    {
        // 显式指定字符串区域性
        // if (string.IsNullOrEmpty(GameDataManager.instance.FishUserSave.roundstarttime)) 
        //     GameDataManager.instance.FishUserSave.roundstarttime = DateTime.Now.ToString(System.Globalization.CultureInfo.InvariantCulture);
        //
        // TimeSpan ts = DateTime.Now.Subtract(DateTime.Parse(GameDataManager.instance.FishUserSave.roundstarttime, System.Globalization.CultureInfo.InvariantCulture));
        // //int progress = GameDataManager.instance.FishUserSave.matchCount;
        // ThinkManager.instance.Event_ActivityComplete("竞速活动",(int)ts.TotalSeconds);
        // //FirebaseManager.Instance.ActivityProgress("竞速活动",progress,(int)ts.TotalSeconds);
        //
        // GameDataManager.instance.FishUserSave.UpdateRound(1);
        // GameDataManager.instance.FishUserSave.ResetFishData();
        //FishInfoController.Instance.FishMatchOver();
        
        _boxSpine.gameObject.SetActive(false);
        Destroy(_boxSpine);
      
        //SystemManager.Instance.HidePanel(PanelType.MatchSuccess);
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




