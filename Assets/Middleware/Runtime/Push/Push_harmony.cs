#if UNITY_OPENHARMONY
using System;
using System.Collections.Generic;
using System.Globalization;
using Middleware;
using OpenHarmonyKits.Signal;
using UnityEngine;
using UnityEngine.OpenHarmony;

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
        private const string KEY_IDX_MORNING = "push_idx_morning";
        private const string KEY_IDX_NOON    = "push_idx_noon";
        private const string KEY_IDX_NIGHT   = "push_idx_night";
        private const string KEY_IDX_RECALL  = "push_idx_recall";

        /// <summary> 代理提醒已注册映射的持久化键 </summary>
        private const string KEY_AGENT_REGISTERED_MAP = "push_agent_registered_map";

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

        /// <summary> 本地调度 tick 间隔（秒） </summary>
        private const float TICK_INTERVAL = 30f;

        /// <summary> 通知标题兜底（配置表缺标题时使用） </summary>
        private const string DEFAULT_TITLE = "游戏提醒";

        /// <summary> 代理提醒预注册周数 </summary>
        private const int AGENT_REFRESH_WEEKS = 2;

        /// <summary> 代理提醒注册上限（系统上限 30，留 2 个余量） </summary>
        private const int AGENT_MAX_REMINDERS = 28;

        /// <summary> 代理提醒响铃时长（秒），必须 > 0 才有铃声 </summary>
        private const int AGENT_RING_DURATION = 5;

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

        /// <summary> ArkTS 桥接类（代理提醒原生接口），由 PushHarmonyProxy.tslib 提供 </summary>
        private static OpenHarmonyJSObject _harmonyProxy;

        /// <summary> 代理提醒已注册映射：key = "{slotId}_{ticks}"，value = 文案 Key </summary>
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

                // 1. 注册 Token 回调
                SignalHandler.Instance.RegisterSignalDelegate<Push_GetTokenSignal>(OnGetTokenTrigger);
                GetToken();
                Debug.Log("[Push_harmony] Agent reminder initialized.");
                
                // 2. 初始化代理提醒（系统级离线推送）
                InitAgentReminder();

                // 3. 启动本地即时通知调度器（应用运行期间的兜底）
                _initialized = true;
                StartScheduler();
            });
        }

        /// <summary> 外部主动销毁时调用 </summary>
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
        /// </summary>
        public void RequestEnableNotification()
        {
            // // 1. 检查是否已授权
            // if (Permission.HasUserAuthorizedPermission("ohos.permission.NOTIFICATION"))
            // {
            //     GameDataManager.Instance.UserData.IsAutoPush = true;
            //     Debug.Log("[Push_harmony] Notification permission already enabled.");
            //     return;
            // }

            // 2. 创建回调实例
            var callbacks = new PermissionCallbacks();

            // 3. 订阅授权成功事件
            callbacks.PermissionGranted += (permissionName) =>
            {
                Debug.Log($"[Push_harmony] Permission granted: {permissionName}");
                GameDataManager.Instance.UserData.IsAutoPush = true;
                // 可以在这里继续执行需要通知权限的逻辑，例如注册代理提醒
                // RegisterAgentReminders();
            };

            // 4. 订阅授权被拒绝事件
            callbacks.PermissionDenied += (permissionName) =>
            {
                Debug.Log($"[Push_harmony] Permission denied: {permissionName}");
                GameDataManager.Instance.UserData.IsAutoPush = false;
                // 引导用户去设置页面的逻辑
                //OHSDKKitManager.Instance.OpenNotificationSettings(); 
            };

            OHSDKKitManager.Instance.RequestEnableNotification();
            
            // 5. 发起权限请求并传入回调
            //Permission.RequestUserPermission("ohos.permission.NOTIFICATION", callbacks);
        }

        /// <summary> 获取 Push Token（本地通知不依赖，保留用于服务端上报） </summary>
        public void GetToken()
        {
            OHSDKKitManager.Instance.GetPushToken();
        }

        // ============================================================
        // 用户分层判断
        // ============================================================
        /// <summary>
        /// 分层判断：
        ///   活跃：离线时间 ≤ 4 天（含当天）
        ///   衰退：离线时间 ≥ 5 天
        /// 无有效 logoutTime（空串、"0"、格式异常）→ 视为活跃
        /// </summary>
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
        // 本地即时通知调度器（应用运行期间）
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

        /// <summary>
        /// 每次 tick 检查：当前时间是否命中某个推送时间槽；
        /// 若命中则执行本地即时推送（应用运行期间的兜底）。
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
        // 文案池：读取（不推进索引）
        // ============================================================
        /// <summary> 仅读取当前池的文案，不推进索引 </summary>
        private PushData PeekText(PushTextPool pool)
        {
            string prefix = GetPoolPrefix(pool);
            string key    = GetIndexKey(pool);

            int idx = PlayerPrefs.GetInt(key, 0);
            return PushManaer.Instance.GetPoolItem(prefix, idx);
        }

        /// <summary> 读取并推进索引（用于代理提醒一次性消耗文案） </summary>
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
        // 本地即时推送（应用运行期间，走 Notification Kit）
        // ============================================================
        private void DoLocalPush(PushTextPool pool, PushData data)
        {
            int notificationId = _notificationIdCounter++;
            _activeNotificationIds.Add(notificationId);

            string title = string.IsNullOrEmpty(data.Title) ? DEFAULT_TITLE : data.Title;
            string body  = string.IsNullOrEmpty(data.Text)  ? data.Key : data.Text;

            OHSDKKitManager.Instance.PublishNotification(
                notificationId,
                title,
                body,
                string.Empty
            );

            Debug.Log($"[Push_harmony] Local notification published. Id={notificationId}, " +
                      $"Pool={pool}, Key={data.Key}, Title={title}");
        }

        // ============================================================
        // 代理提醒：初始化
        // ============================================================
        /// <summary>
        /// 初始化代理提醒：
        ///   2) 创建高优先级通知渠道（横幅 + 铃声）
        ///   3) 恢复已注册映射
        ///   4) 首次注册提醒队列
        /// </summary>
        private void InitAgentReminder()
        {
            try
            {

                
                // 1. 构造 Bridge 实例（OpenHarmonyJSObject，匹配实例方法）
                _harmonyProxy = new OpenHarmonyJSObject("PushHarmonyProxy");

                // 2. 先注入配置（关键：必须在 PublishCalendarReminder 之前）
                _harmonyProxy.Call("SetConfig", BUNDLE_NAME, ABILITY_NAME);
                Debug.Log($"[Push_harmony] SetConfig sent: {BUNDLE_NAME} / {ABILITY_NAME}");

                // 3. 初始化代理提醒
                _harmonyProxy.Call("InitReminderAgent");

                // 4. 清理一次历史脏数据（仅首次调试时保留，正式版请删掉）
                // PlayerPrefs.DeleteKey(KEY_AGENT_REGISTERED_MAP);
                // PlayerPrefs.Save();

                // 5. 恢复映射并注册提醒
                RestoreAgentRegisteredMap();
                RegisterAgentReminders();

                RestoreAgentRegisteredMap();
                RegisterAgentReminders();

                Debug.Log("[Push_harmony] Agent reminder initialized.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Push_harmony] InitAgentReminder failed: {e.Message}");
            }
        }

        // ============================================================
        // 代理提醒：注册队列
        // ============================================================
        /// <summary>
        /// 遍历当前分层排期，为未来 AGENT_REFRESH_WEEKS 周内的每个触发日期
        /// 注册一条代理提醒。已注册的日期会跳过。
        /// </summary>
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

                    // 取文案并推进索引
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
            Debug.Log($"[Push_harmony] Agent reminders registered: {registeredCount}, " +
                      $"Total={_agentRegisteredMap.Count}, Segment={segment}");
        }

        /// <summary>
        /// 发布单条代理提醒。ArkTS 侧自行获取并缓存 AbilityContext，
        /// C# 侧只传触发时间、标题、文案、文案 Key。
        /// </summary>
        private bool PublishAgentReminder(DateTime trigger, PushData data)
        {
            if (_harmonyProxy == null) return false;

            string title = string.IsNullOrEmpty(data.Title) ? DEFAULT_TITLE : data.Title;
            string body  = string.IsNullOrEmpty(data.Text)  ? data.Key : data.Text;

            try
            {
                // 参数：年 月 日 时 分 秒 标题 文案 文案Key 响铃时长
                _harmonyProxy.Call(
                    "PublishCalendarReminder",
                    trigger.Year, trigger.Month, trigger.Day,
                    trigger.Hour, trigger.Minute, trigger.Second,
                    title, body, data.Key, AGENT_RING_DURATION
                );

                Debug.Log($"[Push_harmony] Agent reminder call sent. " +
                          $"Trigger={trigger:yyyy-MM-dd HH:mm:ss}, Key={data.Key}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Push_harmony] PublishAgentReminder failed: {e.Message}");
                return false;
            }
        }

        /// <summary> slotId 映射：活跃用户 1-3，衰退用户 11-14 </summary>
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
        /// <summary>
        /// 刷新代理提醒队列。建议在游戏回到前台 / 切后台时调用。
        /// 会重新读取分层、清理过期记录、补充新的提醒。
        /// </summary>
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

        /// <summary> 取消所有代理提醒（测试或用户关闭推送时调用） </summary>
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

        /// <summary> 查询系统侧仍有效的提醒（用于触达率估算） </summary>
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
        // 桥接回传：接收 ArkTS 侧调用
        // 需要在场景中放置名为 "PushManager" 的 GameObject，
        // 并把 Push_harmony 的接收方法绑定到其上。
        // ============================================================
        /// <summary> 接收单条提醒发布结果 </summary>
        public void OnReminderPublished(string json)
        {
            Debug.Log($"[Push_harmony] OnReminderPublished: {json}");
            // 可解析 { success, reminderId, pushKey, errorCode } 做进一步处理
        }

        /// <summary> 接收系统有效提醒 ID 列表 </summary>
        public void OnValidReminders(string json)
        {
            Debug.Log($"[Push_harmony] OnValidReminders: {json}");
            // 可解析 { ids: [...] }，用 ids.Length / _agentRegisteredMap.Count 估算触达率
        }

        /// <summary> 接收通知点击事件（从 EntryAbility 转发） </summary>
        public void OnNotificationClicked(string pushKey)
        {
            Debug.Log($"[Push_harmony] Notification clicked. pushKey={pushKey}");
            // 埋点上报打开事件
            // AnalyticsManager.Instance.TrackEvent("notification_click",
            //     new Dictionary<string, object> { { "push_key", pushKey } });
        }

        // ============================================================
        // 对外推送 / 取消（本地即时通知，供业务调用）
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