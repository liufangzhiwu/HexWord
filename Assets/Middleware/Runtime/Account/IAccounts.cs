using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 登录状态枚举
/// 用于替代原先的 bool IsLogin，能区分"登录中/成功/失败/取消/超时"
/// </summary>
public enum LoginState
{
    None,       // 还没开始 / 未登录
    Logging,    // 已调用 SDK，等待回调（用户正在登录界面操作）
    Success,    // 登录成功
    Failed,     // SDK 明确返回错误
    Canceled,   // 用户主动取消
    Timeout     // 本地计时超时（不代表 SDK 失败）
}

namespace Middleware
{
    public interface IAccounts
    {
        /// <summary>
        /// 平台端用户唯一值
        /// </summary>
        public string UserId { get; set; }
        public bool IsLogin { get; set; }
        // ✅ 新的枚举状态
        //public LoginState State { get; set; }
        void Init(float delay);
        void Login(bool isShowLoginPanel = false);
        void Logout();
        
        void VerifyPlayer();
        
        // void ShowBanner();
        // void HideBanner();
    }
}