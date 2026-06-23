using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Middleware
{
    public interface IAttribute
    {
        //public string UserId { get; set; }
        //public bool Is { get; set; }
        void Init(float delay);
        void ReportConversion(int eventCode);

        void ReportPurchase(long actionTime, decimal amount, string currency = "CNY");
        void ReportRetention(long actionTime);
    }
}