#if UNITY_OPENHARMONY
using System;
using System.Collections.Generic;
using System.Globalization;
using Middleware;
using OpenHarmonyKits.Signal;
using UnityEngine;

namespace Middleware
{
    /// <summary> 文案池类型 </summary>
    public enum PushTextPool
    {
        Morning,   // 早晨：M*
        Noon,      // 中午：A*
        Night,     // 夜晚：E*
        Recall     // 召回：R*
    }

    /// <summary> 用户分层 </summary>
    public enum UserSegment
    {
        Active,    // 活跃：最近 5 天内（含当天）有登录
        Churn      // 衰退/预流失：最近 5 天及以上未登录
    }

    /// <summary> 单个推送时间槽 </summary>
    internal class PushSlot
    {
        public DayOfWeek[] Days;    // 命中星期
        public TimeSpan Time;       // 命中时间点
        public PushTextPool Pool;   // 使用哪个文案池
    }

    public class Push_harmony : IPushs
    {
        // ============================================================
        // 常量
        // ============================================================
        private const string KEY_LAST_PUSH_STAMP = "push_last_push_stamp"; // yyyyMMdd_HHmm 防重复

        private const string KEY_IDX_MORNING = "push_idx_morning";
        private const string KEY_IDX_NOON    = "push_idx_noon";
        private const string KEY_IDX_NIGHT   = "push_idx_night";
        private const string KEY_IDX_RECALL  = "push_idx_recall";

        /// <summary> 活跃用户阈值：最近 N 天内（含当天）有登录 → 活跃 </summary>
        private const int ACTIVE_DAYS = 5;

        /// <summary> Unix 纪元（用于时间戳换算） </summary>
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary> 毫秒 / 秒时间戳分界阈值 </summary>
        private const long TIMESTAMP_MS_THRESHOLD = 99999999999L;

        /// <summary> 推送时间点 </summary>
        private static readonly TimeSpan MORNING_TIME = new TimeSpan(6, 58, 0);
        private static readonly TimeSpan NOON_TIME    = new TimeSpan(14, 0, 0);
        private static readonly TimeSpan NIGHT_TIME   = new TimeSpan(21, 0, 0);

        /// <summary> 时间点命中容差（秒），落在 [T, T+容差] 内视为命中 </summary>
        private const int SLOT_TOLERANCE_SECONDS = 60;

        /// <summary> 调度 tick 间隔（秒） </summary>
        private const float TICK_INTERVAL = 30f;

        /// <summary> 通知标题兜底（配置表缺标题时使用） </summary>
        private const string DEFAULT_TITLE = "游戏提醒";

