using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class DailyTasksScreen : UIWindow
{
    [SerializeField] private Button closeBtn; // 关闭按钮
    [SerializeField] private Button HideBtn; // 隐私条款按钮
    [SerializeField] private TaskItem taskItemPrefab;
    [SerializeField] private Text headTitle; // 语言选择文本显示
    [SerializeField] private Text taskTitle; // 语言选择文本显示
    [SerializeField] private Text timeText; // 语言选择文本显示
    [SerializeField] private Text butterflyTitle; // 语言选择文本显示
    [SerializeField] private Text flySliderValue; // 语言选择文本显示
    [SerializeField] private Slider flySlider; // 语言选择文本显示
    [SerializeField] private Transform taskParent; // 语言选择文本显示
    [SerializeField] private Text taskOverText; // 语言选择文本显示
    [SerializeField] private Text closetips;
    
    private ObjectPool objectPool; // 对象池实例
    
    private Dictionary<TaskEvent,TaskItem> taskItems = new Dictionary<TaskEvent,TaskItem>();
    private int NeedItemsCount;
    
    
    protected void Start()
    {
        if (taskItemPrefab == null)
        {
            taskItemPrefab = AdvancedBundleLoader.SharedInstance.LoadGameObject("commonitem", "TaskItem").GetComponent<TaskItem>();
        }
        // 初始化对象池
        objectPool = new ObjectPool(taskItemPrefab.gameObject, ObjectPool.CreatePoolContainer(transform, "TaskItemPool"));
        
        //StartCoroutine(CrateTaskItem());
        InitButton();
    }
    
    private IEnumerator CrateTaskItem()
    {        
        ClearTaskItems();
        
        List<TaskSaveData> taskSaveDatas= new List<TaskSaveData>(DailyTaskManager.Instance.GetTaskSaveData());
        NeedItemsCount = taskSaveDatas.Count;
        
        yield return new WaitForSeconds(0.01f);
        
        // 从配置表中读取初始数据
        foreach (var taskSaveData in taskSaveDatas )
        {
            // 从对象池获取 ShopItem 对象
            TaskItem taskItem = objectPool.GetObject<TaskItem>(taskParent);
            
            if (taskItem.taskSaveData != null)
            {
                if (taskItem.taskSaveData.iscomplete && !taskItem.taskSaveData.iscliam)
                {
                    yield return new WaitForSeconds(0.2f);
                }
            }
            
            // 赋值 shopItem 的数据
            taskItem.SetTaskData(taskSaveData,objectPool);
            TaskEvent type = (TaskEvent)taskSaveData.taskid;
            if(!taskItems.ContainsKey(type))
                taskItems.Add(type, taskItem);
            
          
            
            yield return new WaitForSeconds(0.01f);
        }
    }
    
    protected override void OnEnable()
    {
        base.OnEnable();
        LimitTimeManager.Instance.OnDailyTimeUpdated += UpdateTimeDisplay; // 订阅事件
        DailyTaskManager.Instance.OnDailyTaskBtnUI += UpdateButterflyUI;
        
        // if (DailyTaskManager.Instance.isResetDailyTask&& taskItems.Count>0)
        // {
            StartCoroutine(CrateTaskItem());
        //}
        AudioManager.Instance.PlaySoundEffect("ShowUI");
        InitUI();
    }

    protected void InitButton()
    {
        closeBtn.AddClickAction(OnCloseBtn); // 绑定关闭按钮事件
        HideBtn.AddClickAction(OnCloseBtn);
    }

    private void InitUI()
    {
        headTitle.text = MultilingualManager.Instance.GetString("DailyTasksTiles");
        butterflyTitle.text = MultilingualManager.Instance.GetString("DailyTasksMainDes");
        taskTitle.text = MultilingualManager.Instance.GetString("DailyTasksTiles01");
        closetips.text = MultilingualManager.Instance.GetString("limitedRewardsDes05");
        if (taskItems.Count >= NeedItemsCount)
        {
            StartCoroutine(UpdateTaskItemUI());
        }
        
        //EventDispatcher.instance.TriggerUpdateLayerCoin(true,true);
        CheckTaskOverText();
    }

    private void CheckTaskOverText()
    {
        if (DailyTaskManager.Instance.IsAllComplete())
        {
            int ran = UnityEngine.Random.Range(0, 8);
            taskOverText.text = MultilingualManager.Instance.GetString("DailyTasksEndDes0" + ran);
            taskOverText.gameObject.SetActive(true);
        }
        else
        {
            taskOverText.gameObject.SetActive(false);
        }
    }

    private void UpdateButterflyUI()
    {
        int count = GameDataManager.Instance.UserData.completeTaskList.Count;
        if (count > 8) count = 8;
        flySliderValue.text = count+"/8";
        float value = count / 8.0f;
        if (DailyTaskManager.Instance.isResetDailyTask)
        {
            flySlider.value=value;
            //ThinkManager.instance.Event_ActivityBegin("每日任务");       
        }
        else
        {
            flySlider.DOValue(value, 0.5f);
        }
        
        // if (count>=8)
        // {
        //     DailyTaskManager.Instance.CheckOpenButterflyTask();
        //     
        //     CheckTaskOverText();
        // }
    }
    
    IEnumerator UpdateTaskItemUI()
    {
        List<TaskEvent> taskEvents = DailyTaskManager.Instance.UpdatetaskItem.ToList();

        foreach (var taskItem in taskItems)
        {
            taskItem.Value.gameObject.SetActive(true);
            taskItem.Value.animator.Play("Show");
            yield return new WaitForSeconds(0.1f);
        }
        
        foreach (var taskItem in taskItems)
        {
            if (taskEvents.Contains(taskItem.Key))
            {
                if (taskItem.Value.taskSaveData != null)
                {
                    if (taskItem.Value.taskSaveData.iscomplete && !taskItem.Value.taskSaveData.iscliam)
                    {
                        yield return new WaitForSeconds(0.4f);
                    }
                }

                taskItem.Value.InitUI();
                
            }
            
            yield return new WaitForSeconds(0.4f);
        }
        
        yield return new WaitForSeconds(0.5f);
        UpdateButterflyUI();
        //DailyTaskManager.Instance.UpdatetaskItem.Clear();
    }

    private void UpdateTimeDisplay(string time)
    {
        if (!string.IsNullOrEmpty(time))
        {
            timeText.text = time; // 更新文本
        }
    }
    
    private void OnCloseBtn()
    {
        base.Close(); // 隐藏面板
    }
    
    public override void OnHideAnimationEnd()
    {
        //DailyTaskManager.Instance.UpdatetaskItem.Clear();
        base.OnHideAnimationEnd();
    }
    
    private void ClearTaskItems()
    {
        foreach (TaskItem taskItem in taskItems.Values)
        {
            objectPool.ReturnObjectToPool(taskItem.GetComponent<PoolObject>());
        }

        taskItems.Clear();
        //CrateTaskItem();
    }

    protected override void OnDisable() 
    {
        LimitTimeManager.Instance.OnDailyTimeUpdated -= UpdateTimeDisplay; // 订阅事件
        DailyTaskManager.Instance.OnDailyTaskBtnUI -= UpdateButterflyUI;
        DailyTaskManager.Instance.isResetDailyTask=false;
        //EventDispatcher.instance.TriggerUpdateLayerCoin(false,true);
    }
}
