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
        void SetUserPropertyOnce(Dictionary<string, object> properties, Define.DataTarget targets);
        void SetCommonProperties(Dictionary<string, object> properties);
        void Login(string uid);

        /// <summary>
        /// 登出：清除账号ID与已设置的公共事件属性，避免换号后属性串号。
        /// </summary>
        void Logout();
        
        event EventHandler OnSdkInit;
    }
}