        // ---- 活跃用户排期 ----
        private static readonly PushSlot[] ActiveSchedule = new PushSlot[]
        {
            new PushSlot {
                Days = new[]{ DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                Time = MORNING_TIME, Pool = PushTextPool.Morning
            },
            new PushSlot {
                Days = new[]{ DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                              DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday },
                Time = NOON_TIME, Pool = PushTextPool.Noon
            },
            new PushSlot {
                Days = new[]{ DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                              DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday },
                Time = NIGHT_TIME, Pool = PushTextPool.Night
            },
        };

        // ---- 衰退/预流失用户排期 ----
        private static readonly PushSlot[] ChurnSchedule = new PushSlot[]
        {
            new PushSlot {
                Days = new[]{ DayOfWeek.Monday, DayOfWeek.Wednesday },
                Time = MORNING_TIME, Pool = PushTextPool.Morning
            },
            new PushSlot {
                Days = new[]{ DayOfWeek.Tuesday, DayOfWeek.Thursday },
                Time = NOON_TIME, Pool = PushTextPool.Noon
            },
            new PushSlot {
                Days = new[]{ DayOfWeek.Saturday },
                Time = NIGHT_TIME, Pool = PushTextPool.Recall
            },
            new PushSlot {
                Days = new[]{ DayOfWeek.Friday, DayOfWeek.Sunday },
                Time = NIGHT_TIME, Pool = PushTextPool.Night
            },
        };

        // ============================================================
        // 运行时状态
        // ============================================================
        public string pushToken { get; set; }
        
        private bool _initialized = false;
        private bool _destroyed = false;

        private readonly HashSet<int> _activeNotificationIds = new HashSet<int>();
        private int _notificationIdCounter = 0;

        // ============================================================
        // 生命周期
        // ============================================================
        public void Init(float delay)
        {
            _destroyed = false;

            UnityTimer.Delay(delay, () =>
            {
                if (_destroyed) return;

                // 1. 注册 Token 回调
                SignalHandler.Instance.RegisterSignalDelegate<Push_GetTokenSignal>(OnGetTokenTrigger);
                GetToken();

                // 2. 启动推送调度
                //    权限申请由外部满足条件后调用 RequestEnableNotification
                _initialized = true;
                StartScheduler();
            });
        }

        /// <summary> 外部主动销毁时调用（如框架不支持 OnDestroy 时手动调用） </summary>
        public void OnDestroy()
        {
            _destroyed = true;
            _initialized = false;

            if (SignalHandler.Instance != null)
            {
                SignalHandler.Instance.UnRegisterSignalDelegate<Push_GetTokenSignal>(OnGetTokenTrigger);
            }
        }

        // ============================================================
        // 权限请求（外部条件触发）
        // ============================================================
        /// <summary>
        /// 请求通知权限。触发条件（由游戏业务侧保证）：
        ///   1) 填字玩法关卡进度 ≥ 1；
        ///   2) 已经过游戏加载页（Loading 界面）。
        /// 内部做幂等保护，避免重复弹窗。
        /// </summary>
        public void RequestEnableNotification()
        {
            if (GameDataManager.Instance.UserData.IsPushRequested) return;
            GameDataManager.Instance.UserData.IsPushRequested = true;
            OHSDKKitManager.Instance.RequestEnableNotification();
            Debug.Log("[Push_harmony] RequestEnableNotification issued.");
        }

        /// <summary> 获取 Push Token（本地通知不依赖，保留用于服务端上报） </summary>
        public void GetToken()
        {
            OHSDKKitManager.Instance.GetPushToken();
        }

        
        /// <summary>
        /// 分层判断：
        ///   活跃：离线时间 ≤ 4 天（含当天）
        ///   衰退：离线时间 ≥ 5 天
        /// 无有效 logoutTime（空串、"0"、格式异常）→ 视为活跃
        /// </summary>
        private UserSegment GetUserSegment()
        {
            string raw = GameDataManager.Instance?.UserData?.logoutTime;

            // 空串 / "0" / null → 视为活跃
            if (string.IsNullOrWhiteSpace(raw) || raw == "0")
                return UserSegment.Active;

            if (!DateTime.TryParse(raw, out DateTime lastLogout))
                return UserSegment.Active;   // 解析失败也视为活跃，不抛异常

            int days = (DateTime.Now.Date - lastLogout.Date).Days;
            return days >= ACTIVE_DAYS ? UserSegment.Churn : UserSegment.Active;
        }

        // ============================================================
        // 调度器（基于 UnityTimer 递归延时，不依赖 MonoBehaviour）
        // ============================================================
        private void StartScheduler()
        {
            // 启动时先补检一次
            TickScheduler();
            // 然后循环调度
            ScheduleNextTick();
        }

        private void ScheduleNextTick()
        {
            if (_destroyed || !_initialized) return;

            UnityTimer.Delay(TICK_INTERVAL, () =>
            {
                if (_destroyed || !_initialized) return;
                TickScheduler();
                ScheduleNextTick();
            });
        }

        /// <summary>
        /// 每次 tick 检查：当前时间是否命中某个推送时间槽；
        /// 若命中且当天该槽尚未推送过，则执行推送。
        /// </summary>
        private void TickScheduler()
        {
            if (!_initialized) return;

            DateTime now = DateTime.Now;
            UserSegment segment = GetUserSegment();
            PushSlot[] schedule = segment == UserSegment.Active ? ActiveSchedule : ChurnSchedule;

            foreach (var slot in schedule)
            {
                if (Array.IndexOf(slot.Days, now.DayOfWeek) < 0) continue;

                // 命中判断：now.TimeOfDay ∈ [slot.Time, slot.Time + 容差]
                var diff = now.TimeOfDay - slot.Time;
                if (diff.TotalSeconds < 0 || diff.TotalSeconds > SLOT_TOLERANCE_SECONDS) continue;

                // 防重复
                string stamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
                             + "_" + slot.Time.ToString(@"hhmm");
                if (PlayerPrefs.GetString(KEY_LAST_PUSH_STAMP, "") == stamp) continue;

                // 取出文案并推送
                var data = PeekAndAdvanceText(slot.Pool);
                if (data == null)
                {
                    Debug.LogWarning($"[Push_harmony] 文案池 {slot.Pool} 为空，跳过本次推送");
                    continue;
                }

                DoPush(slot.Pool, data);

                PlayerPrefs.SetString(KEY_LAST_PUSH_STAMP, stamp);
                PlayerPrefs.Save();

                Debug.Log($"[Push_harmony] Pushed. Segment={segment}, Slot={slot.Pool}, Stamp={stamp}, Key={data.Key}");
                return; // 一次 tick 只处理一个槽
            }
        }

        // ============================================================
        // 文案池循环
        // ============================================================
        /// <summary>
        /// 从配置表取当前文案并推进索引（持久化，跨分层不重置）。
        /// </summary>
        private PushData PeekAndAdvanceText(PushTextPool pool)
        {
            string prefix = GetPoolPrefix(pool);
            string key    = GetIndexKey(pool);

            int idx = PlayerPrefs.GetInt(key, 0);
            var data = PushManaer.Instance.GetPoolItem(prefix, idx);
            if (data == null) return null;

            // 索引 +1（跨分层沿用，不回退）
            PlayerPrefs.SetInt(key, idx + 1);
            PlayerPrefs.Save();

            return data;
        }

        private string GetPoolPrefix(PushTextPool pool)
        {
            switch (pool)
            {
                case PushTextPool.Morning: return PushManaer.POOL_MORNING; // "M"
                case PushTextPool.Noon:    return PushManaer.POOL_NOON;    // "A"
                case PushTextPool.Night:   return PushManaer.POOL_NIGHT;   // "E"
                case PushTextPool.Recall:  return PushManaer.POOL_RECALL;  // "R"
            }
            return PushManaer.POOL_MORNING;
        }

        private string GetIndexKey(PushTextPool pool)
        {
            switch (pool)
            {
                case PushTextPool.Morning: return KEY_IDX_MORNING;
                case PushTextPool.Noon:    return KEY_IDX_NOON;
                case PushTextPool.Night:   return KEY_IDX_NIGHT;
                case PushTextPool.Recall:  return KEY_IDX_RECALL;
            }
            return KEY_IDX_MORNING;
        }

        // ============================================================
        // 实际推送
        // ============================================================
        /// <summary> 通过 Notification Kit 发布本地通知 </summary>
        private void DoPush(PushTextPool pool, PushData data)
        {
            int notificationId = _notificationIdCounter++;
            _activeNotificationIds.Add(notificationId);

            string title = string.IsNullOrEmpty(data.Title) ? DEFAULT_TITLE : data.Title;
            string body  = string.IsNullOrEmpty(data.Text)  ? data.Key : data.Text;

            OHSDKKitManager.Instance.PublishNotification(
                notificationId,
                title,
                body,
                string.Empty   // additionalText
            );

            Debug.Log($"[Push_harmony] Local notification published. Id={notificationId}, Pool={pool}, Key={data.Key}, Title={title}");
        }

        // ============================================================
        // 对外推送 / 取消（供业务调用）
        // ============================================================
        public void Push(string title, string body)
        {
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body))
            {
                Debug.LogError("[Push_harmony] Title and body are both empty.");
                //return -1;
            }

