using UnityEngine;

public class XIaomiSDKManager : MonoBehaviour
{
    public static XIaomiSDKManager _instance;
    private AndroidJavaClass _bridgeClass;

    [Header("xiaomi开放平台配置")]
    public string xiaomiAppId = "2882303761520479149";
    public string xiaomiAppKey = "5592047971149";   // 5592047971149
    public string xiaomiCpId = "135f53f1caf68e8962e8";           // 135f53f1caf68e8962e8

    void Awake()
    {
        _instance = this;
        if (gameObject.name != "XIaomiSDKManager") gameObject.name = "XIaomiSDKManager";
    }

    void Start()
    {
#if UNITY_ANDROID&&!UNITY_EDITOR
        SDKAndroid.Instance.Init();
#endif
    }
}