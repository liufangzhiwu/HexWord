#if UNITY_ANDROID
using System;
using System.Collections.Generic;
using System.Threading;
using ThinkingData.Analytics;
using UnityEngine;

namespace Middleware
{
    public class Analytics_android : IAnalytics
    {
        public void Init(float delay)
        {
            UnityTimer.Delay(delay, InitThink);
        }

        public void LogEvent(string key, Define.DataTarget targets)
        {
            if (targets.HasFlag(Define.DataTarget.Think))
                TDAnalytics.Track(key);
        }

        public void LogEvent(string key, string parameterName, object parameterValue, Define.DataTarget targets)
        {
            if (targets.HasFlag(Define.DataTarget.Think))
            {
                var dic = new Dictionary<string, object>
                {
                    { parameterName, parameterValue }
                };
                TDAnalytics.Track(key,dic);
            }
        }

        public void LogEvent(string key, Dictionary<string, object> properties, Define.DataTarget targets)
        {
            if (targets.HasFlag(Define.DataTarget.Think))
                TDAnalytics.Track(key, properties);
        }

        public void SetUserProperty(string key, object property, Define.DataTarget targets)
        {
            if (targets.HasFlag(Define.DataTarget.Think))
            {
                var dic = new Dictionary<string, object>
                {
                    { key, property }
                };
                TDAnalytics.UserSet(dic);
            }
        }

        public void SetUserProperty(Dictionary<string, object> properties, Define.DataTarget targets)
        {
            if (targets.HasFlag(Define.DataTarget.Think)) 
                TDAnalytics.UserSet(properties);
        }

        /// <summary>
        /// 只写一次的用户属性（规范 user_setOnce，如 first_version / first_login_time）
        /// </summary>
        public void SetUserPropertyOnce(Dictionary<string, object> properties, Define.DataTarget targets)
        {
            if (targets.HasFlag(Define.DataTarget.Think))
                TDAnalytics.UserSetOnce(properties);
        }

        /// <summary>
        /// 登出：清账号ID + 清公共事件属性（规范接入必知要求）
        /// </summary>
        public void Logout()
        {
            TDAnalytics.Logout();
            TDAnalytics.ClearSuperProperties();
        }

        /// <summary>
        /// 所有事件都要带的公共属性
        /// </summary>
        public void SetCommonProperties(Dictionary<string, object> properties)
        {
            TDAnalytics.SetSuperProperties(properties);
        }

        public event EventHandler OnSdkInit;

        public void Login(string uid)
        {
            TDAnalytics.Login(uid);
        }

        private void InitThink()
        {
            var config = new TDConfig(Define.Config.ThinkAppId, Define.Config.ThinkServerUrl);
#if UNITY_EDITOR
            config.mode = TDMode.DebugOnly;
#else
            config.mode = TDMode.Normal;
#endif
#if !Unity_ShowLog && !UNITY_EDITOR
            TDAnalytics.EnableLog(false);
#endif
            TDAnalytics.Init(config);
            OnSdkInit?.Invoke(this,null);
            // 【故意关闭】ta_app_install 不再交给 SDK 原生判定，改为 AnalyticMgr.ReportInstallOnce() 自建上报。
            // 原生判定（反编译 TDAnalytics.aar / ThinkingAnalyticsSDK 确认）：
            //   1) 只认 firstInstallTime == lastUpdateTime 的"纯净首装"，覆盖安装（内测→正式 / adb install -r / 渠道换包）永远不认；
            //   2) firstInstallTime 在首次启动时无条件落盘，首次没报出去之后该设备永久不再上报；
            //   3) 上报列表是一次性消费（list.remove），失败也无法重试。
            // TDAnalytics.EnableAutoTrack( TDAutoTrackEventType.AppInstall);
            //TDAnalytics.EnableAutoTrack(TDAutoTrackEventType.AppStart | TDAutoTrackEventType.AppInstall| TDAutoTrackEventType.AppEnd);
            
            
            //Debug.Log($"线程ID: {Thread.CurrentThread.ManagedThreadId}");
        }
    }
}
#endif