            int id = _notificationIdCounter++;
            _activeNotificationIds.Add(id);
            OHSDKKitManager.Instance.PublishNotification(id, title, body, string.Empty);
            Debug.Log($"[Push_harmony] Manual push. Id={id}, Title={title}");
            //return id;
        }

        public void Cancel(int notificationId)
        {
            if (!_activeNotificationIds.Contains(notificationId))
            {
                Debug.LogWarning($"[Push_harmony] Notification Id {notificationId} not found or already cancelled.");
                return;
            }
            OHSDKKitManager.Instance.CancelNotification(notificationId, _activeNotificationIds.Count);
            _activeNotificationIds.Remove(notificationId);
            Debug.Log($"[Push_harmony] Notification Id {notificationId} cancelled.");
        }

        public void CancelAll()
        {
            int total = _activeNotificationIds.Count;
            foreach (int id in _activeNotificationIds)
                OHSDKKitManager.Instance.CancelNotification(id, total);
            _activeNotificationIds.Clear();
            Debug.Log("[Push_harmony] All local notifications cancelled.");
        }

        public void Update(int notificationId, string title, string body)
        {
            if (!_activeNotificationIds.Contains(notificationId))
            {
                Debug.LogWarning($"[Push_harmony] Cannot update. Notification Id {notificationId} is not active.");
                return;
            }
            OHSDKKitManager.Instance.PublishNotification(notificationId, title, body, string.Empty);
            Debug.Log($"[Push_harmony] Notification Id {notificationId} updated.");
        }

        // ============================================================
        // Token 回调
        // ============================================================
        private void OnGetTokenTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                var targetSignal = (Push_GetTokenSignal)signal;
                pushToken = targetSignal.pushToken;
                Debug.Log($"[Push_harmony] GetToken Success. Token: {pushToken}");
                GameDataManager.Instance.UserData.PushToken = pushToken;

                // 如需在服务端使用 Push Token 进行远程推送或订阅，请在此处上报
                // UploadTokenToServer(pushToken);
            }
            else
            {
                Debug.LogError($"[Push_harmony] GetToken Error. Code: {signal.code}, Message: {signal.message}");
            }
        }
    }
}
#endif