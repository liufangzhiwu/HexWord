using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class TaskDataItem
{
    public int id;
    //奖励内容
    public TaskType tasktype;
    //需要的成语数
    public string iconname;
    public string des;
    public List<int> values;
    public List<int> rewards;
    public int unlocklv;
}

//对应限时奖励配置表中奖励配置批准中的奖励索引表示的类型
public enum TaskEvent
{
    Null,NeedPassLevel,NeedFindWord,NeedUseTipsTool,NeedUseTipAllWordTool,NeedUseButterflyTool,NeedOnlineTime,
    NeedSeeAds,NeedLightLimit,NeedWinMatch,NeedShopBuy,
}

public enum TaskType
{
    Null,PassLevel,FindWord,UseTool,OnlineTime,ContinueAcitity,
}

public class DailyTaskManager : MonoBehaviour
{
    [HideInInspector]
    public List<TaskEvent> UpdatetaskItem=new List<TaskEvent>();
    private List<TaskDataItem> taskItems;
    public static DailyTaskManager Instance;
    public event System.Action OnDailyTaskBtnUI; // 定义事件
    public event System.Action<string> OnDailyButterflyTaskUI; // 定义事件
    [HideInInspector]
    public bool isResetDailyTask; // 是否重置每日任务
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 保持广告管理器在场景切换时不销毁
        }
        else
        {
            Destroy(gameObject);
        }       
    }

    void Start()
    {
        TextAsset data = AdvancedBundleLoader.SharedInstance.LoadTextFile("gameinfo", "dailytask");
        if (data != null)
        {
            ParseLimitItems(data.text);
        }
        else
        {
            Debug.LogError("Failed to load CSV data.");
        }

        StartCoroutine(CheckSaveTaskOnline());
    }

    IEnumerator CheckSaveTaskOnline()
    {
        yield return new WaitForEndOfFrame();
        
        // 从用户数据中读取
        foreach (TaskSaveData taskSave in GameDataManager.instance.UserData.taskSaveDatas)
        {
            TaskDataItem taskItem = taskItems[taskSave.taskid-1];
            if ((TaskEvent)taskItem.id == TaskEvent.NeedOnlineTime)
            {
                CheckOnlineTimeTask(taskSave);
            }
        }
        
        // if (GameDataManager.instance.UserData.butterflyTaskIsOpen)
        // {
        //     int leftminutes =AppGameSettings.TaskButterflyUseTime- GameDataManager.instance.UserData.taskButterflyUseMinutes;
        //     StartCoroutine(OnMaxButterlfyTask(leftminutes));
        // }
    }
    
    public void UpdateDailyTaskBtnUI()
    {
        OnDailyTaskBtnUI?.Invoke();
    }

    void ParseLimitItems(string data)
    {
        // 将 CSV 数据转换为 JSON 格式
        ConvertCSVToJSON(data);

        // 现在limitItems列表中包含所有商品
        Debug.Log("Limit items loaded: " + taskItems.Count);
    }

    private List<int> GetList(string[] groups)
    {
        // 解析 productContent
        List<int> productContent = new List<int>();
        foreach (string group in groups)
        {
            int value = int.Parse(group);
            productContent.Add(value); // 添加到主列表
        }
        
        return productContent;
    }

    void ConvertCSVToJSON(string data)
    {
        // 用于构建 JSON 字符串
        List<TaskDataItem> items = new List<TaskDataItem>();
        string[] lines = data.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 2; i < lines.Length; i++) // 从第一行开始，跳过标题行
        {
            string[] fields = lines[i].Split(',');

            if (fields.Length >= 3) // 确保有足够的字段
            {
                int id = int.Parse(fields[0].Trim());
                int type = int.Parse(fields[1].Trim());
                string icon = fields[3].Trim();
                string des = fields[4].Trim();
                // 先用 # 分隔
                string[] groups = fields[5].Split('#');
                List<int> values = GetList(groups);
                // 先用 # 分隔
                string[] regroups = fields[6].Split('#');
                List<int> rewards = GetList(regroups);
                int unlocklv = int.Parse(fields[7].Trim());

                TaskDataItem item = new TaskDataItem
                {
                    id = id,
                    tasktype = (TaskType)type,
                    iconname = icon,
                    des = des,
                    values = values,
                    rewards = rewards,
                    unlocklv=unlocklv
                };
                items.Add(item);
            }
            else
            {
                Debug.LogWarning($"Skipping line {i + 1}: Not enough fields.");
            }
        }
      
        taskItems = items;
    }
    
    public List<TaskDataItem> GetTaskItems()
    {
        return taskItems;
    }

    /// <summary>
    /// 检测是否开启在线任务
    /// </summary>
    /// <param name="saveData"></param>
    public void CheckOnlineTimeTask(TaskSaveData saveData)
    {
        if (!saveData.iscomplete)
        {
            TaskDataItem dataItem = taskItems[saveData.taskid - 1];
            int needminutes=dataItem.values[saveData.typeid]-saveData.progressvalue;
            StartCoroutine(OnLineTimeTask(needminutes));
        }
    }

    IEnumerator OnLineTimeTask(int minutes)
    {
        DateTime endTime = DateTime.Now.AddMinutes(minutes);
        TimeSpan timeSpan = endTime.Subtract(DateTime.Now);
        while (timeSpan.TotalMinutes>0)
        {
            yield return new WaitForSeconds(60);
            UpdateTaskProgress(TaskEvent.NeedOnlineTime, 1);
            timeSpan = endTime.Subtract(DateTime.Now);
            if (timeSpan.TotalMinutes <= 0)
            {
                yield break;
            }
        }
    }
    
    IEnumerator OnMaxButterlfyTask(int minutes)
    {
        DateTime endTime = DateTime.Now.AddMinutes(minutes);
        TimeSpan timeSpan = endTime.Subtract(DateTime.Now);
        yield return new WaitForSeconds(60);
        string fen=MultilingualManager.Instance.GetString("TimeM");
        string time = timeSpan.TotalMinutes.ToString("#")+fen;
        if(timeSpan.TotalMinutes > 0)
            OnDailyButterflyTaskUI?.Invoke(time);

        while (timeSpan.TotalMinutes>0)
        {
            yield return new WaitForSeconds(60);

            timeSpan = endTime.Subtract(DateTime.Now);
            //GameDataManager.instance.UserData.taskButterflyUseMinutes++;
            time = timeSpan.TotalMinutes.ToString("#")+fen;
            if (timeSpan.TotalMinutes <= 0)
            {
                // if (GameDataManager.instance.UserData.butterflyTaskIsOpen)
                // {
                //     GameDataManager.instance.UserData.butterflyTaskIsOpen=false;
                // } 
                yield break;
            }
            OnDailyButterflyTaskUI?.Invoke(time);
        }
    }

    /// <summary>
    /// 获取任务数据
    /// </summary>
    /// <returns></returns>
    public List<TaskSaveData> GetTaskSaveData()
    {
        List<TaskSaveData> taskSaveDatas = GameDataManager.instance.UserData.taskSaveDatas;

        if (GameDataManager.instance.UserData.taskSaveDatas.Count <= 0 && !IsAllComplete())
        {
            // 获取所有满足解锁条件的任务
            List<TaskDataItem> eligibleTasks = taskItems
                .Where(t => GameDataManager.instance.UserData.CurrentStage >= t.unlocklv)
                .ToList();

            // 随机打乱任务顺序
            System.Random rng = new System.Random();
            List<TaskDataItem> shuffledTasks = eligibleTasks.OrderBy(t => rng.Next()).ToList();

            foreach (TaskDataItem taskItem in shuffledTasks)
            {
                // 检查当前等级是否满足解锁条件
                if (taskSaveDatas.Count<3)
                {
                    // 检查是否已经存在相同类型的 TaskSaveData
                    bool exists = taskSaveDatas.Any(t => t.type == (int)taskItem.tasktype);

                    if (!exists)
                    {
                        TaskSaveData taskSave = new TaskSaveData()
                        {
                            taskid = taskItem.id,
                            type = (int)taskItem.tasktype,
                            typeid = 0, 
                            progressvalue = 0
                        };
                        
                        GameDataManager.instance.UserData.UpdateDailyTaskData(taskSave);

                        if (!UpdatetaskItem.Contains((TaskEvent)taskItem.id))
                        {
                            UpdatetaskItem.Add((TaskEvent)taskItem.id);
                        }

                        if ((TaskEvent)taskItem.id == TaskEvent.NeedOnlineTime)
                        {
                            CheckOnlineTimeTask(taskSave);
                        }
                    }
                }
            }
        }

        return taskSaveDatas;
    }
    
    public TaskSaveData GetSigleTaskSaveData(int taskid)
    {
        List<TaskSaveData> taskSaveDatas = GameDataManager.instance.UserData.taskSaveDatas;
        
        // 获取所有满足解锁条件的任务
        List<TaskDataItem> eligibleTasks = taskItems
            .Where(t => GameDataManager.instance.UserData.CurrentStage >= t.unlocklv)
            .ToList();
        
        // 随机打乱任务顺序
        System.Random rng = new System.Random();
        List<TaskDataItem> shuffledTasks = eligibleTasks.OrderBy(t => rng.Next()).ToList();
        
        TaskSaveData curtaskSave = taskSaveDatas.Find(t => t.taskid == taskid);
        //int leftprogress = curtaskSave.progressvalue;
        taskSaveDatas.Remove(curtaskSave);
        
        foreach (TaskDataItem taskItem in shuffledTasks)
        {
            // 检查是否已经存在相同类型的 TaskSaveData
            bool exists = taskSaveDatas.Any(t => t.type == (int)taskItem.tasktype);
            bool complete = false;
            int typeid = 0;
            List<CompleteTaskData> curpletetask = GameDataManager.instance.UserData.completeTaskList.FindAll(t => t.taskid == taskItem.id);

            if (curpletetask.Count >= taskItem.values.Count)
            {
                complete = true;
            }
            else
            {
                typeid = curpletetask.Count;
            }

            if ((TaskEvent)taskItem.id == TaskEvent.NeedLightLimit)
            {
                int leftlimitcount = LimitTimeManager.instance.GetLimitItems().Count - GameDataManager.instance.UserData.timePuzzlecount;
                if (leftlimitcount < taskItem.values[0])
                {
                    continue;
                }
            }

            if (!exists&&!complete)
            {
                TaskSaveData taskSave = new TaskSaveData()
                {
                    taskid = taskItem.id,
                    type = (int)taskItem.tasktype,
                    typeid = typeid,
                    progressvalue = 0,
                    iscomplete = false,
                    iscliam = false,
                };
                
                GameDataManager.instance.UserData.UpdateDailyTaskData(taskSave);
                
                if (!UpdatetaskItem.Contains((TaskEvent)taskItem.id))
                {
                    UpdatetaskItem.Add((TaskEvent)taskItem.id);
                }
                
                if ((TaskEvent)taskItem.id == TaskEvent.NeedOnlineTime)
                {
                    //taskSave.progressvalue = leftprogress;
                    CheckOnlineTimeTask(taskSave);
                }
                
                return taskSave;
            }
        }
         
        return null;
    }
    
    public void CheckOpenButterflyTask()
    {
        // if (!GameDataManager.instance.UserData.butterflyTaskIsOpen)
        // {
        //     GameDataManager.instance.UserData.butterflyTaskIsOpen = true;
        //     StartCoroutine(OnMaxButterlfyTask(AppGameSettings.TaskButterflyUseTime));
        // }
    }
    
    /// <summary>
    /// 每日任务是否开启
    /// </summary>
    /// <returns></returns>
    public bool IsOpen()
    {
        return GameDataManager.instance.UserData.CurrentStage>=AppGameSettings.UnlockRequirements.DailyMissions;
    }

    /// <summary>
    /// 所有每日任务奖励是否完成
    /// </summary>
    /// <returns></returns>
    public bool IsAllComplete()
    {
        bool isallover = false;
        int count = 0;
        foreach (TaskDataItem dataItem in taskItems)
        {
            if(GameDataManager.instance.UserData.CurrentStage >= dataItem.unlocklv)
                count+=dataItem.values.Count;
        }

        if (GameDataManager.instance.UserData.completeTaskList.Count >= count)
            isallover = true;

        if (!GameDataManager.instance.UserData.isAllCompleteTask&&isallover)
        {
            GameDataManager.instance.UserData.UpdateAllCompleteTask();           
            TimeSpan ts = DateTime.Now.Subtract(DateTime.Today);
            //ThinkManager.instance.Event_ActivityComplete("每日任务",  (int)ts.TotalSeconds);
            //FirebaseManager.Instance.ActivityComplete("每日任务", DateTime.Now.ToString(), (int)ts.TotalSeconds);
        }

        return isallover;
    }
    
    /// <summary>
    /// 每日任务奖励是否可以领取
    /// </summary>
    /// <returns></returns>
    public bool IsClaim()
    {
        foreach (var taskSave in GameDataManager.instance.UserData.taskSaveDatas)
        {
            if (taskSave.iscomplete&&!taskSave.iscliam)
                return true;
        }
        
        return false;
    }

    /// <summary>
    /// 更新任务进度(同一个类型在一个批次有且仅会出现一个次)
    /// </summary>
    /// <param name="taskType"></param>
    /// <param name="addvalue"></param>
    public void UpdateTaskProgress(TaskEvent taskevent,int addvalue)
    {
        if(!IsOpen()) return;
        
        List<TaskSaveData> taskSaveDatas = GameDataManager.instance.UserData.taskSaveDatas;
        // 查找匹配的 TaskSaveData
        TaskSaveData taskSave = taskSaveDatas.FirstOrDefault(t => t.taskid == (int)taskevent);
        if (taskSave != null)
        {
            // 更新进度值
            taskSave.progressvalue += addvalue;
            TaskDataItem taskDataItem = GetTaskItem(taskSave.taskid);
            int maxvalue = taskDataItem.values[taskSave.typeid];
            if (taskSave.progressvalue >=maxvalue)
            {
                taskSave.iscomplete = true;
                if (taskevent == TaskEvent.NeedOnlineTime && !AppGameSettings.SaveOnlineTimeProgress)
                {
                    taskSave.progressvalue = maxvalue;
                }
                
                if (taskevent != TaskEvent.NeedOnlineTime && !AppGameSettings.SaveMissionProgress)
                {
                    taskSave.progressvalue = maxvalue;
                }

                UpdateDailyTaskBtnUI();
            }

            if (!UpdatetaskItem.Contains(taskevent))
            {
                UpdatetaskItem.Add(taskevent);
            }
        }
        
        //GameDataManager.instance.UserData.SaveTaskData();
    }

    public TaskDataItem GetTaskItem(int limitItemID)
    {
        foreach (var limitItem in taskItems)
        {
            if (limitItem.id == limitItemID)
            {
                return limitItem;
            }
        }
        return null;
    }
}