using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Middleware
{
    public interface IAccounts
    {
        public bool IsLogin { get; set; }
        void Init(float delay);
        void Login();
        void Logout();
        
        void VerifyPlayer();
        
        // void ShowBanner();
        // void HideBanner();
    }
}