#if UNITY_OPENHARMONY
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Middleware;
using OpenHarmonyKits.Signal;
using UnityEngine;
using System.IO;
using UnityEngine.OpenHarmony;

namespace Middleware
{
    /// <summary> 文案池类型 </summary>
    public enum PushTextPool
    {
        Morning,
        Noon,
        Night,
        Recall
    }

    /// <summary> 用户分层 </summary>
    public enum UserSegment
    {
        Active,
        Churn
    }

    /// <summary> 单个推送时间槽 </summary>
    internal class PushSlot
    {
        public DayOfWeek[] Days;
        public TimeSpan Time;
        public PushTextPool Pool;
    }

    public class Push_harmony : IPushs
    {
        // ============================================================
        // 常量
        // ============================================================
        private const string KEY_AGENT_REGISTERED_MAP = "push_agent_registered_map";

        private const string KEY_IS_PUSH_REQUESTED = "is_push_requested";

        /// <summary> ★ 新增：首次注册日期（yyyy-MM-dd） </summary>
        private const string KEY_BASE_DATE = "push_base_date";

        private const int ACTIVE_DAYS = 5;

        private const int AGENT_RING_DURATION = 0;

        // ============================================================
        // 各文案池长度（用于循环取模）
        //   M1-M20：20 条
        //   A1-A28：28 条
        //   E1-E28：28 条
        //   R1-R5 ：5 条
        // ============================================================
        private const int POOL_LEN_MORNING = 20;
        private const int POOL_LEN_NOON    = 28;
        private const int POOL_LEN_NIGHT   = 28;
        private const int POOL_LEN_RECALL  = 5;

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private const long TIMESTAMP_MS_THRESHOLD = 99999999999L;

        private static readonly TimeSpan MORNING_TIME = new TimeSpan(6, 58, 0);
        private static readonly TimeSpan NOON_TIME    = new TimeSpan(14, 0, 0);
        private static readonly TimeSpan NIGHT_TIME   = new TimeSpan(21, 0, 0);

        private const int SLOT_TOLERANCE_SECONDS = 60;
        private const float TICK_INTERVAL = 30f;
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
        // 周一、周三：早 6:58
        // 周二、周四：午 14:00
        // 周六：晚 21:00（召回文案 R1-R5 循环）
        // 周五、周日：晚 21:00（夜晚文案 E1-E28 循环）
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
            // ★ 周六 晚 21:00 → 召回池 R1-R5（循环）
            new PushSlot {
                Days = new[]{ DayOfWeek.Saturday },
                Time = NIGHT_TIME, Pool = PushTextPool.Recall
            },
            // ★ 周五、周日 晚 21:00 → 夜晚池 E1-E28（循环）
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
        private static OpenHarmonyJSObject _harmonyProxy;
        private readonly Dictionary<string, string> _agentRegisteredMap = new Dictionary<string, string>();

        private const string BUNDLE_NAME = "chengyu.idiom.hexa.zen.huawei";
        private const string ABILITY_NAME = "TuanjiePlayerAbility";

        /// <summary> 检查推送状态次数 </summary>
        private int CheckPushStateTimes = 0;
        private int CheckPushStateTimesMax = 10;

        /// <summary> 前台状态日志去重标记 </summary>
        private bool _skipForegroundLogged = false;

