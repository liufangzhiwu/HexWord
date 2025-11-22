#if UNITY_OPENHARMONY
using System;
using System.Collections.Generic;
using System.Threading;
using ThinkingData.Analytics;
using UnityEngine;

namespace Middleware
{
    public class Analytics_harmony : IAnalytics
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
            //TDAnalytics.EnableAutoTrack( TDAutoTrackEventType.AppInstall | TDAutoTrackEventType.AppEnd);
            TDAnalytics.EnableAutoTrack(TDAutoTrackEventType.AppStart | TDAutoTrackEventType.AppInstall| TDAutoTrackEventType.AppEnd);
            OnSdkInit?.Invoke(this,null);
            
            //Debug.Log($"线程ID: {Thread.CurrentThread.ManagedThreadId}");
        }
    }
}
#endif