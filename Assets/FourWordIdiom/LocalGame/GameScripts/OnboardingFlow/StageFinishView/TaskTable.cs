using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class TaskTable : MonoBehaviour
{
    public Button TaskBtn;
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
    }
    
    public void CheckTasksScreen()
    {
        if (GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.DailyMissions)
        {
            DailyTaskManager.Instance.OnDailyTaskBtnUI += InitTaskBtnUI;                
            TaskBtn.gameObject.SetActive(true);
            if(GameDataManager.Instance.UserData.CurrentHexStage > AppGameSettings.UnlockRequirements.DailyMissions)
            {
                DailyTaskManager.Instance.UpdateTaskProgress(TaskEvent.NeedFindWord,StageHexController.Instance.LimitPuzzlecount);
                
                if(DailyTaskManager.Instance.UpdatetaskItem.Count > 0)
                    StartCoroutine(ShowTaskWordAnim());
            }
        }
    }
    
    /// <summary>
    /// 播放任务进度更新动画
    /// </summary>
    IEnumerator ShowTaskWordAnim()
    { 
        yield return new WaitForSeconds(1f);
        GameObject taskye = Instantiate(taskyezi.gameObject,taskyezi.transform);
        taskye.transform.localScale=new Vector3(0.8f,0.8f,0.8f);
        taskye.transform.SetAsLastSibling();
        taskye.transform.localPosition=new Vector3(-165f,165f,0f);
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
        if (GameDataManager.Instance.UserData.CurrentHexStage >= AppGameSettings.UnlockRequirements.DailyMissions)
        {
            DailyTaskManager.Instance.OnDailyTaskBtnUI -= InitTaskBtnUI;              
           
        }
    }
}
