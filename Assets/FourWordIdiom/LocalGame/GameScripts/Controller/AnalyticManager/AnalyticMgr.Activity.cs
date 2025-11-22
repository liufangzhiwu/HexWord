using System.Collections.Generic;
using Middleware;


public partial class AnalyticMgr
{
    /// <summary>
    /// 弹窗显示
    /// </summary>
    public static void PopShow(string popName)
    {
        var properties = new Dictionary<string, object>()
        {
            {"pop_name",popName},
        }; 
        Game.Analytics.LogEvent("Pop_up", properties, Define.DataTarget.Think);

    }
    
    /// <summary>
    /// 弹窗同意（5星/购买/同意）
    /// </summary>
    public static void PopAccept(string popName)
    {

        var properties = new Dictionary<string, object>()
        {
            {"pop_name",popName},
        }; 
        Game.Analytics.LogEvent("Pop_accept", properties, Define.DataTarget.Think);

    }
    
    /// <summary>
    /// 弹窗拒绝（4星及一下/拒绝/关闭）
    /// </summary>
    public static void PopRefuse(string popName)
    {

        var properties = new Dictionary<string, object>()
        {
            {"pop_name",popName},
        }; 
        Game.Analytics.LogEvent("Pop_refuse", properties, Define.DataTarget.Think);

    }
    
   
    public static void BugRecord(string popName,string bugContent)
    {

        var properties = new Dictionary<string, object>()
        {
            {"bugType",popName},
            {"bugContent",bugContent},
        }; 
        Game.Analytics.LogEvent("Bug_record", properties, Define.DataTarget.Think);

    }
    
    /// <summary>
    /// 单个任务完成
    /// </summary>
    public static void TaskCompleted(string taskId,int value)
    {

        var properties = new Dictionary<string, object>()
        {
            {"task_id",taskId},
            {"task_reward",value},
        }; 
        Game.Analytics.LogEvent("task_completed", properties, Define.DataTarget.Think);

    }
    
    /// <summary>
    /// 活动开始
    /// </summary>
    public static void ActivityBegin(string activityId)
    {
        var properties = new Dictionary<string, object>()
        {
            {"activity_id",activityId}
        }; 
        Game.Analytics.LogEvent("activity_begin", properties, Define.DataTarget.Think);
    }
    
    /// <summary>
    /// 活动进度
    /// </summary>
    public static void ActivityProgress(string activityId,int progressId,int duration)
    {
        var properties = new Dictionary<string, object>()
        {
            {"activity_id",activityId},
            {"progress_id",progressId},
            {"activity_duration",duration},
        }; 
        Game.Analytics.LogEvent("activity_progress", properties, Define.DataTarget.Think);
    }
    
    /// <summary>
    /// 活动结束
    /// </summary>
    public static void ActivityComplete(string activityId,int duration)
    {
        var properties = new Dictionary<string, object>()
        {
            {"activity_id",activityId},
            {"activity_duration",duration},
        }; 
        Game.Analytics.LogEvent("activity_complete", properties, Define.DataTarget.Think);
    }
    
}