        // ============================================================
        // 生命周期
        // ============================================================
        public void Init(float delay)
        {
            _destroyed = false;
            UnityTimer.Delay(delay, () =>
            {
                if (_destroyed) return;
                SignalHandler.Instance.RegisterSignalDelegate<Push_GetTokenSignal>(OnGetTokenTrigger);
              
#if UNITY_OPENHARMONY&&!UNITY_EDITOR
                GetToken();

                InitAgentReminder();

                _initialized = true;
                StartScheduler();
#endif
            });
        }

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
        // 权限请求
        // ============================================================
        public void RequestEnableNotification()
        {
            int isPushRequested = PlayerPrefs.GetInt(KEY_IS_PUSH_REQUESTED, 0);
            CheckPushStateTimes = 0;

            if (isPushRequested == 0)
            {
                AnalyticMgr.PopShow(popName: "消息推送");
                OHSDKKitManager.Instance.RequestEnableNotification();

                Debug.Log("[Push_harmony] RequestEnableNotification issued.");
            }
            else
            {
                if (GameDataManager.Instance.UserData.IsAutoPush)
                {
                    RestoreAgentRegisteredMap();
                    RegisterAgentReminders();

                    EventDispatcher.instance.TriggerChangeGoldUI(0, false);

                    Debug.Log("[Push_harmony] Push Open, Enter RestoreAgentRegisteredMap.");
                }
            }
        }

        public void ReSetRequestEnableNotification()
        {
            //AnalyticMgr.PopShow(popName: "消息推送");
            CheckPushStateTimes = 0;
            OHSDKKitManager.Instance.CancelNotification(0, 0);
            Debug.Log("[Push_harmony] OpenNotificationSettingsPanel");
        }

        // ============================================================
        // ★ 供 PushMessageReceiver 调用（由 ArkTS 侧 TuanjieSendMessage 触发）
        // ============================================================
        public void HandleSettingsClosedFromJS(bool enabled)
        {
            Debug.Log($"[Push_harmony] HandleSettingsClosedFromJS, enabled={enabled}");

            if (GameDataManager.Instance != null && GameDataManager.Instance.UserData != null)
            {
                GameDataManager.Instance.UserData.IsAutoPush = enabled;
            }
            int isPushRequested = PlayerPrefs.GetInt(KEY_IS_PUSH_REQUESTED, 0);

            if (enabled)
            {
               if(isPushRequested == 0) 
                   AnalyticMgr.PopAccept(popName: "消息推送");

                //后台时才进行注册
                //if (!Application.isFocused)
                //{
                    RestoreAgentRegisteredMap();
                    RegisterAgentReminders();
                //}
             
                EventDispatcher.instance.TriggerChangeGoldUI(0, false);
            }
            else
            {
                if(isPushRequested == 0) 
                    AnalyticMgr.PopRefuse(popName: "消息推送");
            }

            EventDispatcher.instance.TriggerChangeGoldUI(0, false);
            
            PlayerPrefs.SetInt(KEY_IS_PUSH_REQUESTED, 1);
            PlayerPrefs.Save();
          
        }

        public void GetToken()
        {
            OHSDKKitManager.Instance.GetPushToken();
        }

        // ============================================================
        // 用户分层判断
        // ============================================================
        private UserSegment GetUserSegment()
        {
            string raw = GameDataManager.Instance?.UserData?.logoutTime;
            if (string.IsNullOrWhiteSpace(raw) || raw == "0")
                return UserSegment.Active;
            if (!DateTime.TryParse(raw, out DateTime lastLogout))
                return UserSegment.Active;
            int days = (DateTime.Now.Date - lastLogout.Date).Days;
            return days >= ACTIVE_DAYS ? UserSegment.Churn : UserSegment.Active;
        }

