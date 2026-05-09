using UnityEngine;

public class VivoSDKManager : MonoBehaviour
{
    public static VivoSDKManager _instance;
    private AndroidJavaClass _bridgeClass;

    [Header("vivo开放平台配置")]
    public string vivoAppId = "106007769";
    public string vivoAppKey = "你的支付AppKey";   // 43f**************************64d
    public string vivoCpId = "你的CpId";           // 135f53f1caf68e8962e8

    void Awake()
    {
        _instance = this;
        if (gameObject.name != "VivoSDKManager") gameObject.name = "VivoSDKManager";
    }

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // 注意：包名必须与你的 Java 类所在包名完全一致
        _bridgeClass = new AndroidJavaClass("com.liufangzhiwu.chengyuxiao.vivobridge.VivoBridge");
        _bridgeClass.CallStatic("init", vivoAppId, vivoAppKey, vivoCpId);
        Debug.Log("VivoBridge initialized");
#endif
    }

    // --- 对外接口 ---
    public static void OnPrivacyAgreed() => _instance._bridgeClass?.CallStatic("onPrivacyAgreed");
    public static void Login() => _instance._bridgeClass?.CallStatic("login");
    public static void ReportRoleInfo(string roleId, string roleLevel, string roleName, string zoneId, string zoneName)
        => _instance._bridgeClass?.CallStatic("reportRoleInfo", roleId, roleLevel, roleName, zoneId, zoneName);
    public static void PayForTesting(string orderId, string amount, string productName, string productDesc)
        => _instance._bridgeClass?.CallStatic("payForTesting", orderId, amount, productName, productDesc);
    public static void PayV2(string cpOrderNumber, string amount, string productName, string productDesc, string notifyUrl, string vivoSignature)
        => _instance._bridgeClass?.CallStatic("payV2", cpOrderNumber, amount, productName, productDesc, notifyUrl, vivoSignature);
    public static void ReportOrderComplete(string transNo) => _instance._bridgeClass?.CallStatic("reportOrderComplete", transNo);
    public static void ExitGame() => _instance._bridgeClass?.CallStatic("exit");
    public static string GetUid() => _instance._bridgeClass?.CallStatic<string>("getUid");

    // --- 回调接收 ---
    public void OnPrivacyAgreedResult(string msg) => Debug.Log($"[Vivo] Privacy: {msg}");
    public void OnLoginResult(string data) => Debug.Log($"[Vivo] Login: {data}");
    public void OnPayResult(string data) => Debug.Log($"[Vivo] Pay: {data}");
    public void OnExitResult(string data) { if (data == "exit") Application.Quit(); }
}