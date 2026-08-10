using System.Collections.Generic;
using ByteDance.Union.Mediation;
using UnityEngine;

namespace ByteDance.Union
{
#if !UNITY_EDITOR && UNITY_ANDROID
    public class Utils
    {
        public static AndroidJavaObject MakeCustomController(PrivacyConfiguration controller)
        {
            var customController = new AndroidJavaObject("com.bytedance.android.CustomController");
            if (controller == null) return customController;
            customController.Call("canUseWifiState", controller.CanUseWifiState);
            customController.Call("setMacAddress", controller.MacAddress);
            customController.Call("canUseLocation", controller.CanUseLocation);
            if (!double.IsNaN(controller.Latitude) || !double.IsNaN(controller.Longitude))
            {
                customController.Call("setLocationInfo", controller.Longitude, controller.Latitude);
            }
            customController.Call("canReadAppList", controller.CanReadAppList);
            customController.Call("canUsePhoneState", controller.CanUsePhoneState);
            customController.Call("setDevImei", controller.DevImei);
            customController.Call("canUseWriteExternal", controller.CanUseWriteExternal);
            customController.Call("setDevOaid", controller.DevOaid);
            customController.Call("canUseAndroidId", controller.CanUseAndroidId);
            customController.Call("setAndroidId", controller.AndroidId);
            customController.Call("setCanUseMessage", controller.IsCanUseMessage);
            if (controller.MediationPrivacyConfig != null)
            {
                customController.Call("setMediationPrivacyConfig", 
                    MakeMediationPrivacyConfig(controller.MediationPrivacyConfig));
            }

            if (controller.UserPrivacyConfig != null)
            {
                foreach (var kv in controller.UserPrivacyConfig)
                {
                    Debug.Log("CSJM_Unity, setUserPrivacyConfig: " + kv.Key + ": " + kv.Value);
                }
                customController.Call("setUserPrivacyConfig", GetMapFromDictionary(controller.UserPrivacyConfig));
            }

            return customController;
        }
        
        public static AndroidJavaObject MakeMediationConfig(MediationConfig mediationConfig)
        {
            var jBuilder = new AndroidJavaObject("com.bytedance.sdk.openadsdk.mediation.init.MediationConfig$Builder");
            jBuilder.Call<AndroidJavaObject>("setPublisherDid", mediationConfig.PublisherDid);
            if (mediationConfig.MediationConfigUserInfoForSegment != null)
            {
                jBuilder.Call<AndroidJavaObject>("setMediationConfigUserInfoForSegment",
                    MakeMediationConfigUserInfoForSegment(mediationConfig.MediationConfigUserInfoForSegment));
            }
            if (mediationConfig.LocalExtra != null)
            {
                jBuilder.Call<AndroidJavaObject>("setLocalExtra",
                    GetMapFromDictionary(mediationConfig.LocalExtra));
            }
            jBuilder.Call<AndroidJavaObject>("setHttps", mediationConfig.UseHttps);
            if (mediationConfig.CustomLocalConfig != null && mediationConfig.CustomLocalConfig.Length > 0)
            {
                jBuilder.Call<AndroidJavaObject>("setCustomLocalConfig", 
                    GetJsonObjFromJsonStr(mediationConfig.CustomLocalConfig));
            }
            jBuilder.Call<AndroidJavaObject>("setWxInstalled", mediationConfig.WxInstalled);
            jBuilder.Call<AndroidJavaObject>("setOpensdkVer", mediationConfig.OpensdkVer);
            jBuilder.Call<AndroidJavaObject>("setSupportH265", mediationConfig.SupportH265);
            jBuilder.Call<AndroidJavaObject>("setSupportSplashZoomout", mediationConfig.SupportSplashZoomout);
            jBuilder.Call<AndroidJavaObject>("setWxAppId", mediationConfig.WxAppId);
            return jBuilder.Call<AndroidJavaObject>("build");
        }

        public static AndroidJavaObject MakeMediationConfigUserInfoForSegment(MediationConfigUserInfoForSegment segment)
        {
            var jSegment = new AndroidJavaObject("com.bytedance.sdk.openadsdk.mediation.init.MediationConfigUserInfoForSegment");
            if (segment.CustomInfos != null)
            {
                jSegment.Call("setCustomInfos", GetMapFromDictionary(segment.CustomInfos));
            }
            jSegment.Call("setUserId", segment.UserId);
            jSegment.Call("setChannel", segment.Channel);
            jSegment.Call("setSubChannel", segment.SubChannel);
            jSegment.Call("setAge", segment.Age);
            jSegment.Call("setGender", segment.Gender);
            jSegment.Call("setUserValueGroup", segment.UserValueGroup);
            return jSegment;
        }

