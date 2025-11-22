using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class MatchFishTable : MonoBehaviour
{
    public Button FishBtn;
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
            // if (string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.roundstarttime))
            // {
            //     SystemManager.Instance.ShowPanel(PanelType.CompetitionStart);
            // }
            // else
            // {
            //     SystemManager.Instance.ShowPanel(PanelType.DashCompetition);
            // }
        }
        else
        {
            MessageSystem.Instance.ShowTip(MultilingualManager.Instance.GetString("RestorePurchasesTips01"), false);
        }
    }
    
    public void CheckFishBtn()
    {
        claimObj.gameObject.SetActive(false);
        if (GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.FishOpenLevel)
        {
            if (GameDataManager.Instance.UserData.CurrentHexStage == AppGameSettings.UnlockRequirements.FishOpenLevel)
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
                
                // GameDataManager.Instance.FishUserSave.opentime = openTime.ToString();
                // GameDataManager.Instance.FishUserSave.cloestime = closeTime.ToString();
                //FishInfoController.Instance.UpdateFishTime();
                FishBtn.gameObject.SetActive(true);
            }
            else
            {
                //FishBtn.gameObject.SetActive(FishInfoController.Instance.GetOpenFishFunction());
                
                if (GameDataManager.Instance.UserData.CurrentHexStage > AppGameSettings.UnlockRequirements.FishOpenLevel)
                {
                    //GameDataManager.Instance.FishUserSave.UpdateFishProgress(StageController.Instance.CurStageInfo.Puzzles.Count);
                }
            }
        }
        else
        {
            FishBtn.gameObject.SetActive(false);
        }
    }
    

    public void UpdateFishTime(string time="")
    {
        fishTime.text = time;
        if (GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.FishOpenLevel)
        {
            //FishBtn.gameObject.SetActive(FishInfoController.Instance.GetOpenFishFunction());
        }  
    }
    
    public void UpdateFishRank()
    {
         if (GameCoreManager.Instance.IsNetworkActive)
         {
             fishwifiimage.gameObject.SetActive(false);
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
