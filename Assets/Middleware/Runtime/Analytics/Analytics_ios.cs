#if UNITY_IOS
using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Analytics;
using ThinkingData.Analytics;
using UnityEngine;

namespace Middleware
{
    public class Analytics_ios : IAnalytics
    {
        public void Init(float delay)
        {
            UnityTimer.Delay(delay, () =>
            {
                InitThink();
                InitFirebase();
            });
        }
        
        public void LogEvent(string key, Define.DataTarget targets)
        {
            if (targets.HasFlag(Define.DataTarget.Think))
                TDAnalytics.Track(key);
            
            if (targets.HasFlag(Define.DataTarget.Firebase) && _isFirebaseInit)
                FirebaseAnalytics.LogEvent(key);
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
            
            if (targets.HasFlag(Define.DataTarget.Firebase) && _isFirebaseInit)
                FirebaseAnalytics.LogEvent(key, parameterName, parameterValue.ToString());
        }

        public void LogEvent(string key, Dictionary<string, object> properties, Define.DataTarget targets)
        {
            if (targets.HasFlag(Define.DataTarget.Think))
                TDAnalytics.Track(key, properties);

            if (targets.HasFlag(Define.DataTarget.Firebase) && _isFirebaseInit)
            {
                var list = new List<Parameter>();
                foreach (var d in properties)
                    list.Add(new Parameter(d.Key, d.Value.ToString()));
                FirebaseAnalytics.LogEvent(key, list.ToArray());
            }
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
            
            if (targets.HasFlag(Define.DataTarget.Firebase) && _isFirebaseInit)
                FirebaseAnalytics.SetUserProperty(key,property.ToString());
        }

        public void SetUserProperty(Dictionary<string, object> properties, Define.DataTarget targets)
        {
            if (targets.HasFlag(Define.DataTarget.Think))
                TDAnalytics.UserSet(properties);

            if (targets.HasFlag(Define.DataTarget.Firebase) && _isFirebaseInit)
            {
                foreach (var d in properties)
                    FirebaseAnalytics.SetUserProperty(d.Key, d.Value.ToString());
            }
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
            TDAnalytics.EnableAutoTrack(TDAutoTrackEventType.AppStart | TDAutoTrackEventType.AppInstall | TDAutoTrackEventType.AppEnd);
            OnSdkInit?.Invoke(this,null);
        }

        private bool _isFirebaseInit;
        private void InitFirebase()
        {
            FirebaseApp.CheckDependenciesAsync().ContinueWith(task =>
            {
                var depStatus = task.Result;
                if (depStatus == DependencyStatus.Available)
                {
                    Debug.Log("Firebase init Ok");
                    _isFirebaseInit = true;
                }
                else
                {
                    Debug.LogError("Firebase init failed: " + depStatus);
                    _isFirebaseInit = false;
                }
            });
        }
    }
}
#endif