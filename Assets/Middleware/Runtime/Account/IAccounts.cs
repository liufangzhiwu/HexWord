using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Middleware
{
    public interface IAccounts
    {
        /// <summary>
        /// 平台端用户唯一值
        /// </summary>
        public string UserId { get; set; }
        /// <summary>
        /// 登录状态
        /// </summary>
        public bool IsLogin { get; set; }
        /// <summary>
        /// 授权状态
        /// </summary>
        public bool IsAuthorized { get; set; }
        void Init(float delay);
        void Login(Action<string> callback);
        void Logout();
        
        void VerifyPlayer();
        
        // void ShowBanner();
        // void HideBanner();
    }
}