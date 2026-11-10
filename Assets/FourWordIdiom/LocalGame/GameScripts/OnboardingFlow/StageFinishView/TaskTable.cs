using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class TaskTable : MonoBehaviour
{
    public Button TaskBtn;
    public GameObject LockObj; // 锁图标节点（同步自 WT 1.9.8，预制体未挂则为空）
    [SerializeField] private Image TaskOver;
    public GameObject taskEffect;
    public Text taskTime;
    [SerializeField] private Image taskyezi;
    [SerializeField] private GameObject TaskClaim;

    private void Start()
    {
        InitButton();
    }
    
    private void InitButton()
    {
        TaskBtn.AddClickAction(OnTaskClick);
    }
    
    private void OnTaskClick()
    {
        SystemManager.Instance.ShowPanel(PanelType.DailyTasksScreen);
        AchievementManager.Instance.DisableFinishAchieveTable();
    }
    
    public void CheckTasksScreen()
    {
        
        if (GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.DailyMissions
            || GameDataManager.Instance.UserData.CurrentChessStage >= AppGameSettings.UnlockRequirements.DailyMissions
            ||!string.IsNullOrEmpty(GameDataManager.Instance.FishUserSave.opentime))
        {
            DailyTaskManager.Instance.OnDailyTaskBtnUI += InitTaskBtnUI;                
            TaskBtn.gameObject.SetActive(true);
            
            if (GameDataManager.Instance.UserData.levelMode == 1)
            {
                DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedFindWord,StageHexController.Instance.LimitPuzzlecount);

            }else if (GameDataManager.Instance.UserData.levelMode == 2)
            {
                int puzzlecount = ChessStageController.Instance.LimitPuzzleCount;
                Debug.Log("找出的词语数量: "+puzzlecount );
                DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedFindWord,puzzlecount);
            }
            if (GameDataManager.Instance.UserData.levelMode == 3)
            {
                DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedFindWord,StageHexController.Instance.LimitPuzzlecount);
            }
            
            if(DailyTaskManager.Instance.UpdatetaskItem.Count > 0)
                StartCoroutine(ShowTaskWordAnim());
          
        }
    }
    
    /// <summary>
    /// 播放任务进度更新动画
    /// </summary>
    IEnumerator ShowTaskWordAnim()
    { 
        // 原为 1 秒盲等，纯空转；改为短衔接，避免结算页在这里卡住
        yield return new WaitForSeconds(FinishFlowTiming.TaskAnimLead);
        GameObject taskye = Instantiate(taskyezi.gameObject,taskyezi.transform);
        taskye.transform.localScale=new Vector3(0.8f,0.8f,0.8f);
        taskye.transform.SetAsLastSibling();
        taskye.transform.localPosition=new Vector3(165f,165f,0f);
        CanvasGroup canvas = taskye.GetComponent<CanvasGroup>();
        if (canvas == null)
        {
            canvas = taskye.AddComponent<CanvasGroup>();
        }
        canvas.alpha = 0f;
        var midPos = (taskyezi.transform.localPosition + taskye.transform.localPosition) / 2;
        var BezierMidPos = (midPos + taskye.transform.localPosition) / 2 + Vector3.left * 50;
        Vector3[] MovePoints = CustomFlyInManager.Instance.CreatTwoBezierCurve(taskye.transform.localPosition,taskyezi.transform.localPosition,BezierMidPos).ToArray();
        
        canvas.DOFade(1, 0.3f).OnComplete(() =>
        {
            taskye.transform.DOLocalPath(MovePoints, 0.5f).OnComplete(() =>
            {
                taskEffect.gameObject.SetActive(true);
                Destroy(taskye);
                InitTaskBtnUI();   
            });
            
            canvas.DOFade(1, 0.4f).OnComplete(() =>
            {
                AudioManager.Instance.PlaySoundEffect("levelOverLimitwordAward");
                taskyezi.transform.DOScale(new Vector3(1.2f,1.15f,1.15f), 0.3f).OnComplete(() =>
                {
                    taskEffect.gameObject.SetActive(false);
                    taskyezi.transform.DOScale(Vector3.one, 0.2f);
                });
            });
        });
    }
    
    private void InitTaskBtnUI()
    {
        if (!DailyTaskManager.Instance.IsAllComplete())
        {
            if (!DailyTaskManager.Instance.IsClaim())
            {
                if (TaskClaim.activeSelf)
                {
                    TaskClaim.gameObject.SetActive(false);
                    TaskClaim.GetComponent<CanvasGroup>().alpha = 0;
                }
            }
            else
            {
                TaskClaim.gameObject.SetActive(true);
                TaskClaim.GetComponent<CanvasGroup>().DOFade(1,0.2f);
            }
            TaskOver.gameObject.SetActive(false);
        }
        else
        {
            TaskClaim.gameObject.SetActive(false);
            TaskOver.gameObject.SetActive(true);
        }
    }
    
    
    public void UpdateTimeDisplay(string time)
    {
        if (!string.IsNullOrEmpty(time))
        {
            taskTime.text = time; // 更新文本
        }
    }

    private void OnDisable()
    {
        if (GameDataManager.Instance == null)
        {
            return;
        }
        
        if (GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.DailyMissions)
        {
            DailyTaskManager.Instance.OnDailyTaskBtnUI -= InitTaskBtnUI;              
           
        }
    }
}
