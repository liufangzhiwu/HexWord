using Middleware;
using UnityEngine;
using Xiaomi.GameSDK;

//登录回调
public class MyLoginCallback : IMiSDKLoginCallback
{
    public void FinishLoginProcess(int code, MiAccountInfo var2)
        // code为登陆结果
    {
        switch (code)
        {
            case 0:
                //登陆成功
                Debug.Log("login succeed: id=" + var2.uid + " session=" + var2.sessionId);
                break;
            default:
                Debug.Log("login failed");
                Game.self.ShowLoginErrorPanel();
                break;
        }
    }
}

public class MyExitCallback : IMiSDKExitCallback
{
    public void OnExit(int code)
    {
        if (code == 10001)
        {
            //可以退出游戏
            Application.Quit();
        }
        else
        {
            //不要退出游戏
        }
    }
}

public class SDKAndroid : Xiaomi.Singleton<SDKAndroid>
{
    AndroidJavaObject activity;
    AndroidJavaObject sdk;

    public void Init()
    {
        //获取名为UnityPlayer的类
        AndroidJavaClass unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        Debug.Log("unityPlayerClass : " + unityPlayerClass);
        //获取当前Activity
        activity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity");
        Debug.Log("activity : " + activity);
        //获取SDK类
        AndroidJavaClass sdkClass = new AndroidJavaClass("com.xiaomi.gamecenter.sdk.MiCommplatform");
        Debug.Log("sdkClass : " + sdkClass);
        //获取SDK对象
        sdk = sdkClass.CallStatic<AndroidJavaObject>("getInstance");
        Debug.Log("sdk : " + sdk);
    }

    public void OnUserAgreed() {
        sdk.Call("onUserAgreed", activity);
    }

    public void OnStartLogin(IMiSDKLoginCallback callback)
    {
        sdk.Call("miLogin", activity, new SDKLoginCallback(callback));
    }

    public void OnProduceCodePay(string productCode, int count, string cpOrderId, string cpUserInfo, IMiSDKPayCallback callback)
    {
        sdk.Call<int>("miUniPay", activity,
            createProductCodeBuyInfo(productCode, count, cpOrderId, cpUserInfo), new SDKPayCallback(callback));
    }

    public void OnAmountPay(int amount, string cpOrderId, string cpUserInfo, IMiSDKPayCallback callback)
    {
        sdk.Call<int>("miUniPay", activity,
            createAmountBuyInfo(amount, cpOrderId, cpUserInfo), new SDKPayCallback(callback));
    }

    public void OnReportOrder(string cpOrderId, bool isDelivery, string errMsg = null)
    {
        sdk.Call("miReportOrder",
            createMiReportOrder(cpOrderId, isDelivery, errMsg));
    }

    public void OnAppExit(IMiSDKExitCallback callback)
    {
        sdk.Call("miAppExit", activity, new SDKExitCallback(callback));
    }


    //生成按计费点支付对象
    public AndroidJavaObject createProductCodeBuyInfo(string productCode, int count, string cpOrderId, string cpUserInfo) {
        AndroidJavaObject buyInfo = new AndroidJavaObject("com.xiaomi.gamecenter.sdk.entry.MiBuyInfo");
        buyInfo.Call("setProductCode", productCode);
        buyInfo.Call("setCount", count);
        buyInfo.Call("setCpOrderId", cpOrderId);
        buyInfo.Call("setCpUserInfo", cpUserInfo);
        return buyInfo;
    }

    //生成按金额支付对象
    public AndroidJavaObject createAmountBuyInfo(int amount, string cpOrderId, string cpUserInfo)
    {
        AndroidJavaObject buyInfo = new AndroidJavaObject("com.xiaomi.gamecenter.sdk.entry.MiBuyInfo");
        buyInfo.Call("setAmount", amount);
        buyInfo.Call("setCpOrderId", cpOrderId);
        buyInfo.Call("setCpUserInfo", cpUserInfo);
        return buyInfo;
    }

    //生成订单信息
    public AndroidJavaObject createMiReportOrder(string cpOrderId, bool isDelivery, string errMsg)
    {
        AndroidJavaObject miReportOrder = new AndroidJavaObject("com.xiaomi.gamecenter.sdk.entry.MiReportOrder");
        miReportOrder.Call("setCpOrderId", cpOrderId);
        miReportOrder.Call("setDelivery", isDelivery);
        miReportOrder.Call("setErrMsg", errMsg);
        return miReportOrder;
    }

    /// <summary>
    /// 登录回调
    /// </summary>
    public class SDKLoginCallback : AndroidJavaProxy
    {
        IMiSDKLoginCallback loginCallback;
        public SDKLoginCallback(IMiSDKLoginCallback loginCallback) : base("com.xiaomi.gamecenter.sdk.OnLoginProcessListener") {
            this.loginCallback = loginCallback;
        }

        void finishLoginProcess(int code, AndroidJavaObject var2) {
            if (loginCallback != null) {
                loginCallback.FinishLoginProcess(code, MiAccountInfo.parse(var2));
            }
        }
    }

    /// <summary>
    /// 支付回调
    /// </summary>
    public class SDKPayCallback : AndroidJavaProxy
    {
        private IMiSDKPayCallback callback;
        public SDKPayCallback(IMiSDKPayCallback callback) : base("com.xiaomi.gamecenter.sdk.OnPayProcessListener") {
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
    /// 退弹回调
    /// </summary>
    public class SDKExitCallback : AndroidJavaProxy
    {
        private IMiSDKExitCallback callback;
        public SDKExitCallback(IMiSDKExitCallback callback) : base("com.xiaomi.gamecenter.sdk.OnExitListner") {
            this.callback = callback;
        }

        void onExit(int code)
        {
            if (callback != null) {
                callback.OnExit(code);
            }
        }

    }
}
