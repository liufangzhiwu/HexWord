using UnityEngine;
using System.Collections.Generic;
using System;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();
    private static UnityMainThreadDispatcher _instance;

    private void Awake()
    {
        if(_instance == null)
            _instance = this;
    }

    public static UnityMainThreadDispatcher Instance()
    {
        if (!_instance)
        {
            // 自动在场景中创建一个承载脚本的物体
            GameObject obj = new GameObject("MainThreadDispatcher");
            _instance = obj.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(obj);
        }
        return _instance;
    }

    public void Enqueue(Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                // 取出任务
                Action action = _executionQueue.Dequeue();
                try
                {
                    // 尝试执行，如果这里报错，捕获它，不要影响后续任务
                    action.Invoke();
                }
                catch (Exception e)
                {
                    // 打印红色错误日志，方便在手机 Logcat 中看到
                    Debug.LogError($"[Dispatcher Error] 执行任务时发生异常: {e.Message}\n{e.StackTrace}");
                }
            }
        }
    }
}