using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TaskSaveData
{
    public int taskid;
    public int type;
    public int typeid;
    public int progressvalue;
    /// <summary>
    /// 是否已经领取奖励
    /// </summary>
    public bool iscliam;
    /// <summary>
    /// 是否已经完成
    /// </summary>
    public bool iscomplete;

    /// <summary>
    /// 当前任务类型更新到下一个
    /// </summary>
    public void AddTypeidTask()
    {
        typeid++;
        progressvalue = 0;
        iscomplete = false;
        iscliam = false;
    }
   
}

public class CompleteTaskData
{
    public int taskid;
    public int typeid;
}


