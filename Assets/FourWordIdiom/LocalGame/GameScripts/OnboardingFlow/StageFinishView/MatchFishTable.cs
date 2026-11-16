using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class MatchFishTable : MonoBehaviour
{
    public Button FishBtn;
    public GameObject LockObj; // 挂载锁头物体
    public Animator animator;
    public GameObject matchEffect;
    [SerializeField] private Text fishTime;
    [SerializeField] private Image matchfishImage;
    [SerializeField] private Image fishwifiimage;
    [SerializeField] private Image rankimage;
    [SerializeField] private Text rankcount;
    [SerializeField] private GameObject claimObj;
    [SerializeField] private Text claimText;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        FishBtn.AddClickAction(OnFishClick);
    }
    
    public void OnFishClick()
    {
        if (GameCoreManager.Instance.IsNetworkActive)
        {
            if (string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.roundstarttime))
            {
                SystemManager.Instance.ShowPanel(PanelType.CompetitionStart);
            }
            else
            {
                SystemManager.Instance.ShowPanel(PanelType.DashCompetition);
            }
        }
        else
        {
            MessageSystem.Instance.ShowTip(MultilingualManager.Instance.GetString("RestorePurchasesTips01"), false);
        }
    }
    
    public void CheckFishBtn()
    {
        claimObj.gameObject.SetActive(false);
        if (!FishBtn.interactable) return;

        // 时间窗初始化统一交给 FishInfoController（首页/结算页共用一份，避免各写一套）
        FishInfoController.Instance.EnsureFishTimeWindow();

        bool fishOpen = FishInfoController.Instance.GetOpenFishFunction();
        FishBtn.gameObject.SetActive(fishOpen);
        FishBtn.gameObject.transform.parent.gameObject.SetActive(fishOpen);

        if (!FishInfoController.Instance.IsOpen()) return;

        bool isJustUnlocked = false;
        // 主要针对模式2（拼字玩法）进行防蹭分拦截
        if (GameDataManager.Instance.UserData.levelMode == 2)
        {
            int playedStage = ChessStageController.Instance.CurrStageData.StageId; // 刚打完的关卡
            int unlockStage = AppGameSettings.UnlockRequirements.FishOpenLevel;    // 解锁关卡（通关第 38 关后，进度到 39）

            // 进度语义：通关 N 关后 CurrentChessStage = N+1，
            // 所以「刚打完 38 关、进度变 39」这一局就是新鲜解锁的结算局
            if (playedStage == unlockStage - 1 && GameDataManager.Instance.UserData.CurrentChessStage == unlockStage)
            {
                isJustUnlocked = true;
                Debug.Log($"[捕鱼防蹭分] 刚打完第{playedStage}关新鲜解锁捕鱼，绝不将本局进度白送给比赛！");
            }
        }

        if (isJustUnlocked) return;
        if (GameDataManager.Instance.UserData.levelMode == 1)
            GameDataManager.Instance.FishUserSave.UpdateFishProgress(StageHexController.Instance.LimitPuzzlecount);
        else if (GameDataManager.Instance.UserData.levelMode == 2)
            GameDataManager.Instance.FishUserSave.UpdateFishProgress(ChessStageController.Instance.CurrStageInfo.PhraseGroups.Count);
        else if (GameDataManager.Instance.UserData.levelMode == 3)
            GameDataManager.Instance.FishUserSave.UpdateFishProgress(StageHexController.Instance.LimitPuzzlecount); // HexWord 六边形模式补回
    }

    /// <summary>
    /// 刷新结算页竞速入口三态：隐藏 / 带锁显示（「第X关」）+ 不可点 / 解锁显示。
    /// 口径：通关第 34 关后（进度变 35）露出带锁入口；通关第 38 关后（进度变 39）解锁；
    ///      未解锁态不受竞速周时间窗影响，已解锁态沿用活动时间窗。
    /// 说明：拼字结算页（ChessFinishView）与六边形结算页（StageFinishView）共用这一份逻辑。
    /// </summary>
    public void RefreshEntryState()
    {
        if (FishBtn == null || GameDataManager.Instance == null || GameDataManager.Instance.UserData == null) return;

        int progress = Mathf.Max(GameDataManager.Instance.UserData.CurrentHexStage,
                                 GameDataManager.Instance.UserData.CurrentChessStage);
        bool isUnlocked = FishInfoController.Instance.IsOpen();
        bool isShow = progress > AppGameSettings.ShowRequirements.FishShowLevel || isUnlocked;

        bool isVisible = isShow;
        if (isShow && isUnlocked)
        {
            // 新玩家先补一份竞速时间窗，否则 GetOpenFishFunction() 恒 false → 入口永远不出现
            FishInfoController.Instance.EnsureFishTimeWindow();
            isVisible = FishInfoController.Instance.GetOpenFishFunction();
        }

        Transform parent = FishBtn.transform.parent;
        if (parent != null) parent.gameObject.SetActive(isVisible);
        if (!isVisible) return;

        bool isLocked = !isUnlocked;
        FishBtn.interactable = !isLocked;
        if (LockObj != null) LockObj.SetActive(isLocked);

        Image fishIcon = matchfishImage;
        if (fishIcon != null)
        {
            Color c = fishIcon.color;
            c.a = isLocked ? 0.5f : 1f;
            fishIcon.color = c;
        }

        if (isLocked)
        {
            FishBtn.gameObject.SetActive(true);
            ShowLockedState();
        }
        else CheckFishBtn();
    }

    /// <summary>
    /// 未解锁态渲染：锁图标 + 「第X关」提示文案，并收起排名 / 领取 / 网络等状态。
    /// 说明：fishTime 预制体默认带残留时间文本，锁态必须显式刷新，否则会显示成旧的时间串。
    /// </summary>
    /// <param name="showTimeBg">是否显示 fishTime 的底板 Image</param>
    public void ShowLockedState(bool showTimeBg = true)
    {
        if (FishBtn != null) FishBtn.interactable = false;

        string lockTip = $"第{AppGameSettings.UnlockRequirements.FishOpenLevel}关";
        if (fishTime != null)
        {
            fishTime.gameObject.SetActive(true);
            fishTime.text = lockTip;
        }

        if (rankimage != null) rankimage.gameObject.SetActive(false);
        if (claimObj != null) claimObj.gameObject.SetActive(false);
        if (fishwifiimage != null) fishwifiimage.gameObject.SetActive(false);

        if (LockObj != null)
        {
            LockObj.SetActive(true);
            Text lockText = LockObj.GetComponentInChildren<Text>(true);
            if (lockText != null) lockText.text = lockTip;
        }
    }
    

    public void UpdateFishTime(string time="")
    {
        //  1. 拦截未解锁状态（锁图标 + 「第X关」文案）
        if (!FishInfoController.Instance.IsOpen())
        {
            ShowLockedState(true);
            return; 
        }
        //  2. 正常更新时间，控制自身显隐与底板 Image
        bool shouldActivate = !string.IsNullOrEmpty(time);
        fishTime.gameObject.SetActive(shouldActivate);
        if (shouldActivate) fishTime.text = time;

        if (fishTime.transform.parent != null)
        {
            Image bg = fishTime.transform.parent.GetComponent<Image>();
            if (bg != null) bg.enabled = shouldActivate;
        }

        if (FishInfoController.Instance.IsOpen())
        {
            FishBtn.gameObject.SetActive(FishInfoController.Instance.GetOpenFishFunction());
        }
    }
    
    public void UpdateFishRank()
    {
        // 1. 核心拦截：如果未解锁，全面隐藏（锁图标 + 「第X关」文案）
        if (!FishInfoController.Instance.IsOpen())
        {
            ShowLockedState(false);
            return;
        }
        
         if (GameCoreManager.Instance.IsNetworkActive)
         {
             fishwifiimage.gameObject.SetActive(false);
             if (GameDataManager.Instance.FishUserSave.rank > 0&& !string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.roundstarttime))
             {
                 int rank = GameDataManager.Instance.FishUserSave.rank;
                 switch (rank)
                 {
                     case 1:
                         rankimage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fishbtnred");
                         break;
                     case 2:
                         rankimage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fishbtnblue");
                         break;
                     case 3:
                         rankimage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fishbtngreen");
                         break;
                     case 4:
                     case 5:
                         rankimage.sprite = AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas("fishbtngrey");
                         break;
        
                 }

                 if (!FishInfoController.Instance.RoundFishIsOver())
                 {
                     fishTime.gameObject.SetActive(true);
                     if (fishTime.transform.parent != null)
                     {
                         Image bg = fishTime.transform.parent.GetComponent<Image>();
                         if (bg != null) bg.enabled = true;
                     }
                     fishTime.transform.parent.gameObject.SetActive(true); 
                     rankimage.gameObject.SetActive(true);
                     rankcount.text = rank.ToString();
                 }
                 else
                 {
                     claimObj.gameObject.SetActive(true);
                     rankimage.gameObject.SetActive(false);
                     fishTime.transform.parent.gameObject.SetActive(false);
                     fishTime.gameObject.SetActive(false);
                     if (fishTime.transform.parent != null)
                     {
                         Image bg = fishTime.transform.parent.GetComponent<Image>();
                         if (bg != null) bg.enabled = false;
                     }
                 }
                
             }
             else
             {
                 fishTime.transform.parent.gameObject.SetActive(true);
                 rankimage.gameObject.SetActive(false);
                 claimObj.gameObject.SetActive(false);
             }
         }
        else
        {
            fishTime.transform.parent.gameObject.SetActive(true);
            rankimage.gameObject.SetActive(false);
            fishwifiimage.gameObject.SetActive(true);
            claimObj.gameObject.SetActive(false);
        }
    }
    
    
    /// <summary>
    /// 播放竞速进度更新动画
    /// </summary>
    public void ShowFishWordAnim()
    { 
        GameObject taskyu = Instantiate(matchfishImage.gameObject,matchfishImage.transform);
        taskyu.transform.localScale=new Vector3(0.8f,0.8f,0.8f);
        taskyu.transform.SetAsLastSibling();
        taskyu.transform.localPosition=new Vector3(-165f,165f,0f);
        CanvasGroup canvas = taskyu.GetComponent<CanvasGroup>();
        if (canvas == null)
        {
            canvas = taskyu.AddComponent<CanvasGroup>();
        }
        canvas.alpha = 0f;
        var midPos = (matchfishImage.transform.localPosition + taskyu.transform.localPosition) / 2;
        var BezierMidPos = (midPos + taskyu.transform.localPosition) / 2 + Vector3.left * 50;
        Vector3[] MovePoints = CustomFlyInManager.Instance.CreatTwoBezierCurve(taskyu.transform.localPosition,matchfishImage.transform.localPosition,BezierMidPos).ToArray();
        
        canvas.DOFade(1, 0.3f).OnComplete(() =>
        {
            taskyu.transform.DOLocalPath(MovePoints, 0.5f).OnComplete(() =>
            {
                matchEffect.gameObject.SetActive(true);
                Destroy(taskyu);
            });
            
            canvas.DOFade(1, 0.4f).OnComplete(() =>
            {
                AudioManager.Instance.PlaySoundEffect("levelOverLimitwordAward");
                matchfishImage.transform.DOScale(new Vector3(1.2f,1.15f,1.15f), 0.3f).OnComplete(() =>
                {
                    matchfishImage.transform.DOScale(Vector3.one, 0.2f);
                });
            });
        });
    }
}
