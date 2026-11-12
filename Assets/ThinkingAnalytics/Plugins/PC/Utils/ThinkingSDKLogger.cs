using System;
using System.IO;
using ThinkingSDK.PC.Constant;
using ThinkingSDK.PC.Config;
using UnityEngine;
using System.Collections.Generic;

namespace ThinkingSDK.PC.Utils
{

    public interface OnLogPrintListener
    {
        void OnLogPrint(string msg);
    }

    public class ThinkingSDKLogger
    {
        public static OnLogPrintListener logListener;

        public ThinkingSDKLogger()
        {

        }

        public static void SetLogPrintListener(OnLogPrintListener listener)
        {
            logListener = listener;
        }

        public static void Print(string str)
        {
            if (ThinkingSDKPublicConfig.IsPrintLog())
            {
                Debug.Log("[ThinkingData] Info: " + str);
                if (logListener != null)
                {
                    logListener.OnLogPrint(str);
                }
            }
        }

        public static void PrintJson(string str, Dictionary<string, object> data)
        {
            if (ThinkingSDKPublicConfig.IsPrintLog())
            {
                // ★ 日志用 keepUnicode: true —— 中文原样输出，避免控制台里看到 "\u5185\u8D2D"
                string dataStr = str + ThinkingSDKJSON.Serialize(data, true);
                Debug.Log("[ThinkingData] Info: " + dataStr);
                if (logListener != null)
                {
                    logListener.OnLogPrint(dataStr);
                }
            }
        }

        public static void PrintJson1(string str, Dictionary<string, object> data, string str1)
        {
            if (ThinkingSDKPublicConfig.IsPrintLog())
            {
                // ★ 日志用 keepUnicode: true —— 中文原样输出，避免控制台里看到 "\u5185\u8D2D"
                string dataStr = str + ThinkingSDKJSON.Serialize(data, true) + str1;
                Debug.Log("[ThinkingData] Info: " + dataStr);
                if (logListener != null)
                {
                    logListener.OnLogPrint(dataStr);
                }
            }
        }

    }
}