        // ============================================================
        // 本地即时通知调度器
        // ============================================================
        private void StartScheduler()
        {
            TickScheduler();
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

        private void TickScheduler()
        {
            if (!_initialized) return;

            // App 在前台运行时，跳过当前时间点的本地推送
            // if (Application.isFocused)
            // {
            //     if (!_skipForegroundLogged)
            //     {
            //         Debug.Log("[Push_harmony] App 在前台，TickScheduler 跳过本地推送");
            //         _skipForegroundLogged = true;
            //     }
            //     return;
            // }
            _skipForegroundLogged = false;

            DateTime now = DateTime.Now;
            UserSegment segment = GetUserSegment();
            PushSlot[] schedule = segment == UserSegment.Active ? ActiveSchedule : ChurnSchedule;

            foreach (var slot in schedule)
            {
                if (Array.IndexOf(slot.Days, now.DayOfWeek) < 0) continue;
                var diff = now.TimeOfDay - slot.Time;
                if (diff.TotalSeconds < 0 || diff.TotalSeconds > SLOT_TOLERANCE_SECONDS) continue;

                // ★ 按日期取文案
                var data = GetTextForDate(slot.Pool, now);
                if (data == null) continue;
                //DoLocalPush(slot.Pool, data);
                Debug.Log($"[Push_harmony] Local push. Segment={segment}, Slot={slot.Pool}, Key={data.Key}");
                return;
            }
        }

        // ============================================================
        // ★ 按日期分配文案
        // ============================================================
        /// <summary>
        /// 获取基准日期（首次注册日）
        /// 首次调用时写入 PlayerPrefs，后续沿用
        /// </summary>
        private DateTime GetBaseDate()
        {
            string baseStr = PlayerPrefs.GetString(KEY_BASE_DATE, "");
            DateTime baseDate;
            if (string.IsNullOrEmpty(baseStr) || !DateTime.TryParse(baseStr, out baseDate))
            {
                baseDate = DateTime.Now.Date;
                PlayerPrefs.SetString(KEY_BASE_DATE, baseDate.ToString("yyyy-MM-dd"));
                PlayerPrefs.Save();
                Debug.Log($"[Push_harmony] ★ 首次注册，记录 baseDate = {baseDate:yyyy-MM-dd}");
            }
            return baseDate.Date;
        }

        /// <summary>
        /// 计算指定日期距基准日期的天数差
        /// </summary>
        private int GetDayIndexForDate(DateTime date)
        {
            DateTime baseDate = GetBaseDate();
            int dayOffset = (date.Date - baseDate).Days;
            if (dayOffset < 0) dayOffset = 0;
            return dayOffset;
        }

        /// <summary>
        /// ★ 按日期取文案：
        ///   dayIdx = (date - baseDate).Days
        ///   realIdx = dayIdx % poolLen
        ///   例：24 号安装，则 24 号 dayIdx=0 → M1/A1/N1
        ///       25 号 dayIdx=1 → M2/A2/N2
        ///       26 号 dayIdx=2 → M3/A3/N3
        /// </summary>
        private PushData GetTextForDate(PushTextPool pool, DateTime date)
        {
            string prefix = GetPoolPrefix(pool);
            int poolLen = GetPoolLength(pool);
            if (poolLen <= 0)
            {
                Debug.LogWarning($"[Push_harmony] 文案池 {pool} 长度为 0");
                return null;
            }

            int dayIdx = GetDayIndexForDate(date);
            int realIdx = dayIdx % poolLen;

            var data = PushManager.Instance.GetPoolItem(prefix, realIdx);
            if (data == null)
            {
                Debug.LogWarning($"[Push_harmony] 文案池 {pool} 取不到数据, dayIdx={dayIdx}, realIdx={realIdx}");
                return null;
            }

            Debug.Log($"[Push_harmony] 按日期取文案: pool={pool}, date={date:yyyy-MM-dd}, dayIdx={dayIdx}, realIdx={realIdx}, key={data.Key}");
            return data;
        }

        /// <summary> 各文案池长度 </summary>
        private int GetPoolLength(PushTextPool pool)
        {
            switch (pool)
            {
                case PushTextPool.Morning: return POOL_LEN_MORNING;
                case PushTextPool.Noon:    return POOL_LEN_NOON;
                case PushTextPool.Night:   return POOL_LEN_NIGHT;
                case PushTextPool.Recall:  return POOL_LEN_RECALL;
            }
            return 0;
        }

        private string GetPoolPrefix(PushTextPool pool)
        {
            switch (pool)
            {
                case PushTextPool.Morning: return PushManager.POOL_MORNING;
                case PushTextPool.Noon:    return PushManager.POOL_NOON;
                case PushTextPool.Night:   return PushManager.POOL_NIGHT;
                case PushTextPool.Recall:  return PushManager.POOL_RECALL;
            }
            return PushManager.POOL_MORNING;
        }

        // // ============================================================
        // // 本地即时推送
        // // ============================================================
        // private void DoLocalPush(PushTextPool pool, PushData data)
        // {
        //     int notificationId = _notificationIdCounter++;
        //     _activeNotificationIds.Add(notificationId);
        //     string title = string.IsNullOrEmpty(data.Title) ? DEFAULT_TITLE : data.Title;
        //     string body  = string.IsNullOrEmpty(data.Text)  ? data.Key : data.Text;
        //     //OHSDKKitManager.Instance.PublishNotification(notificationId, title, body, string.Empty);
        //     Debug.Log($"[Push_harmony] Local notification published. Id={notificationId}, Pool={pool}, Key={data.Key}, Title={title}");
        // }

        // ============================================================
        // 代理提醒：初始化
        // ============================================================
        private void InitAgentReminder()
        {
            Debug.Log("[Push_harmony] InitAgentReminder start.");

            try
            {
                _harmonyProxy = new OpenHarmonyJSObject("PushHarmonyProxy");

                string filesDir = Application.persistentDataPath;
                if (string.IsNullOrEmpty(filesDir))
                {
                    filesDir = "/data/storage/el2/base/haps/entry/files";
                    Debug.LogWarning($"[Push_harmony] persistentDataPath empty, using fallback: {filesDir}");
                }

                Debug.Log($"[Push_harmony] filesDir = '{filesDir}'");
                _harmonyProxy.Call("SetConfig", BUNDLE_NAME, ABILITY_NAME, filesDir);

                // 恢复 map，清理已过期
                RestoreAgentRegisteredMap();
                CleanupExpiredAgentMap(DateTime.Now);

                // 确保 baseDate 存在（首次安装时写入）
                GetBaseDate();

                Debug.Log($"[Push_harmony] InitAgentReminder done. Restored map={_agentRegisteredMap.Count}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Push_harmony] InitAgentReminder failed: {e.Message}");
            }
        }

        // ============================================================
        // 代理提醒：注册队列（两段式）
        // ============================================================
        private void RegisterAgentReminders()
        {
            if (_harmonyProxy == null)
            {
                Debug.LogWarning("[Push_harmony] RegisterAgentReminders skipped: proxy not ready.");
                return;
            }

            DateTime now = DateTime.Now;
            DateTime today = now.Date;
            CleanupExpiredAgentMap(now);
            int registeredCount = 0;

            registeredCount += RegisterScheduleRange(
                ActiveSchedule, UserSegment.Active, today, now, 0, ACTIVE_DAYS);

            registeredCount += RegisterScheduleRange(
                ChurnSchedule, UserSegment.Churn, today, now, ACTIVE_DAYS, ACTIVE_DAYS * 6);

            SaveAgentRegisteredMap();
            Debug.Log($"[Push_harmony] Agent reminders registered: {registeredCount}, " +
                      $"Total={_agentRegisteredMap.Count}");
        }

        /// <summary>
        /// 注册指定日期范围内的排期
        /// ★ 按日期分配文案（同一天的早中晚用同一编号）
        /// </summary>
        private int RegisterScheduleRange(
            PushSlot[] schedule, UserSegment segment,
            DateTime today, DateTime now,
            int startDayOffset, int endDayOffset)
        {
            int count = 0;

            foreach (var slot in schedule)
            {
                for (int i = startDayOffset; i < endDayOffset; i++)
                {
                    DateTime d = today.AddDays(i);
                    if (Array.IndexOf(slot.Days, d.DayOfWeek) < 0) continue;

                    DateTime trigger = d.Date + slot.Time;
                    if (trigger <= now) continue;

                    // App 在前台，且触发时间很近（1 分钟内），跳过注册
                    if (Application.isFocused && (trigger - now).TotalSeconds <= 60)
                    {
                        Debug.Log($"[Push_harmony] Skip imminent reminder (App in foreground): {trigger:yyyy-MM-dd HH:mm:ss}");
                        continue;
                    }

                    int slotId = GetSlotId(slot.Pool, segment);
                    string triggerKey = $"{slotId}_{trigger.Ticks}";

                    // ★ 按日期取文案
                    var data = GetTextForDate(slot.Pool, d);
                    if (data == null)
                    {
                        Debug.LogWarning($"[Push_harmony] 文案池 {slot.Pool} 取不到数据，跳过注册");
                        continue;
                    }

                    bool ok = PublishAgentReminder(trigger, data);
                    if (ok)
                    {
                        _agentRegisteredMap[triggerKey] = data.Key;
                        count++;

                        Debug.Log($"[Push_harmony] 分配并注册: {triggerKey} -> {data.Key} (date={d:yyyy-MM-dd})");
                    }
                }
            }

            return count;
        }

        private bool PublishAgentReminder(DateTime trigger, PushData data)
        {
            if (_harmonyProxy == null) return false;
            string title = string.IsNullOrEmpty(data.Title) ? DEFAULT_TITLE : data.Title;
            string body  = string.IsNullOrEmpty(data.Text)  ? data.Key : data.Text;
            try
            {
                _harmonyProxy.Call(
                    "PublishCalendarReminder",
                    trigger.Year, trigger.Month, trigger.Day,
                    trigger.Hour, trigger.Minute, trigger.Second,
                    title, body, data.Key, AGENT_RING_DURATION
                );
                Debug.Log($"[Push_harmony] Agent reminder call sent. Trigger={trigger:yyyy-MM-dd HH:mm:ss}, Key={data.Key}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Push_harmony] PublishAgentReminder failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// slotId 映射：
        ///   活跃用户：Morning=1, Noon=2, Night=3, Recall=4
        ///   衰退用户：Morning=11, Noon=12, Night=13, Recall=14
        /// </summary>
        private int GetSlotId(PushTextPool pool, UserSegment segment)
        {
            int baseId = segment == UserSegment.Active ? 0 : 10;
            switch (pool)
            {
                case PushTextPool.Morning: return baseId + 1;
                case PushTextPool.Noon:    return baseId + 2;
                case PushTextPool.Night:   return baseId + 3;
                case PushTextPool.Recall:  return baseId + 4;
            }
            return baseId + 1;
        }

        // ============================================================
        // 代理提醒：映射持久化
        // ============================================================
        private void CleanupExpiredAgentMap(DateTime now)
        {
            List<string> expiredKeys = null;
            foreach (var kv in _agentRegisteredMap)
            {
                int idx = kv.Key.IndexOf('_');
                if (idx < 0) continue;
                string ticksStr = kv.Key.Substring(idx + 1);
                if (!long.TryParse(ticksStr, out long ticks)) continue;
                if (new DateTime(ticks) < now)
                {
                    if (expiredKeys == null) expiredKeys = new List<string>();
                    expiredKeys.Add(kv.Key);
                }
            }
            if (expiredKeys != null)
            {
                foreach (var k in expiredKeys) _agentRegisteredMap.Remove(k);
                Debug.Log($"[Push_harmony] Cleaned up {expiredKeys.Count} expired agent map entries.");
            }
        }

        private void RestoreAgentRegisteredMap()
        {
            _agentRegisteredMap.Clear();
            string raw = PlayerPrefs.GetString(KEY_AGENT_REGISTERED_MAP, "");
            if (string.IsNullOrEmpty(raw)) return;
            try
            {
                var dict = JsonUtility.FromJson<SerializableDict>(raw);
                if (dict != null && dict.keys != null && dict.values != null
                    && dict.keys.Length == dict.values.Length)
                {
                    for (int i = 0; i < dict.keys.Length; i++)
                        _agentRegisteredMap[dict.keys[i]] = dict.values[i];
                }
                Debug.Log($"[Push_harmony] Restored {_agentRegisteredMap.Count} agent map entries.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Push_harmony] RestoreAgentRegisteredMap failed: {e.Message}");
                _agentRegisteredMap.Clear();
            }
        }

        private void SaveAgentRegisteredMap()
        {
            var dict = new SerializableDict();
            dict.keys = new string[_agentRegisteredMap.Count];
            dict.values = new string[_agentRegisteredMap.Count];
            int i = 0;
            foreach (var kv in _agentRegisteredMap)
            {
                dict.keys[i] = kv.Key;
                dict.values[i] = kv.Value;
                i++;
            }
            PlayerPrefs.SetString(KEY_AGENT_REGISTERED_MAP, JsonUtility.ToJson(dict));
            PlayerPrefs.Save();
        }

        [Serializable]
        private class SerializableDict
        {
            public string[] keys;
            public string[] values;
        }

        // ============================================================
        // 代理提醒：对外刷新 / 取消
        // ============================================================
        public void RefreshAgentReminders()
        {
            if (_harmonyProxy == null)
            {
                Debug.LogWarning("[Push_harmony] RefreshAgentReminders skipped: proxy not ready.");
                return;
            }

            _harmonyProxy.Call("CancelAllReminders");
            _agentRegisteredMap.Clear();
            SaveAgentRegisteredMap();

            UnityTimer.Delay(0.5f, () =>
            {
                RegisterAgentReminders();
                Debug.Log("[Push_harmony] Agent reminders refreshed.");
            });
        }

        public void CancelAllReminders()
        {
            if (_harmonyProxy == null) return;
            _harmonyProxy.Call("CancelAllReminders");
            _agentRegisteredMap.Clear();
            SaveAgentRegisteredMap();
            Debug.Log("[Push_harmony] CancelAllReminders issued.");
        }

        public void CancelAllAgentReminders()
        {
            if (_harmonyProxy == null) return;
            try
            {
                _harmonyProxy.Call("CancelAllReminders");
                _agentRegisteredMap.Clear();
                SaveAgentRegisteredMap();
                Debug.Log("[Push_harmony] All agent reminders cancelled.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Push_harmony] CancelAllAgentReminders failed: {e.Message}");
            }
        }

        public void QueryValidReminders()
        {
            if (_harmonyProxy == null) return;
            try
            {
                _harmonyProxy.Call("GetValidReminders");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Push_harmony] QueryValidReminders failed: {e.Message}");
            }
        }

        // ============================================================
        // 桥接回传
        // ============================================================
        public void OnReminderPublished(string json)
        {
            Debug.Log($"[Push_harmony] OnReminderPublished: {json}");
        }

        public void OnValidReminders(string json)
        {
            Debug.Log($"[Push_harmony] OnValidReminders: {json}");
        }

        public void OnNotificationClicked(string pushKey)
        {
            Debug.Log($"[Push_harmony] Notification clicked. pushKey={pushKey}");
        }

        // ============================================================
        // 对外推送 / 取消
        // ============================================================
        public void Push(string title, string body)
        {
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body))
            {
                Debug.LogError("[Push_harmony] Title and body are both empty.");
            }
            int id = _notificationIdCounter++;
            _activeNotificationIds.Add(id);
            OHSDKKitManager.Instance.PublishNotification(id, title, body, string.Empty);
            Debug.Log($"[Push_harmony] Manual push. Id={id}, Title={title}");
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
            }
            else
            {
                Debug.LogError($"[Push_harmony] GetToken error. Code: {signal.code}");
            }
        }
    }
}
#endif