        public static AndroidJavaObject MakeMediationPrivacyConfig(MediationPrivacyConfig config)
        {
            var jConfig = new AndroidJavaObject("com.bytedance.android.MediationPrivacyConfig");
            jConfig.Call("setCustomAppList", GetJavaListFromList(config.CustomAppList));
            jConfig.Call("setCustomDevImeis", GetJavaListFromList(config.CustomDevImeis));
            jConfig.Call("setCanUseOaid", config.CanUseOaid);
            jConfig.Call("setLimitPersonalAds", config.LimitPersonalAds);
            jConfig.Call("setProgrammaticRecommend", config.ProgrammaticRecommend);
            
            return jConfig;
        }

        public static AndroidJavaObject MakeMediationPreloadReqInfos(List<MediationPreloadRequestInfo> reqInfos)
        {
            if (reqInfos == null || reqInfos.Count <= 0)
            {
                return null;
            }
            
            var jList = new AndroidJavaObject("java.util.ArrayList");
            foreach (var info in reqInfos)
            {
                var jReqInfo = new AndroidJavaObject(
                    "com.bytedance.sdk.openadsdk.mediation.MediationPreloadRequestInfo",
                    info.AdType, info.AdSlot.Handle, GetJavaListFromList(info.PrimeRitList));

                jList.Call<bool>("add", jReqInfo);
            }
            return jList;
        }

        public static AndroidJavaObject GetMapFromDictionary(Dictionary<string, System.Object> dict)
        {
            if (dict == null)
            {
                return null;
            }

            var jMap = new AndroidJavaObject("java.util.HashMap");
            foreach(KeyValuePair<string, System.Object> kvp in dict)
            {
                if (kvp.Value is bool)
                {
                    jMap.Call<AndroidJavaObject>("put", kvp.Key, new AndroidJavaObject("java.lang.Boolean", kvp.Value));
                }
                else if (kvp.Value is int || kvp.Value is long)
                {
                    jMap.Call<AndroidJavaObject>("put", kvp.Key, new AndroidJavaObject("java.lang.Integer", kvp.Value));
                }
                else if (kvp.Value is float || kvp.Value is double)
                {
                    jMap.Call<AndroidJavaObject>("put", kvp.Key, new AndroidJavaObject("java.lang.Float", kvp.Value));
                }
                else
                {
                    jMap.Call<AndroidJavaObject>("put", kvp.Key, kvp.Value);
                }
            }

            return jMap;
        }
        public static AndroidJavaObject GetMapFromDictionary(Dictionary<string, string> dict)
        {
            if (dict == null)
            {
                return null;
            }

            var jMap = new AndroidJavaObject("java.util.HashMap");
            foreach(KeyValuePair<string, string> kvp in dict)
            {
                jMap.Call<AndroidJavaObject>("put", kvp.Key, kvp.Value);
            }

            return jMap;
        }

        public static AndroidJavaObject GetJsonObjFromJsonStr(string jsonStr)
        {
            if (jsonStr == null || jsonStr.Length <= 0)
            {
                return null;
            }

            Debug.Log("CSJM_Unity, start json, localConfigJsonStr: " + jsonStr);
            var javaJsonObj = new AndroidJavaObject("org.json.JSONObject", jsonStr);
            Debug.Log("CSJM_Unity, after json, localConfigJsonStr: " + javaJsonObj.Call<string>("toString"));
            return javaJsonObj;
        }

        public static AndroidJavaObject GetJavaListFromList(List<string> list)
        {
            if (list == null)
            {
                return null;
            }
            var jList = new AndroidJavaObject("java.util.ArrayList");
            foreach (string str in list)
            {
                jList.Call<bool>("add", str);
            }

            return jList;
        }

        public static AndroidJavaObject GetMediationManager()
        {
            var jc = new AndroidJavaClass("com.bytedance.sdk.openadsdk.TTAdSdk");
            var jMediationManager = jc.CallStatic<AndroidJavaObject>("getMediationManager");

            return jMediationManager;
        }
        
        public static AndroidJavaObject GetActivity()
        {
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }
        
        public static List<MediationAdLoadInfo> GetAdLoadInfo(AndroidJavaObject handle)
        {
            if (handle != null)
            {
                AndroidJavaObject jList = handle.Call<AndroidJavaObject>("getAdLoadInfo");
                if (jList != null)
                {
                    List<MediationAdLoadInfo> list = new List<MediationAdLoadInfo>();
                    int len = jList.Call<int>("size");
                    for (int i = 0; i < len; i++)
                    {
                        AndroidJavaObject jLoadInfo = jList.Call<AndroidJavaObject>("get", i);
                        if (jLoadInfo != null)
                        {
                            MediationAdLoadInfo info = new MediationAdLoadInfo();
                            info.adnName = jLoadInfo.Call<string>("getAdnName");
                            info.mediationRit = jLoadInfo.Call<string>("getMediationRit");
                            info.adType = jLoadInfo.Call<string>("getAdType");
                            info.errCode = jLoadInfo.Call<int>("getErrCode");
                            info.errMsg = jLoadInfo.Call<string>("getErrMsg");
                            list.Add(info);
                        }
                    }
                    return list;
                }
            }
            return null;
        }

