using System;
using Middleware;
using UnityEngine;
using Xiaomi.GameSDK;

// =====================================================================
// 补单查询回调接口（与小米 SDK 的 QueryOrderCallback 对应）
// =====================================================================
namespace Xiaomi.GameSDK
{
    public interface IMiSDKQueryOrderCallback
    {
        /// <summary>
        /// 查询结果回调
        /// </summary>
        /// <param name="status">
        /// WAIT_BUYER_PAY // 未付款
        /// TRADE_SUCCESS // 支付成功
        /// TRADE_CLOSED  // 关闭
        /// TRADE_FAIL    // 支付失败
        /// TRADE_TIMEOUT // 超时
        /// REPEAT_PURCHASE // 重复购买
        /// </param>
        void OnQueryResult(string status);

        /// <summary>
        /// 查询失败回调
        /// </summary>
        void OnQueryError(int code);
    }
}

// =====================================================================
// 登录回调（保留原始实现）
// =====================================================================
public class MyLoginCallback : IMiSDKLoginCallback
{
    public void FinishLoginProcess(int code, MiAccountInfo var2)
    {
        switch (code)
        {
            case 0:
                Debug.Log("login succeed: id=" + var2.uid + " session=" + var2.sessionId);
                GameDataManager.Instance.UserData.UserId = var2.uid.ToString();
                break;
            default:
                Debug.Log("login failed");
                Game.self.ShowLoginErrorPanel();
                break;
        }
    }
}

// =====================================================================
// 退出回调（保留原始实现）
// =====================================================================
public class MyExitCallback : IMiSDKExitCallback
{
    public void OnExit(int code)
    {
        if (code == 10001)
        {
            Application.Quit();
        }
    }
}

// =====================================================================
// 小米 SDK 封装类
// =====================================================================
public class SDKAndroid : Xiaomi.Singleton<SDKAndroid>
{
    AndroidJavaObject activity;
    AndroidJavaObject sdk;

    // -----------------------------------------------------------------
    // 初始化
    // -----------------------------------------------------------------
    public void Init()
    {
        AndroidJavaClass unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        Debug.Log("unityPlayerClass : " + unityPlayerClass);
        activity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity");
        Debug.Log("activity : " + activity);

        AndroidJavaClass sdkClass = new AndroidJavaClass("com.xiaomi.gamecenter.sdk.MiCommplatform");
        Debug.Log("sdkClass : " + sdkClass);
        sdk = sdkClass.CallStatic<AndroidJavaObject>("getInstance");
        Debug.Log("sdk : " + sdk);
    }

    // -----------------------------------------------------------------
    // 用户协议
    // -----------------------------------------------------------------
    public void OnUserAgreed()
    {
        sdk.Call("onUserAgreed", activity);
    }

    // -----------------------------------------------------------------
    // 登录
    // -----------------------------------------------------------------
    public void OnStartLogin(IMiSDKLoginCallback callback)
    {
        sdk.Call("miLogin", activity, new SDKLoginCallback(callback));
    }

    // -----------------------------------------------------------------
    // 支付（计费点）
    // -----------------------------------------------------------------
    public void OnProduceCodePay(string productCode, int count, string cpOrderId, string cpUserInfo, IMiSDKPayCallback callback)
    {
        sdk.Call<int>("miUniPay", activity,
            createProductCodeBuyInfo(productCode, count, cpOrderId, cpUserInfo), new SDKPayCallback(callback));
    }

    // -----------------------------------------------------------------
    // 支付（按金额）
    // -----------------------------------------------------------------
    public void OnAmountPay(int amount, string cpOrderId, string cpUserInfo, IMiSDKPayCallback callback)
    {
        sdk.Call<int>("miUniPay", activity,
            createAmountBuyInfo(amount, cpOrderId, cpUserInfo), new SDKPayCallback(callback));
    }

    // -----------------------------------------------------------------
    // 上报订单发货状态
    // -----------------------------------------------------------------
    public void OnReportOrder(string cpOrderId, bool isDelivery, string errMsg = null)
    {
        sdk.Call("miReportOrder",
            createMiReportOrder(cpOrderId, isDelivery, errMsg));
    }

    // -----------------------------------------------------------------
    // 退出
    // -----------------------------------------------------------------
    public void OnAppExit(IMiSDKExitCallback callback)
    {
        sdk.Call("miAppExit", activity, new SDKExitCallback(callback));
    }

