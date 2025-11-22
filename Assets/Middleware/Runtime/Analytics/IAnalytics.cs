using System;
using System.Collections;
using System.Collections.Generic;

namespace Middleware
{
    public interface IAnalytics
    {
        void Init(float delay);
        
        void LogEvent(string key, Define.DataTarget targets);
        void LogEvent(string key, string parameterName, object parameterValue, Define.DataTarget targets);
        void LogEvent(string key, Dictionary<string, object> properties, Define.DataTarget targets);
        void SetUserProperty(string key, object property, Define.DataTarget targets);
        void SetUserProperty(Dictionary<string, object> properties, Define.DataTarget targets);
        void SetCommonProperties(Dictionary<string, object> properties);
        void Login(string uid);
        event EventHandler OnSdkInit;
    }
}