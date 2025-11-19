using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Unity.Passport.Runtime.UI
{
    public class PassportUIConfig
    {
        [Tooltip("是否自动旋转屏幕方向")]
        public bool AutoRotation = true;

        [Tooltip("是否通过自行调用 Login 函数启动登录面板。该值为 true 时，请先调用 Init 后调用 Login 来唤起登录窗口。")]
        public bool InvokeLoginManually = false;

        [Tooltip("UI风格主题")]
        public PassportUITheme Theme = PassportUITheme.Dark;
        
        [Tooltip("WebGL 场景下 Unity 实例容器 Id，用于挂载微信扫码 iframe。")] 
        public string UnityContainerId = "unity-container";
    }

    public enum PassportUITheme
    {
        Light = 0,
        Dark = 1
    }

    public enum PassportEvent
    {
        RejectedTos = 1000, // 用户拒绝协议
        LoggedIn = 1001, // 已完成登录
        Completed = 1002, // 已完成协议同意（如有）、用户登录和实名认证（如有）
        LoggedOut = 1003, // 用户登出
    }

    public abstract class PassportUIBase : MonoBehaviour
    {
        public Action<PassportEvent> OnCallback;
        public bool invokeLoginManually;
        public PassportUIConfig Config = new();
        public static PassportUIBase Instance => _instance;
        private static PassportUIBase _instance;
        
        private void Start()
        {
            if (_instance != null)
            {
                Destroy(this);
            }
            else
            {
                _instance = this;
            }
        }

#if UNITY_WEBGL
        [DllImport("__Internal")]
        protected static extern void passportWebWechatLogin(string config);

        [DllImport("__Internal")]
        protected static extern void passportWebWechatLoginComplete();

        protected class PassportWebWechatConfig
        {
            public string AppID;
            public string RedirectUri;
            public string State;
            public string Scope;
            public string UnityContainerId;
            public string HeaderHeight;
        }
#endif


        [Flags]
        protected enum UserStatus
        {
            Tos = 0b_0001,
            LoggedIn = 0b_0010,
            RealName = 0b_0100,
            ReLogin = 0b_1000,
        }
        
        protected enum LoginType
        {
            None = 0, // 新账号登录
            LoginByUserInfo = 1, // 根据之前的用户信息自动登录
            ReLogin = 2, // 通过重新登录面板登录
        }

        public virtual Task Init()
        {
            return Task.CompletedTask;
        }

        public virtual void Login()
        {
        }

        public virtual void Logout()
        {
        }
    }
}