    // =================================================================
    // 补单查询（反射动态查找回调接口）
    // =================================================================
    /// <summary>
    /// 查询订单状态（补单用）
    /// </summary>
    /// <param name="cpOrderId">游戏订单号</param>
    /// <param name="callback">查询结果回调</param>
    public void QueryOrderStatus(string cpOrderId, IMiSDKQueryOrderCallback callback)
    {
        try
        {
            string interfaceName = FindQueryOrderCallbackInterface();
            Debug.Log($"[SDKAndroid] Using QueryOrderCallback interface: {interfaceName}");

            var proxy = new SDKQueryOrderCallbackReflect(interfaceName, callback);
            sdk.Call("queryOrderStatus", cpOrderId, proxy);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SDKAndroid] QueryOrderStatus exception: {e}");
            callback?.OnQueryError(-1);
        }
    }

    /// <summary>
    /// 尝试查找实际存在的 QueryOrderCallback 接口类名
    /// </summary>
    private static string FindQueryOrderCallbackInterface()
    {
        // 候选类名列表（按可能性排序）
        string[] candidates = {
            "com.xiaomi.gamecenter.sdk.QueryOrderCallback",
            "com.xiaomi.gamecenter.sdk.OnQueryOrderCallback",
            "com.xiaomi.gamecenter.sdk.IQueryOrderCallback",
            "com.xiaomi.gamecenter.sdk.entry.QueryOrderCallback",
            "com.xiaomi.gamecenter.sdk.QueryOrderStatusCallback",
            "com.xiaomi.gamecenter.sdk.OnQueryOrderStatusCallback"
        };

        foreach (var name in candidates)
        {
            try
            {
                using (var clazz = new AndroidJavaClass(name))
                {
                    // 如果成功加载，说明该类存在
                    Debug.Log($"[SDKAndroid] Found QueryOrderCallback class: {name}");
                    return name;
                }
            }
            catch
            {
                // 继续尝试下一个
            }
        }

        Debug.LogError("[SDKAndroid] QueryOrderCallback class not found in any candidate package! " +
                       "请确认 SDK 包是否包含补单接口，或联系小米技术支持。");
        // 返回默认值，让上层捕获异常
        return candidates[0];
    }

    // =================================================================
    // 支付信息构建辅助方法
    // =================================================================
    public AndroidJavaObject createProductCodeBuyInfo(string productCode, int count, string cpOrderId, string cpUserInfo)
    {
        AndroidJavaObject buyInfo = new AndroidJavaObject("com.xiaomi.gamecenter.sdk.entry.MiBuyInfo");
        buyInfo.Call("setProductCode", productCode);
        buyInfo.Call("setCount", count);
        buyInfo.Call("setCpOrderId", cpOrderId);
        buyInfo.Call("setCpUserInfo", cpUserInfo);
        return buyInfo;
    }

    public AndroidJavaObject createAmountBuyInfo(int amount, string cpOrderId, string cpUserInfo)
    {
        AndroidJavaObject buyInfo = new AndroidJavaObject("com.xiaomi.gamecenter.sdk.entry.MiBuyInfo");
        buyInfo.Call("setAmount", amount);
        buyInfo.Call("setCpOrderId", cpOrderId);
        buyInfo.Call("setCpUserInfo", cpUserInfo);
        return buyInfo;
    }

    public AndroidJavaObject createMiReportOrder(string cpOrderId, bool isDelivery, string errMsg)
    {
        AndroidJavaObject miReportOrder = new AndroidJavaObject("com.xiaomi.gamecenter.sdk.entry.MiReportOrder");
        miReportOrder.Call("setCpOrderId", cpOrderId);
        miReportOrder.Call("setDelivery", isDelivery);
        miReportOrder.Call("setErrMsg", errMsg);
        return miReportOrder;
    }

    // =================================================================
    // 回调桥接类
    // =================================================================

    /// <summary>
    /// 登录回调桥接
    /// </summary>
    public class SDKLoginCallback : AndroidJavaProxy
    {
        IMiSDKLoginCallback loginCallback;
        public SDKLoginCallback(IMiSDKLoginCallback loginCallback) : base("com.xiaomi.gamecenter.sdk.OnLoginProcessListener")
        {
            this.loginCallback = loginCallback;
        }

        void finishLoginProcess(int code, AndroidJavaObject var2)
        {
            if (loginCallback != null)
            {
                loginCallback.FinishLoginProcess(code, MiAccountInfo.parse(var2));
            }
        }
    }

    /// <summary>
    /// 支付回调桥接
    /// </summary>
    public class SDKPayCallback : AndroidJavaProxy
    {
        private IMiSDKPayCallback callback;
        public SDKPayCallback(IMiSDKPayCallback callback) : base("com.xiaomi.gamecenter.sdk.OnPayProcessListener")
        {
            this.callback = callback;
        }

        void finishPayProcess(int code)
        {
            if (callback != null)
            {
                callback.FinishPayProcess(code);
            }
        }
    }

    /// <summary>
    /// 退出回调桥接
    /// </summary>
    public class SDKExitCallback : AndroidJavaProxy
    {
        private IMiSDKExitCallback callback;
        public SDKExitCallback(IMiSDKExitCallback callback) : base("com.xiaomi.gamecenter.sdk.OnExitListner")
        {
            this.callback = callback;
        }

        void onExit(int code)
        {
            if (callback != null)
            {
                callback.OnExit(code);
            }
        }
    }

    /// <summary>
    /// 补单查询回调桥接（反射版，动态指定接口类名）
    /// </summary>
    public class SDKQueryOrderCallbackReflect : AndroidJavaProxy
    {
        private IMiSDKQueryOrderCallback _callback;

        public SDKQueryOrderCallbackReflect(string interfaceName, IMiSDKQueryOrderCallback callback)
            : base(interfaceName)
        {
            _callback = callback;
        }

        // 方法名必须与 Java 接口中的方法名完全一致（大小写敏感）
        void queryResult(string status)
        {
            _callback?.OnQueryResult(status);
        }

        void onError(int code)
        {
            _callback?.OnQueryError(code);
        }
    }
}