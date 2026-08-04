using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Middleware
{
    public interface IPushs
    {   
        void Init(float delay);
        /// <summary>
        /// 获取Token
        /// </summary>
        void GetToken();
        
    }
}