using UnityEngine;

namespace ByteDance.Union.Mediation
{
#if !UNITY_EDITOR && UNITY_ANDROID
    /// <summary>
    /// The slot of a Mediation.
    /// </summary>
    public sealed class MediationAdSlot
    {
        internal AndroidJavaObject jMediationAdSlot;
        
        public MediationAdSlot(AndroidJavaObject slot)
        {
            this.jMediationAdSlot = slot;
        }
        
        internal AndroidJavaObject Handle
        {
            get
            {
                return this.jMediationAdSlot;
            }
        }

        /// <summary>
        /// The builder used to build an Mediation Ad slot.
        /// </summary>
        public class Builder
        {
            private AndroidJavaObject jBuilder;

            public Builder()
            {
                this.jBuilder = new AndroidJavaObject(
                    "com.bytedance.sdk.openadsdk.mediation.ad.MediationAdSlot$Builder");
            }
            
            public Builder SetSplashShakeButton(bool splashShakeBtn)
            {
                this.jBuilder.Call<AndroidJavaObject>("setSplashShakeButton", splashShakeBtn);
                return this;
            }

            public Builder SetAllowShowCloseBtn(bool allowShowCloseBtn)
            {
                this.jBuilder.Call<AndroidJavaObject>("setAllowShowCloseBtn", allowShowCloseBtn);
                return this;
            }

            public Builder SetShakeViewSize(float w, float h)
            {
                this.jBuilder.Call<AndroidJavaObject>("setShakeViewSize", w, h);
                return this;
            }

            public Builder SetWxAppId(string wxAppId)
            {
                this.jBuilder.Call<AndroidJavaObject>("setWxAppId", wxAppId);
                return this;
            }

            public Builder SetMediationSplashRequestInfo(MediationSplashRequestInfo info)
            {
                var jMsplashReqInfo = new AndroidJavaObject(
                    "com.bytedance.android.MediationSplashReqInfo",
                    info.AdnName, info.AdnSlotId, info.AppId, info.Appkey);
                this.jBuilder.Call<AndroidJavaObject>("setMediationSplashRequestInfo", jMsplashReqInfo);
                return this;
            }

            public Builder SetSplashPreLoad(bool isPreload)
            {
                this.jBuilder.Call<AndroidJavaObject>("setSplashPreLoad", isPreload);
                return this;
            }
            
            /// <summary>
            /// Sets the muted.
            /// </summary>
            public Builder SetMuted(bool isMuted)
            {
                this.jBuilder.Call<AndroidJavaObject>("setMuted", isMuted);
                return this;
            }

            public Builder SetVolume(float volume)
            {
                this.jBuilder.Call<AndroidJavaObject>("setVolume", volume);
                return this;
            }

            public Builder SetUseSurfaceView(bool isUseSurfaceview)
            {
                this.jBuilder.Call<AndroidJavaObject>("setUseSurfaceView", isUseSurfaceview);
                return this;
            }

            public Builder SetBidNotify(bool bidNotify)
            {
                this.jBuilder.Call<AndroidJavaObject>("setBidNotify", bidNotify);
                return this;
            }

            public Builder SetScenarioId(string scenarioId)
            {
                this.jBuilder.Call<AndroidJavaObject>("setScenarioId", scenarioId);
                return this;
            }

            public Builder SetRewardName(string rewardName)
            {
                this.jBuilder.Call<AndroidJavaObject>("setRewardName", rewardName);
                return this;
            }

            public Builder SetRewardAmount(int amount)
            {
                this.jBuilder.Call<AndroidJavaObject>("setRewardAmount", amount);
                return this;
            }
            
            public Builder SetExtraObject(string key, string value)
            {
                if (key == AdConst.KEY_M_MEDIA_AD_ROTATE_VIEW_ENABLE || key == AdConst.KEY_M_SHOW_ADN_LOAD_ERROR_DETAIL)
                {
                    AndroidJavaObject javaBooleanTrue = new AndroidJavaObject("java.lang.Boolean", true);
                    AndroidJavaObject javaBooleanFalse = new AndroidJavaObject("java.lang.Boolean", false);
                    string androidKey = (key == AdConst.KEY_M_SHOW_ADN_LOAD_ERROR_DETAIL) ?
                        "show_adn_load_error_detail" : "ksEnableRotate";
                    Debug.Log("CSJM_Unity, SetExtraObject start: " + androidKey + ", value: " + value);
                    this.jBuilder.Call<AndroidJavaObject>("setExtraObject", androidKey,
                        value != "0" ? javaBooleanTrue : javaBooleanFalse);
                    Debug.Log("CSJM_Unity, SetExtraObject end: " + androidKey + ", value: " + value);
                }
                else if (key == AdConst.KEY_M_TWO_STAGE_INFO)
                {
                    if (value != null && value.Length > 0)
                    {
                        AndroidJavaObject jsonObj = Utils.GetJsonObjFromJsonStr(value.Replace("two_stage_", ""));
                        this.jBuilder.Call<AndroidJavaObject>("setExtraObject", "baidu_reward_info", jsonObj);
                    }
                }
                else
                {
                    this.jBuilder.Call<AndroidJavaObject>("setExtraObject", key, value);
                }
                return this;
            }

            /// <summary>
            /// Build the Ad slot.
            /// </summary>
            public MediationAdSlot Build()
            {
                var jMediationNativeToBannerClass = new AndroidJavaClass("com.bytedance.android.MediationNativeToBannerUtils");
                var jNativeToBannerListener = jMediationNativeToBannerClass.CallStatic<AndroidJavaObject>(
                    "getMediationNativeToBannerListener", Utils.GetActivity());
                this.jBuilder.Call<AndroidJavaObject>("setMediationNativeToBannerListener", jNativeToBannerListener);
                
                var jMediationAdSlot = this.jBuilder.Call<AndroidJavaObject>("build");
                return new MediationAdSlot(jMediationAdSlot);
            }
        }
    }
#endif
}