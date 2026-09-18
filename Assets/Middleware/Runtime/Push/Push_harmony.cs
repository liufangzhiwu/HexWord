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
        private const string KEY_IDX_MORNING = "push_idx_morning";
        private const string KEY_IDX_NOON    = "push_idx_noon";
        private const string KEY_IDX_NIGHT   = "push_idx_night";
        private const string KEY_IDX_RECALL  = "push_idx_recall";

        private const string KEY_AGENT_REGISTERED_MAP = "push_agent_registered_map";

        /// <summary> 是否已经向用户请求推送授权（由 prefab 配置写入，0/1） </summary>
        private string KEY_IS_PUSH_REQUESTED = "is_push_requested";

        private const int ACTIVE_DAYS = 5;
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private const long TIMESTAMP_MS_THRESHOLD = 99999999999L;

        private static readonly TimeSpan MORNING_TIME = new TimeSpan(6, 58, 0);
        private static readonly TimeSpan NOON_TIME    = new TimeSpan(14, 0, 0);
        private static readonly TimeSpan NIGHT_TIME   = new TimeSpan(21, 0, 0);

        private const int SLOT_TOLERANCE_SECONDS = 60;
        private const float TICK_INTERVAL = 30f;
        private const string DEFAULT_TITLE = "游戏提醒";
        private const int AGENT_REFRESH_WEEKS = 2;
        private const int AGENT_MAX_REMINDERS = 82;
        private const int AGENT_RING_DURATION = 5;

        // ---- 活跃用户排期 ----
        private static readonly PushSlot[] ActiveSchedule = new PushSlot[]
        {
            new PushSlot { Days = new[]{ DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }, Time = MORNING_TIME, Pool = PushTextPool.Morning },
            new PushSlot { Days = new[]{ DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }, Time = NOON_TIME, Pool = PushTextPool.Noon },
            new PushSlot { Days = new[]{ DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }, Time = NIGHT_TIME, Pool = PushTextPool.Night },
        };

        // ---- 衰退/预流失用户排期 ----
        private static readonly PushSlot[] ChurnSchedule = new PushSlot[]
        {
            new PushSlot { Days = new[]{ DayOfWeek.Monday, DayOfWeek.Wednesday }, Time = MORNING_TIME, Pool = PushTextPool.Morning },
            new PushSlot { Days = new[]{ DayOfWeek.Tuesday, DayOfWeek.Thursday }, Time = NOON_TIME, Pool = PushTextPool.Noon },
            new PushSlot { Days = new[]{ DayOfWeek.Saturday }, Time = NIGHT_TIME, Pool = PushTextPool.Recall },
            new PushSlot { Days = new[]{ DayOfWeek.Friday, DayOfWeek.Sunday }, Time = NIGHT_TIME, Pool = PushTextPool.Night },
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
                GetToken();

                InitAgentReminder();
                _initialized = true;
                StartScheduler();
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
        // 接收 Bridge 回传的授权结果
        // ============================================================
        // ============================================================
        private IEnumerator CheckNotificationAndInit()
        {
            // ★ 从 PlayerPrefs 写入的值（0/1）
            int isPushRequested = PlayerPrefs.GetInt(KEY_IS_PUSH_REQUESTED, 0);
           
            
            // ★ 使用泛型指定返回类型为 bool
            _harmonyProxy.Call("RequestNotificationEnable");
            yield return new WaitForSeconds(0.1f);
            //Debug.Log("[Push_harmony] 通知权限弹窗是否完成"+task);
            int pushNumber = ReadIsAutoPushFromFile();
            bool enabled =pushNumber==1;
            
            GameDataManager.Instance.UserData.IsAutoPush = enabled;
            
            if (enabled)
            {
                Debug.Log("[Push_harmony] 通知权限已授权");
                
                if(isPushRequested==0)
                    AnalyticMgr.PopAccept(popName: "消息推送");
                
                KEY_IS_PUSH_REQUESTED = "1";
                PlayerPrefs.SetInt(KEY_IS_PUSH_REQUESTED, 0);

                UnityTimer.Delay(0.5f, () =>
                {
                    RestoreAgentRegisteredMap();
                    RegisterAgentReminders();
                    Debug.Log("[Push_harmony] Agent reminder initialized after notification enable.");

                });
            }
            else
            {
                Debug.Log("[Push_harmony] 通知未授权，3秒后重试...");
                
                if(isPushRequested==0)
                    AnalyticMgr.PopRefuse(popName: "消息推送");
                
                KEY_IS_PUSH_REQUESTED = "1";
                PlayerPrefs.SetInt(KEY_IS_PUSH_REQUESTED, 0);
               
                yield return new WaitForSeconds(3f);
                Game.self.StartCoroutine(CheckNotificationAndInit());
               
            }
        }
        
          // ============================================================
        // 接收 Bridge 回传的授权结果
        // ============================================================
        // ============================================================
        private IEnumerator ResetCheckNotificationAndInit()
        {
            // ★ 使用泛型指定返回类型为 bool
            _harmonyProxy.Call("RequestNotificationEnable");
            yield return new WaitForSeconds(0.1f);
            //Debug.Log("[Push_harmony] 通知权限弹窗是否完成"+task);
            int pushNumber = ReadIsAutoPushFromFile();
            bool enabled =pushNumber==1;
            
            GameDataManager.Instance.UserData.IsAutoPush = enabled;
            
            if (enabled)
            {
                Debug.Log("[Push_harmony] 通知权限已授权");
                AnalyticMgr.PopAccept(popName: "消息推送");

                UnityTimer.Delay(0.5f, () =>
                {
                    RestoreAgentRegisteredMap();
                    RegisterAgentReminders();
                    Debug.Log("[Push_harmony] Agent reminder initialized after notification enable.");

                    EventDispatcher.instance.TriggerChangeGoldUI(0,false);
                });
            }
            else
            {
                Debug.Log("[Push_harmony] 通知未授权，3秒后重试...");
                AnalyticMgr.PopRefuse(popName: "消息推送");
                
                EventDispatcher.instance.TriggerChangeGoldUI(0,false);
                
                yield return new WaitForSeconds(3f);
                Game.self.StartCoroutine(CheckNotificationAndInit());
               
            }
        }

        /// <summary> 从 JSON 文件读取 IsAutoPush 值（0/1） </summary>
        public int ReadIsAutoPushFromFile()
        {
            try
            {
                // ★ 路径与 ArkTS 侧 ctx.filesDir 保持一致
                string path = Path.Combine(Application.persistentDataPath, "is_auto_push.json");

                if (!File.Exists(path))
                {
                    Debug.Log($"[Push_harmony] is_auto_push.json not found at {path}");
                    return 0;
                }

                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<IsAutoPushData>(json);
                int value = data != null ? data.IsAutoPush : 0;
                Debug.Log($"[Push_harmony] Read IsAutoPush from file: {value}");
                return value;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Push_harmony] ReadIsAutoPushFromFile failed: {e.Message}");
                return 0;
            }
        }

        [Serializable]
        private class IsAutoPushData
        {
            public int IsAutoPush;
        }


        // ============================================================
        // 权限请求
        // ============================================================
        public void RequestEnableNotification()
        {
            // ★ 从 PlayerPrefs 写入的值（0/1）
            int isPushRequested = PlayerPrefs.GetInt(KEY_IS_PUSH_REQUESTED, 0);

            if (isPushRequested == 0)
            {
                AnalyticMgr.PopShow(popName: "消息推送");
                OHSDKKitManager.Instance.RequestEnableNotification();
                Debug.Log("[Push_harmony] RequestEnableNotification issued.");
            }
            else
            {
                Debug.Log("[Push_harmony] Push disabled by config, skip RequestEnableNotification.");
            }
            
            UnityTimer.Delay(5f, () =>
            {
                Game.self.StartCoroutine(CheckNotificationAndInit());
            });
        }
        
        // ============================================================
        // 重置推送权限请求
        // ============================================================
        public void ReSetRequestEnableNotification()
        {
            AnalyticMgr.PopShow(popName: "消息推送");
            OHSDKKitManager.Instance.RequestEnableNotification();
            Debug.Log("[Push_harmony] RequestEnableNotification issued.");
            
            UnityTimer.Delay(5f, () =>
            {
                Game.self.StartCoroutine(ResetCheckNotificationAndInit());
            });
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
            DateTime now = DateTime.Now;
            UserSegment segment = GetUserSegment();
            PushSlot[] schedule = segment == UserSegment.Active ? ActiveSchedule : ChurnSchedule;

            foreach (var slot in schedule)
            {
                if (Array.IndexOf(slot.Days, now.DayOfWeek) < 0) continue;
                var diff = now.TimeOfDay - slot.Time;
                if (diff.TotalSeconds < 0 || diff.TotalSeconds > SLOT_TOLERANCE_SECONDS) continue;
                var data = PeekText(slot.Pool);
                if (data == null) continue;
                DoLocalPush(slot.Pool, data);
                Debug.Log($"[Push_harmony] Local push. Segment={segment}, Slot={slot.Pool}, Key={data.Key}");
                return;
            }
        }

        // ============================================================
        // 文案池
        // ============================================================
        private PushData PeekText(PushTextPool pool)
        {
            string prefix = GetPoolPrefix(pool);
            string key    = GetIndexKey(pool);
            int idx = PlayerPrefs.GetInt(key, 0);
            return PushManaer.Instance.GetPoolItem(prefix, idx);
        }

        private PushData PeekAndAdvanceText(PushTextPool pool)
        {
            string prefix = GetPoolPrefix(pool);
            string key    = GetIndexKey(pool);
            int idx = PlayerPrefs.GetInt(key, 0);
            var data = PushManaer.Instance.GetPoolItem(prefix, idx);
            if (data == null) return null;
            PlayerPrefs.SetInt(key, idx + 1);
            PlayerPrefs.Save();
            return data;
        }

        private string GetPoolPrefix(PushTextPool pool)
        {
            switch (pool)
            {
                case PushTextPool.Morning: return PushManaer.POOL_MORNING;
                case PushTextPool.Noon:    return PushManaer.POOL_NOON;
                case PushTextPool.Night:   return PushManaer.POOL_NIGHT;
                case PushTextPool.Recall:  return PushManaer.POOL_RECALL;
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
        // 本地即时推送
        // ============================================================
        private void DoLocalPush(PushTextPool pool, PushData data)
        {
            int notificationId = _notificationIdCounter++;
            _activeNotificationIds.Add(notificationId);
            string title = string.IsNullOrEmpty(data.Title) ? DEFAULT_TITLE : data.Title;
            string body  = string.IsNullOrEmpty(data.Text)  ? data.Key : data.Text;
            OHSDKKitManager.Instance.PublishNotification(notificationId, title, body, string.Empty);
            Debug.Log($"[Push_harmony] Local notification published. Id={notificationId}, Pool={pool}, Key={data.Key}, Title={title}");
        }

        // ============================================================
        // 代理提醒：初始化
        // ============================================================
        private void InitAgentReminder()
        {
            Debug.Log("[Push_harmony] Agent reminder initialized.");

            try
            {
                
                _harmonyProxy = new OpenHarmonyJSObject("PushHarmonyProxy");

                string filesDir = Application.persistentDataPath;
                if (string.IsNullOrEmpty(filesDir))
                {
                    // ★ OpenHarmony 沙箱固定路径
                    filesDir = "/data/storage/el2/base/haps/entry/files";
                    Debug.LogWarning($"[Push_harmony] persistentDataPath empty, using fallback: {filesDir}");
                }

                Debug.Log($"[Push_harmony] filesDir = '{filesDir}'");
                _harmonyProxy.Call("SetConfig", BUNDLE_NAME, ABILITY_NAME, filesDir);
                _harmonyProxy.Call("InitReminderAgent");

                CancelAllReminders();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Push_harmony] InitAgentReminder failed: {e.Message}");
            }
        }

        // ============================================================
        // 代理提醒：注册队列
        // ============================================================
        private void RegisterAgentReminders()
        {
            if (_harmonyProxy == null)
            {
                Debug.LogWarning("[Push_harmony] RegisterAgentReminders skipped: proxy not ready.");
                return;
            }

            UserSegment segment = GetUserSegment();
            PushSlot[] schedule = segment == UserSegment.Active ? ActiveSchedule : ChurnSchedule;
            DateTime now = DateTime.Now;
            DateTime today = now.Date;
            CleanupExpiredAgentMap(now);
            int registeredCount = 0;

            foreach (var slot in schedule)
            {
                for (int i = 0; i < AGENT_REFRESH_WEEKS * 7; i++)
                {
                    if (_agentRegisteredMap.Count >= AGENT_MAX_REMINDERS)
                    {
                        Debug.LogWarning($"[Push_harmony] Reached AGENT_MAX_REMINDERS ({AGENT_MAX_REMINDERS}), stop.");
                        break;
                    }

                    DateTime d = today.AddDays(i);
                    if (Array.IndexOf(slot.Days, d.DayOfWeek) < 0) continue;

                    DateTime trigger = d.Date + slot.Time;
                    if (trigger <= now) continue;

                    int slotId = GetSlotId(slot.Pool, segment);
                    string triggerKey = $"{slotId}_{trigger.Ticks}";
                    if (_agentRegisteredMap.ContainsKey(triggerKey)) continue;

                    var data = PeekAndAdvanceText(slot.Pool);
                    if (data == null)
                    {
                        Debug.LogWarning($"[Push_harmony] 文案池 {slot.Pool} 为空，跳过注册");
                        continue;
                    }

                    bool ok = PublishAgentReminder(trigger, data);
                    if (ok)
                    {
                        _agentRegisteredMap[triggerKey] = data.Key;
                        registeredCount++;
                    }
                }
                if (_agentRegisteredMap.Count >= AGENT_MAX_REMINDERS) break;
            }

            SaveAgentRegisteredMap();
            Debug.Log($"[Push_harmony] Agent reminders registered: {registeredCount}, Total={_agentRegisteredMap.Count}, Segment={segment}");
        }

        public void CancelAllReminders()
        {
            // ★ 先取消系统内所有旧提醒，避免数量累积超限
            _harmonyProxy.Call("CancelAllReminders");
            Debug.Log("[Push_harmony] CancelAllReminders issued.");
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
            RestoreAgentRegisteredMap();
            RegisterAgentReminders();
            Debug.Log("[Push_harmony] Agent reminders refreshed.");
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
                Debug.LogError($"[Push_harmony] GetToken Error. Code: {signal.code}, Message: {signal.message}");
            }
        }
    }
}
#endif