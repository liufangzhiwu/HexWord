using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
//using UnityEngine.Purchasing;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class TaskItem : MonoBehaviour
{
    public Animator animator;
    [SerializeField] private Image taskIcon;
    [SerializeField] private Text taskTitle;
    [SerializeField] private Text progressText;
    [SerializeField] private Slider slider;
    [SerializeField] private Image giftIcon;
    private TaskDataItem taskDataItem;
    public TaskSaveData taskSaveData;
    private ObjectPool objectPool; // 对象池实例

    public void SetTaskData(TaskSaveData data,ObjectPool bjectPool)
    {
        taskSaveData = data;
        taskDataItem = DailyTaskManager.Instance.GetTaskItem(data.taskid);
        objectPool=bjectPool;
        InitUI();
    }

    public void InitUI()
    {
        if(taskDataItem==null) return;

        int maxvalue = taskDataItem.values[taskSaveData.typeid];
        taskIcon.sprite = LoadtaskIcon(taskDataItem.iconname);
        //taskIcon.SetNativeSize();
        string des = MultilingualManager.Instance.GetString(taskDataItem.des);

        if (des.Contains("重置"))
        {
            des = des.Replace("重置","火箭");
        }
        taskTitle.text = string.Format(des, maxvalue); // 假设 productContent 是数量
        if (taskSaveData.progressvalue > maxvalue)
        {
            progressText.text = maxvalue+"/"+ maxvalue; 
        }
        else
        {
            progressText.text = taskSaveData.progressvalue+"/"+ maxvalue; 
        }
       
        float progress = (float)taskSaveData.progressvalue/maxvalue;
        
        if (DailyTaskManager.Instance.isResetDailyTask)
        {
            slider.value=progress;
        }
        else
        {
            slider.DOValue(progress,0.5f);
        }
       
        StartCoroutine(CheckGetRewards());

    }
    
    IEnumerator CheckGetRewards()
    {
        if (taskSaveData.iscomplete && !taskSaveData.iscliam)
        {
            int rewardvalue = taskDataItem.rewards[taskSaveData.typeid];
            GameDataManager.Instance.UserData.UpdateGold(rewardvalue, true,true,"任务获得");
            taskSaveData.iscliam = true;
            AnalyticMgr.TaskCompleted(taskSaveData.taskid.ToString(),rewardvalue);
            UpdateNextTask();  
            yield return new WaitForSeconds(0.2f);
                  
            CustomFlyInManager.Instance.FlyInGold(giftIcon.transform,() =>
            {                
                //播放翻转动画
                animator.SetBool("isUpdate", true);                
                AudioManager.Instance.PlaySoundEffect("StageFinish");    
            });
            
            yield return new WaitForSeconds(0.9f);
            slider.value=0;           

            if (taskDataItem != null)
            {
                InitUI();
            }
            DailyTaskManager.Instance.UpdateDailyTaskBtnUI();
        }
    }

    public void UpdateNextTask()
    {
        //更新完成任务
        GameDataManager.Instance.UserData.UpdateCompleteTask(taskSaveData.taskid, taskSaveData.typeid);
        TimeSpan ts = DateTime.Now.Subtract(DateTime.Today);
        int completecount=GameDataManager.Instance.UserData.completeTaskList.Count;
        AnalyticMgr.ActivityProgress("每日任务", completecount, (int)ts.TotalSeconds);
        
        //更新到下一个任务
        if (taskSaveData.typeid < taskDataItem.rewards.Count - 1)
        {
            int rage = Random.Range(0, 2);
            bool leftcountCancomplete = true;
            if ((TaskEvent)taskDataItem.id == TaskEvent.NeedLightLimit)
            {
                int leftlimitcount = LimitTimeManager.Instance.GetLimitItems().Count - GameDataManager.Instance.UserData.timePuzzlecount;
                if (leftlimitcount < taskDataItem.values[taskSaveData.typeid + 1])
                {
                    leftcountCancomplete = false;
                }
            }
            int leftprogress = taskSaveData.progressvalue;
            if (leftcountCancomplete && rage == 0)
            {
                taskSaveData.AddTypeidTask();
                if ((TaskEvent)taskSaveData.taskid == TaskEvent.NeedOnlineTime && AppGameSettings.SaveOnlineTimeProgress)
                {
                    taskSaveData.progressvalue = leftprogress;
                }

                if ((TaskEvent)taskSaveData.taskid != TaskEvent.NeedOnlineTime && AppGameSettings.SaveMissionProgress)
                {
                    taskSaveData.progressvalue = leftprogress;
                }

                if ((TaskEvent)taskSaveData.taskid == TaskEvent.NeedOnlineTime)
                {
                    DailyTaskManager.Instance.CheckOnlineTimeTask(taskSaveData);
                }
            }
            else
            {
                //重置任务
                taskSaveData = DailyTaskManager.Instance.GetSigleTaskSaveData(taskSaveData.taskid);
                taskDataItem = DailyTaskManager.Instance.GetTaskItem(taskSaveData.taskid);
            }
        }
        else
        {
            //重置任务
            taskSaveData = DailyTaskManager.Instance.GetSigleTaskSaveData(taskSaveData.taskid);
            if (taskSaveData != null)
            {
                taskDataItem = DailyTaskManager.Instance.GetTaskItem(taskSaveData.taskid);
            }
            else
            {                
                taskDataItem = null;
                objectPool.ReturnObjectToPool(transform.GetComponent<PoolObject>());
            }
        }
    }

    private Sprite LoadtaskIcon(string showIcon)
    {
        return AdvancedBundleLoader.SharedInstance.GetSpriteFromAtlas(showIcon);
    }

    private void OnDisable()
    {      
        DailyTaskManager.Instance.UpdateDailyTaskBtnUI();
        animator.SetBool("isUpdate",false);
        transform.gameObject.SetActive(false);
    }
}