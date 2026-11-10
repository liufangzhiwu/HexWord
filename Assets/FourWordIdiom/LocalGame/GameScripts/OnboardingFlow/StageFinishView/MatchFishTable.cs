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
        
            if (string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.opentime))
            {
                DateTime openTime = DateTime.Now;
                //DateTime openTime = DateTime.Today.AddDays(1);
                // 计算本周五的日期（如果今天已经过了周五，则计算下周五）
                DateTime closeTime = DateTime.Today.AddDays((DayOfWeek.Friday - DateTime.Now.DayOfWeek + 7) % 7);
                
                DayOfWeek dayOfWeek = openTime.DayOfWeek;      // 获取星期几
                // 如果是周五、周六或周日，则调整到下周一
                if (dayOfWeek == DayOfWeek.Friday || dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
                {
                    // 计算到下周一的天数差
                    int daysUntilMonday = ((int)DayOfWeek.Monday - (int)dayOfWeek + 7) % 7;
                    closeTime = openTime.AddDays(daysUntilMonday);
                }
                
                GameDataManager.Instance.FishUserSave.opentime = openTime.ToString();
                GameDataManager.Instance.FishUserSave.cloestime = closeTime.ToString();
                FishInfoController.Instance.UpdateFishTime();
                FishBtn.gameObject.SetActive(true);
                FishBtn.gameObject.transform.parent.gameObject.SetActive(true);
            }
            else
            {
                bool fishOpen = FishInfoController.Instance.GetOpenFishFunction();
                FishBtn.gameObject.SetActive(fishOpen);
                FishBtn.gameObject.transform.parent.gameObject.SetActive(fishOpen);
                
                if (FishInfoController.Instance.IsOpen())
                {
                    bool isJustUnlocked = false;
                    // 主要针对模式2（拼字玩法）进行防蹭分拦截
                    if (GameDataManager.Instance.UserData.levelMode == 2) 
                    {
                        int playedStage = ChessStageController.Instance.CurrStageData.StageId; // 刚打完的关卡
                        int unlockStage = AppGameSettings.UnlockRequirements.FishOpenLevel;    // 解锁关卡
                    
                        if (playedStage == unlockStage - 1 && GameDataManager.Instance.UserData.CurrentChessStage == unlockStage)
                        {
                            isJustUnlocked = true;
                            Debug.Log($"[捕鱼防蹭分] 刚打完第{playedStage}关新鲜解锁捕鱼，绝不将本局进度白送给比赛！");
                        }
                    }
                    
                    if (isJustUnlocked) return; 
                    if(GameDataManager.Instance.UserData.levelMode == 1)
                        GameDataManager.Instance.FishUserSave.UpdateFishProgress(StageHexController.Instance.LimitPuzzlecount);
                    else if(GameDataManager.Instance.UserData.levelMode == 2)
                        GameDataManager.Instance.FishUserSave.UpdateFishProgress(ChessStageController.Instance.CurrStageInfo.PhraseGroups.Count);
                    else if(GameDataManager.Instance.UserData.levelMode == 3)
                        GameDataManager.Instance.FishUserSave.UpdateFishProgress(StageHexController.Instance.LimitPuzzlecount); // HexWord 六边形模式补回
                }
            }
        
    }
    

    public void UpdateFishTime(string time="")
    {
        //  1. 拦截未解锁状态
        if (!FishInfoController.Instance.IsOpen())
        {
            // 假设你的时间文本叫 fishtimetext
            fishTime.gameObject.SetActive(true);
            fishTime.text = $"第{AppGameSettings.UnlockRequirements.FishOpenLevel}关";
            if (fishTime.transform.parent != null)
            {
                Image bg = fishTime.transform.parent.GetComponent<Image>();
                if (bg != null) bg.enabled = true;
            }
            rankimage.gameObject.SetActive(false);
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
        // 1. 核心拦截：如果未解锁，全面隐藏
        if (!FishInfoController.Instance.IsOpen())
        {
            fishTime.gameObject.SetActive(true);
            fishTime.text = $"第{AppGameSettings.UnlockRequirements.FishOpenLevel}关";
            if (fishTime.transform.parent != null)
            {
                Image bg = fishTime.transform.parent.GetComponent<Image>();
                if (bg != null) bg.enabled = false;
            }
            rankimage.gameObject.SetActive(false);
            claimObj.gameObject.SetActive(false);
            fishwifiimage.gameObject.SetActive(false);
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
