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