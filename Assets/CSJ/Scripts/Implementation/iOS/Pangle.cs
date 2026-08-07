#if !UNITY_EDITOR && UNITY_IOS
namespace ByteDance.Union
{
    using System.Runtime.InteropServices;
    public class Pangle 
    {
        public delegate void PangleInitializeCallBack(bool success, string message);
        private static PangleInitializeCallBack callbackio;
        private static SDKConfiguration configurationio;

        public static void Init(SDKConfiguration configuration) {
            PangleConfiguration c = PangleConfiguration.CreateInstance();
            // 设置appid
            c.appID = configuration.appId;
            // 设置一些初始化设置
            configurationio = configuration;
            if (configuration != null)
            {
                c.useMediation = configuration.useMediation;
                c.debugLog = configuration.debug;
                c.themeStatus = configuration.themeStatus;
                c.ageGroup = configuration.ageGroup;

                if (configuration.privacyConfiguration != null)
                {
                    c.privacyConfig = configuration.privacyConfiguration;
                }
                if (configuration.mediationConfig != null)
                {
                    if (configuration.mediationConfig.PublisherDid != null)
                    {
                        c.publisherDid = configuration.mediationConfig.PublisherDid;
                    }
                    if (configuration.mediationConfig.MediationConfigUserInfoForSegment != null)
                    {
                        c.mediationConfigUserInfoForSegment = configuration.mediationConfig.MediationConfigUserInfoForSegment;
                    }
                    c.supportSplashZoomout = configuration.mediationConfig.SupportSplashZoomout;
                }
            }
        }

        public static void Start(PangleInitializeCallBack callback) {
            // 触发原生初始化，并传递回调
            callbackio = callback;
            UnionPlatform_PangleInitializeSDK(PangleInitializeCallBackCC);
        }

        public static bool IsSdkReady()
        {
            return UnionPlatform_PanglIsSdkReady();
        }

        [DllImport("__Internal")]
        private static extern float UnionPlatform_PangleInitializeSDK(PangleInitializeCallBack callback);

        [DllImport("__Internal")]
        private static extern bool UnionPlatform_PanglIsSdkReady();

        [AOT.MonoPInvokeCallback(typeof(PangleInitializeCallBack))]
        private static void PangleInitializeCallBackCC(bool success, string message)
        {
            callbackio(success, message);
        }


    }
}
#endif