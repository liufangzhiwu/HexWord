using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Middleware
{
    public class UnityTimer : MonoBehaviour
    {
        /// <summary>
        /// 激活中的TimerTask对象
        /// </summary>
        private static readonly List<TimerTask> s_ActiveTasks = new List<TimerTask>();

        /// <summary>
        /// 定时器的ID计数
        /// </summary>
        private static long s_TimerID = 0x7f;

        /// <summary>
        /// 闲置TimerTask对象 : 线程安全
        /// </summary>
        private static readonly ConcurrentQueue<TimerTask> s_FreeTasks = new ConcurrentQueue<TimerTask>();

        /// <summary>
        /// 待添加的定时器任务队列 : 线程安全
        /// </summary>
        private static readonly ConcurrentQueue<TimerTask> s_PendingAddQueue = new ConcurrentQueue<TimerTask>();

        /// <summary>
        /// 待移除的定时器任务队列 : 线程安全
        /// </summary>
        private static readonly ConcurrentQueue<TimerTask> s_PendingRemoveQueue = new ConcurrentQueue<TimerTask>();

        /// <summary>
        /// 锁对象，用于确保在多线程环境下对定时器任务列表的安全访问和修改。
        /// </summary>
        private static readonly object s_Locker = new object();

        /// <summary>
        /// 双缓冲池
        /// </summary>
        private static List<TimerTask>[] s_Snapshots = { new List<TimerTask>(), new List<TimerTask>() };

        /// <summary>
        /// 双缓冲池当前下标
        /// </summary>
        private static int s_ActiveSnapshotIndex = 0;

        /// <summary>
        /// 清除所有定时器标志位
        /// </summary>
        private static bool s_ClearAll = false;

        /// <summary>
        /// 游戏时间:  缓存 Time.time当前帧 提供对外的业务访问
        /// </summary>
        public static float GameTime { private set; get; }

        /// <summary>
        /// 不受Time.timeScale限制的游戏时间: 缓存 Time.unscaledTime当前帧
        /// </summary>
        public static float UnscaledTime { private set; get; }

        /// <summary>
        /// 世界真实时间: 缓存 DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0当前帧
        /// </summary>
        public static double RealTime { private set; get; }


        #region Add timer task

        /// <summary>
        /// 延迟定时器 在指定时间后调用一次回调方法
        /// </summary>
        /// <param name="delay"> 延迟时长: 秒 </param>
        /// <param name="func"> 调用的方法回调 </param>
        /// <param name="timeSource"> 时间来源类型: 默认使用游戏时间 </param>
        /// <returns> 返回定时器的唯一标识符 用于后续操作 如取消定时器 </returns>
        public static long Delay(float delay, Action func, TimerTimeSource timeSource = TimerTimeSource.GameTime)
        {
            return Loop(delay, func, timeSource, 1);
        }

        /// <summary>
        /// 创建一个循环定时器，按照指定的时间间隔重复调用回调方法。
        /// </summary>
        /// <param name="interval">时间间隔: 秒</param>
        /// <param name="func">需要调用的回调方法</param>
        /// <param name="timeSource">时间来源类型: 默认使用游戏时间</param>
        /// <param name="times">循环次数: 0 表示无限循环，默认为 0</param>
        /// <returns>返回定时器的唯一标识符，用于后续操作，如取消定时器</returns>
        public static long Loop(float interval, Action func, TimerTimeSource timeSource = TimerTimeSource.GameTime,
            int times = 0)
        {
            //检查参数合法性
            if (func != null)
            {
                var target = func.Target;
                var method = func.Method;

                if (target == null || method.IsStatic)
                {
                    Debug.LogWarning(
                        "[Timer] Detected lambda or static method as func. " +
                        "Kill(object target) may not work for such tasks. " +
                        "Consider using class instance methods instead.");
                }
            }

            //从free池中 获取一个闲置的TimerTask对象
            var timer = GetFreeTimerTask();
            timer.LifeCycle = interval;
            timer.TimeSource = timeSource;
            timer.Func = func;
            timer.Times = times == 0 ? long.MaxValue : times;
            timer.ID = Interlocked.Increment(ref s_TimerID);
            timer.Refresh();

            //推入到 等待添加的线程安全队列中
            s_PendingAddQueue.Enqueue(timer);
            return timer.ID;
        }

        #endregion


        #region Find Timer

        private static List<TimerTask> FindTasks(Predicate<TimerTask> matchedCondition)
        {
            var snapshot = GetCurrentTaskSnapshot();
            List<TimerTask> result = snapshot.FindAll(matchedCondition);
            foreach (var t in s_PendingAddQueue)
            {
                if (matchedCondition(t))
                    result.Add(t);
            }

            return result;
        }

        /// <summary>
        /// 查找具有指定唯一标识符的定时任务。
        /// </summary>
        /// <param name="ID">要查找的定时任务的唯一标识符。</param>
        /// <returns>返回与指定ID匹配的定时任务的副本。如果未找到匹配的任务，则返回null。</returns>
        public static TimerTask Find(long ID)
        {
            List<TimerTask> freeTasks = null;
            lock (s_Locker)
            {
                freeTasks = FindTasks(t => t.ID == ID);
            }

            return freeTasks != null && freeTasks.Count > 0 ? freeTasks[0].Clone() : null;
        }

        /// <summary>
        /// 查找与指定回调方法关联的所有定时任务。
        /// 该方法会遍历当前活动的定时任务列表，筛选出与提供的回调方法匹配的任务，并返回这些任务的副本列表。
        /// </summary>
        /// <param name="func">要查找的回调方法。如果为 null，则不会返回任何任务。</param>
        /// <returns>包含与指定回调方法匹配的定时任务副本的列表。如果没有找到匹配的任务，则返回空列表。</returns>
        public static List<TimerTask> Find(Action func)
        {
            List<TimerTask> freeTasks = null;
            lock (s_Locker)
            {
                freeTasks = FindTasks(t => t.Func == func);
            }

            List<TimerTask> result = new List<TimerTask>();
            freeTasks?.ForEach(task => result.Add(task.Clone()));
            return result;
        }

        /// <summary>
        /// 查找与指定目标对象关联的所有定时任务。
        /// 该方法通过遍历活动任务列表，匹配任务回调函数的目标对象，返回所有符合条件的任务副本。
        /// </summary>
        /// <param name="target">要查找的目标对象，用于匹配定时任务回调函数的目标。</param>
        /// <returns>返回一个包含所有匹配定时任务副本的列表。如果没有找到匹配的任务，则返回空列表。</returns>
        public static List<TimerTask> Find(object target)
        {
            List<TimerTask> freeTasks = null;
            lock (s_Locker)
            {
                freeTasks = FindTasks(t => t.Func != null && t.Func.Target == target);
            }

            List<TimerTask> result = new List<TimerTask>();
            freeTasks?.ForEach(task => result.Add(task.Clone()));
            return result;
        }

        #endregion


        #region Clear timer

        /// <summary>
        /// 通过ID 清理定时器
        /// </summary>
        /// <param name="ID">定时器标签</param>
        /// <returns></returns>
        public static void Kill(long ID)
        {
            List<TimerTask> freeTasks = null;
            lock (s_Locker)
            {
                freeTasks = FindTasks(t => t.ID == ID);
            }

            if (freeTasks != null)
            {
                Kill(freeTasks);
            }
        }


        /// <summary>
        /// 通过类型来Kill
        /// @ps: 移除同类型的所有成员方法定时器  包含( lambda 和 其它类实例 )
        /// </summary>
        /// <param name="clsType"></param>
        public static void Kill<T>()
        {
            var clsName = typeof(T).FullName;
            List<TimerTask> freeTasks = null;

            lock (s_Locker)
            {
                freeTasks = FindTasks(t =>
                {
                    if (null != t.Func && null != t.Func.Target)
                    {
                        var fullname = t.Func.Target.GetType().FullName;
                        var currentClsNameClip = fullname.Split('+');
                        if (currentClsNameClip.Length > 0 && currentClsNameClip[0] == clsName)
                        {
                            return true;
                        }
                    }

                    return false;
                });
            }

            if (freeTasks != null)
            {
                Kill(freeTasks);
            }
        }

        /// <summary>
        /// 通过方法 清理定时器
        /// </summary>
        /// <param name="func">处理方法</param>
        /// <returns></returns>
        public static void Kill(Action func)
        {
            List<TimerTask> freeTasks = null;
            lock (s_Locker)
            {
                freeTasks = FindTasks(t => t.Func == func);
            }

            if (freeTasks != null)
            {
                Kill(freeTasks);
            }
        }


        /// <summary>
        /// 清理当前类的所有方法
        /// 避免Lambda 可能会存在问题，请尽量使用类成员方法注册定时器
        /// </summary>
        /// <param name="func">处理方法</param>
        /// <returns></returns>
        public static void Kill(object target)
        {
            List<TimerTask> freeTasks = null;
            lock (s_Locker)
            {
                freeTasks = FindTasks(t => t.Func != null && t.Func.Target == target);
            }

            if (freeTasks != null)
            {
                Kill(freeTasks);
            }
        }



        private static void Kill(List<TimerTask> tasks) => tasks.ForEach(t => t.Cancel());


        /// <summary>
        /// 清理所有定时器 一定要确保所有定时器能完全清理掉
        /// </summary>
        public static void KillAll() => s_ClearAll = true;

        #endregion


        #region Core

        /// <summary>
        /// 初始化定时器系统。此方法在场景加载之前自动调用，用于设置定时器的核心环境。
        /// 它会清理所有激活和闲置的定时任务列表，确保定时器系统在一个干净的状态下启动。
        /// 同时，创建一个名为 "Timer" 的全局游戏对象，并附加 Timer 组件，以保证定时器系统在整个应用程序生命周期内持续运行。
        /// 该方法通过 [RuntimeInitializeOnLoadMethod] 特性标记，确保在游戏启动时自动执行，无需手动调用。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            s_ActiveTasks.Clear();
            s_FreeTasks.Clear();
            DontDestroyOnLoad(new GameObject("Timer", typeof(UnityTimer)));
        }

        /// <summary>
        /// TimerTaskComparer 是一个用于比较两个定时任务的工具类，实现了 IComparer<TimerTask> 接口。
        /// 它的主要功能是根据定时任务的到期时间（ExpirationTime）对任务进行排序。
        /// 该类通过 Compare 方法定义了排序逻辑，确保定时任务按照其到期时间的先后顺序排列。
        /// 此比较器被内部用于维护定时任务的有序性，从而提高任务调度和管理的效率。
        /// </summary>
        private class TimerTaskComparer : IComparer<TimerTask>
        {
            public int Compare(TimerTask x, TimerTask y)
            {
                return x.GetTimeUntilNextExecution().CompareTo(y.GetTimeUntilNextExecution());
            }
        }

        /// <summary>
        /// TimerTaskComparer 是一个用于比较两个定时任务的工具类，实现了 IComparer<TimerTask> 接口。
        /// </summary>
        private static readonly TimerTaskComparer s_Comparer = new TimerTaskComparer();


        /// <summary>
        /// 将定时任务插入到活动任务列表中，并保持列表的有序性。
        /// </summary>
        /// <param name="task"></param>
        private static void InsertTaskSorted(TimerTask task)
        {
            int index = s_ActiveTasks.BinarySearch(task, s_Comparer);
            if (index < 0)
                index = ~index;
            s_ActiveTasks.Insert(index, task);
        }

        /// <summary>
        /// 定义定时器时间来源类型，用于指定定时器的时间基准。
        /// GameTime: 使用游戏时间，受时间缩放影响，适用于常规游戏逻辑。
        /// UnscaledTime: 使用未缩放的游戏时间，不受时间缩放影响，适用于暂停或慢动作等场景。
        /// RealTime: 使用系统实时时间，完全独立于游戏时间，适用于与游戏逻辑无关的精确计时。
        /// </summary>
        public enum TimerTimeSource
        {
            GameTime,
            UnscaledTime,
            RealTime
        }

        /// <summary>
        /// TimerTask 表示一个定时任务，用于在指定的时间或间隔内执行特定的操作。
        /// 该类封装了定时器任务的核心逻辑，包括生命周期、执行时间、执行次数以及回调函数。
        /// 定时任务支持不同的时间源类型，如游戏时间、非缩放时间和真实时间。
        /// 定时任务对象可以通过回收机制复用，以提高性能和减少内存分配。
        /// </summary>
        public class TimerTask
        {
            public long ID;
            public float LifeCycle;
            public double ExpirationTime;
            public long Times;
            public Action Func;
            public TimerTimeSource TimeSource;
            private volatile bool IsActived = true;


            /// <summary>
            /// 取消当前定时器任务的执行。
            /// </summary>
            public void Cancel()
            {
                if (!IsActived)
                    return;
                IsActived = false;
                s_PendingRemoveQueue.Enqueue(this);
            }


            /// <summary>
            /// 判断当前定时器任务是否处于活动状态。
            /// </summary>
            /// <returns></returns>
            public bool IsActive() => IsActived;


            /// <summary>
            /// 返回一个副本，避免一些获取的操作 对定时器直接操作，避免可能的线程安全问题
            /// </summary>
            /// <returns></returns>
            public TimerTask Clone()
            {
                var task = new TimerTask()
                {
                    ID = ID,
                    LifeCycle = LifeCycle,
                    ExpirationTime = ExpirationTime,
                    Times = Times,
                    Func = Func,
                    TimeSource = TimeSource
                };
                return task;
            }


            /// <summary>
            /// 获取当前时间 根据定时器的类型来获取 世界真实时间，游戏内时间，游戏内非缩放时间
            /// </summary>
            /// <param name="timeSource">时间来源类型: 游戏时间、不受缩放影响的游戏时间或世界真实时间</param>
            /// <returns>返回对应时间来源类型的当前时间值</returns>
            private double GetCurrentTime(TimerTimeSource timeSource)
            {
                return timeSource switch
                {
                    TimerTimeSource.GameTime => UnityTimer.GameTime,
                    TimerTimeSource.UnscaledTime => UnityTimer.UnscaledTime,
                    TimerTimeSource.RealTime => UnityTimer.RealTime,
                    _ => UnityTimer.GameTime,
                };
            }


            /// <summary>
            /// 获取当前时间 根据定时器的时间来源类型返回对应的时间值
            /// </summary>
            /// <returns>返回与定时器时间来源类型相对应的当前时间值</returns>
            public double GetCurrentTime() => GetCurrentTime(TimeSource);

            /// <summary>
            /// 释放回收当前定时器
            /// </summary>
            public void Recycle()
            {
                ID = 0;
                LifeCycle = 0;
                ExpirationTime = 0;
                Times = 0;
                Func = null;
                TimeSource = TimerTimeSource.GameTime;
                IsActived = true;
                s_FreeTasks.Enqueue(this);
            }

            /// <summary>
            /// 刷新下一次更新的时间
            /// </summary>
            public void Refresh()
            {
                ExpirationTime = GetCurrentTime() + LifeCycle;
            }


            /// <summary>
            /// 判断当前定时器任务是否已达到下一次执行的时间点。
            /// 该方法通过比较当前时间与任务的过期时间来确定是否需要执行下一步操作。
            /// </summary>
            /// <returns>返回布尔值，如果当前时间大于或等于任务的过期时间，则返回 true，否则返回 false。</returns>
            public bool Next() => GetCurrentTime() >= ExpirationTime;


            /// <summary>
            /// 计算当前定时器任务距离下次执行的时间间隔。
            /// </summary>
            /// <returns></returns>
            public double GetTimeUntilNextExecution() => ExpirationTime - GetCurrentTime();
        }

        /// <summary>
        /// 刷新当前活动任务的快照。该方法用于在多线程环境下安全地更新任务快照，
        /// 确保在遍历任务列表时不会因任务的动态添加或移除而导致数据不一致。
        /// 快照通过索引切换的方式进行更新，避免直接修改当前正在使用的任务列表。
        /// 该方法在类的内部被调用，通常与定时任务的管理和执行逻辑配合使用。
        /// </summary>
        private static void RefreshSnapshot()
        {
            lock (s_Locker)
            {
                s_ActiveSnapshotIndex = 1 - s_ActiveSnapshotIndex;
                var snapshot = s_Snapshots[s_ActiveSnapshotIndex];
                snapshot.Clear();
                snapshot.AddRange(s_ActiveTasks);
            }
        }

        /// <summary>
        /// 获取当前活动的任务快照列表，包含所有正在运行的定时任务。
        /// 该方法返回一个只读的定时任务列表，用于查询或遍历当前所有活动的任务。
        /// 快照是双缓冲池的一部分，确保在多线程环境下任务列表的一致性和安全性。
        /// </summary>
        /// <returns>返回当前活动的任务快照列表，其中包含所有正在执行的定时任务。</returns>
        private static List<TimerTask> GetCurrentTaskSnapshot() => s_Snapshots[s_ActiveSnapshotIndex];

        /// <summary>
        /// 在每一帧更新定时器系统的内部状态。
        /// 该方法负责刷新时间快照、处理待执行的任务、管理活跃任务，并在满足执行条件时调用任务回调函数。
        /// 它确保任务根据其设定的时间间隔或延迟被正确执行。
        /// 此外，如果某个任务在执行过程中发生异常，它也会通过日志记录错误，以实现优雅的异常处理。
        /// </summary>
        private void Update()
        {
            GameTime = Time.time;
            UnscaledTime = Time.unscaledTime;
            RealTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

            //检查清除所有定时器的标志位
            if (s_ClearAll)
            {
                s_ClearAll = false;
                s_ActiveTasks.ForEach(task => task.Recycle());
                s_ActiveTasks.Clear();
                s_PendingAddQueue.Clear();
                s_PendingRemoveQueue.Clear();
            }
            else
            {
                //新增的任务
                while (s_PendingAddQueue.TryDequeue(out var task))
                {
                    InsertTaskSorted(task);
                }

                //移除的任务
                while (s_PendingRemoveQueue.TryDequeue(out var task))
                {
                    s_ActiveTasks.Remove(task);
                    task.Recycle();
                }

                //刷新快照
                RefreshSnapshot();
            }


            TimerTask t = null;
            for (int i = 0; i < s_ActiveTasks.Count; ++i)
            {
                t = s_ActiveTasks[i];
                if (!t.IsActive()) continue;
                if (t.Next())
                {

                    try
                    {
                        t.Func?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"TimerTask Exception: {e}");
                    }

                    --t.Times;
                    if (t.Times == 0)
                    {
                        t.Cancel();
                    }
                    else
                    {
                        t.Refresh();
                        s_ActiveTasks.Remove(t);
                        InsertTaskSorted(t);
                        break;
                    }
                }
                else break;
            }
        }


        /// <summary>
        /// 从定时任务的空闲队列中获取一个可用的 TimerTask 对象。
        /// 如果空闲队列中没有可用对象，则创建一个新的 TimerTask 实例。
        /// 该方法用于优化定时任务的内存使用，通过复用已回收的 TimerTask 对象减少频繁的内存分配。
        /// </summary>
        /// <returns>返回一个可用的 TimerTask 对象，该对象可能来自空闲队列或新创建的实例。</returns>
        private static TimerTask GetFreeTimerTask()
        {
            if (!s_FreeTasks.TryDequeue(out var task))
            {
                task = new TimerTask();
            }

            return task;
        }

        #endregion

    }
}

