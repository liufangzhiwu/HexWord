#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)

namespace ByteDance.Union
{

    public class Pangle
    {

        public delegate void PangleInitializeCallBack(bool success, string message);

        public static void Init(SDKConfiguration config) { }

        public static void Start(PangleInitializeCallBack callback) { }

        public static bool IsSdkReady()
        {
            return false;
        }
    }

}

#endif