        public static List<MediationAdEcpmInfo> GetMultiBiddingEcpm(AndroidJavaObject handle)
        {
            return GetMediationAdEcpmInfos(handle, "getMultiBiddingEcpm");
        }

        public static List<MediationAdEcpmInfo> GetCacheList(AndroidJavaObject handle)
        {
            return GetMediationAdEcpmInfos(handle, "getCacheList");
        }

        public static MediationAdEcpmInfo GetBestEcpm(AndroidJavaObject handle)
        {
            if (handle != null)
            {
                AndroidJavaObject jEcpmInfo = handle.Call<AndroidJavaObject>("getBestEcpm");
                return GetMediationAdEcpmInfo(jEcpmInfo);
            }

            return null;
        }

        public static MediationAdEcpmInfo GetShowEcpm(AndroidJavaObject handle)
        {
            if (handle != null)
            {
                AndroidJavaObject jEcpmInfo = handle.Call<AndroidJavaObject>("getShowEcpm");
                return GetMediationAdEcpmInfo(jEcpmInfo);
            }

            return null;
        }
        
        private static List<MediationAdEcpmInfo> GetMediationAdEcpmInfos(AndroidJavaObject handle, string javaMethodName)
        {
            if (handle != null)
            {
                AndroidJavaObject jList = handle.Call<AndroidJavaObject>(javaMethodName);
                if (jList != null)
                {
                    List<MediationAdEcpmInfo> list = new List<MediationAdEcpmInfo>();
                    int len = jList.Call<int>("size");
                    for (int i = 0; i < len; i++)
                    {
                        AndroidJavaObject jEcpmInfo = jList.Call<AndroidJavaObject>("get", i);
                        if (jEcpmInfo != null)
                        {
                            MediationAdEcpmInfo info = GetMediationAdEcpmInfo(jEcpmInfo);
                            if (info != null)
                            {
                                list.Add(info);
                            }
                        }
                    }

                    return list;
                }
            }
            return null;
        }

        private static MediationAdEcpmInfo GetMediationAdEcpmInfo(AndroidJavaObject jEcpmInfo)
        {
            if (jEcpmInfo != null)
            {
                MediationAdEcpmInfo info = new MediationAdEcpmInfo();
                info.sdkName = jEcpmInfo.Call<string>("getSdkName");
                info.customSdkName = jEcpmInfo.Call<string>("getCustomSdkName");
                info.slotId = jEcpmInfo.Call<string>("getSlotId");
                info.levelTag = jEcpmInfo.Call<string>("getLevelTag");
                info.ecpm = jEcpmInfo.Call<string>("getEcpm");
                info.reqBiddingType = jEcpmInfo.Call<int>("getReqBiddingType");
                info.errorMsg = jEcpmInfo.Call<string>("getErrorMsg");
                info.requestId = jEcpmInfo.Call<string>("getRequestId");
                info.ritType = jEcpmInfo.Call<string>("getRitType");
                info.segmentId = jEcpmInfo.Call<string>("getSegmentId");
                info.channel = jEcpmInfo.Call<string>("getChannel");
                info.subChannel = jEcpmInfo.Call<string>("getSubChannel");
                info.abTestId = jEcpmInfo.Call<string>("getAbTestId");
                info.scenarioId = jEcpmInfo.Call<string>("getScenarioId");
                
                AndroidJavaObject jCustomDataMap = jEcpmInfo.Call<AndroidJavaObject>("getCustomData");
                if (jCustomDataMap != null)
                {
                    Dictionary<string, string> customData = new Dictionary<string, string>();

                    AndroidJavaObject jSet = jCustomDataMap.Call<AndroidJavaObject>("entrySet");
                    AndroidJavaObject jIterator = jSet.Call<AndroidJavaObject>("iterator");
                    while (jIterator.Call<bool>("hasNext"))
                    {
                        AndroidJavaObject jEntry = jIterator.Call<AndroidJavaObject>("next");
                        customData.Add(jEntry.Call<string>("getKey"), jEntry.Call<string>("getValue"));
                    }

                    info.customData = customData;
                }

                return info;
            }

            return null;
        }
    }
#endif
}