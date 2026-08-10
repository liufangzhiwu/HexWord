#if !UNITY_EDITOR && UNITY_ANDROID
using UnityEngine;

namespace ByteDance.Union
{
    public sealed class AndroidBundleWrapper
    {
        private AndroidJavaObject bundle;
        public AndroidBundleWrapper()
        {
            bundle = new AndroidJavaObject("android.os.Bundle");
        }

        public void putString(string key, string value)
        {
            bundle.Call("putString",key, value);
        }
        
        public void putBoolean(string key, bool value)
        {
            bundle.Call("putBoolean",key, value);
        }

        public void putInt(string key, int value)
        {
            bundle.Call("putInt",key, value);
        }
        public AndroidJavaObject getAndroidBundle()
        {
            return bundle;
        }
    }
}
#endif