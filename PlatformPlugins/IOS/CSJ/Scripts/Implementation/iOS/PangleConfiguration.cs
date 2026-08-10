namespace ByteDance.Union
{
#if !UNITY_EDITOR && UNITY_IOS
    using System.Runtime.InteropServices;
    using ByteDance.Union.Mediation;
    using Newtonsoft.Json;

    public class PangleConfiguration
    {
        public enum BUAdSDKTerritory
        {
            BUAdSDKTerritory_CN = 1,
            BUAdSDKTerritory_NO_CN,
        }
        public enum BUAdSDKLogLevel
        {
            BUAdSDKLogLevelNone,
            BUAdSDKLogLevelError,
            BUAdSDKLogLevelWarning,
            BUAdSDKLogLevelInfo,
            BUAdSDKLogLevelDebug,
            BUAdSDKLogLevelVerbose,
        };
        public enum BUOfflineType
        {
            BUOfflineTypeNone,  // Do not set offline
            BUOfflineTypeWebview, // Offline dependence WKWebview
        };
        /// This property should be set when integrating non-China and china areas at the same time,
        /// otherwise it need'nt to be set.you‘d better set Territory first,  if you need to set them
        public BUAdSDKTerritory territory
        {
            get { return UnionPlatform_territory(); }
            set { UnionPlatform_setTerritory(value); }
        }
        ///Register the App key that’s already been applied before requesting an ad from TikTok Audience Network.
        /// the unique identifier of the App
        public string appID
        {
            get { return UnionPlatform_appID(); }
            set { UnionPlatform_setAppID(value); }
        }
        public bool useMediation
        {
            get { return UnionPlatform_useMediation(); }
            set { UnionPlatform_setUseMediation(value); }
        }

        public bool supportSplashZoomout
        {
            get { return UnionPlatform_SupportSplashZoomout(); }
            set { UnionPlatform_setSupportSplashZoomout(value); }
        }

        public string publisherDid
        {
            get { return null; }
            set { UnionPlatform_setPublisherDid(value); }
        }

        public bool limitProgrammaticAds
        {
            get { return UnionPlatform_limitProgrammaticAds(); }
            set { UnionPlatform_setLimitProgrammaticAds(value); }
        }

        public PrivacyConfiguration privacyConfig
        {
            get { return null; }

            set {
                if (value != null)
                {
                    object motion_info = null;
                    object bum_advertiser_tracking_enabled = null;
                    object bum_loc_time = null;
                    object bum_limit_personal_cpus = null;
                    object bum_disable_use_phone_status = null;
                    object bum_custom_idfv = null;
                    object bum_forbidden_idfv = null;
                    object forbidden_idfa = null;
                    if (value.UserPrivacyConfig != null)
                    {
                        value.UserPrivacyConfig.TryGetValue("motion_info", out motion_info);
                        value.UserPrivacyConfig.TryGetValue(AdConst.bum_advertiser_tracking_enabled, out bum_advertiser_tracking_enabled);
                        value.UserPrivacyConfig.TryGetValue(AdConst.bum_loc_time, out bum_loc_time);
                        value.UserPrivacyConfig.TryGetValue(AdConst.bum_limit_personal_cpus, out bum_limit_personal_cpus);
                        value.UserPrivacyConfig.TryGetValue(AdConst.bum_disable_use_phone_status, out bum_disable_use_phone_status);
                        value.UserPrivacyConfig.TryGetValue(AdConst.bum_custom_idfv, out bum_custom_idfv);
                        value.UserPrivacyConfig.TryGetValue(AdConst.bum_forbidden_idfv, out bum_forbidden_idfv);
                        value.UserPrivacyConfig.TryGetValue("forbidden_idfa", out forbidden_idfa);
                    }
                    UnionPlatform_setPrivacyProvider(
                        value.CanUseLocation,
                        value.Latitude,
                        value.Longitude,
                        motion_info as string,
                        bum_advertiser_tracking_enabled as string,
                        bum_loc_time as string,
                        bum_limit_personal_cpus as string,
                        bum_disable_use_phone_status as string,
                        bum_custom_idfv as string,
                        bum_forbidden_idfv as string,
                        forbidden_idfa as string);

                    PangleConfiguration c = PangleConfiguration.CreateInstance();
                    if (value.MediationPrivacyConfig != null)
                    {
                        c.limitProgrammaticAds = value.MediationPrivacyConfig.ProgrammaticRecommend ? false : true;
                        c.limitPersonalAds = value.MediationPrivacyConfig.LimitPersonalAds;
                    }

                    if (value.CustomIdfa != null)
                    {
                        c.customIdfa = value.CustomIdfa;
                    }  
                }
            }

        }

        public MediationConfigUserInfoForSegment mediationConfigUserInfoForSegment
        {
            set
            {
                string CustomInfosJson = null;
                if (value != null && value.CustomInfos != null)
                {
                    CustomInfosJson = JsonConvert.SerializeObject(value.CustomInfos);
                };
                UnionPlatform_setUserInfoForSegment(value.UserId,
                    value.UserValueGroup,
                    value.Age,
                    value.Gender,
                    value.Channel,
                    value.SubChannel,
                    CustomInfosJson
                );

            }
            get { return null; }
        }

        public bool limitPersonalAds
        {
            get { return UnionPlatform_limitPersonalAds(); }
            set { UnionPlatform_setLimitPersonalAds(value); }
        }

        public bool debugLog
        {
            get { return UnionPlatform_debugLog(); }
            set { UnionPlatform_setdebugLogebugLog(value); }
        }

        /// Configure development mode. default BUAdSDKLogLevelNone
        public BUAdSDKLogLevel logLevel
        {
            get { return UnionPlatform_logLevel(); }
            set { UnionPlatform_setLogLevel(value); }
        }
        /// the COPPA of the user, COPPA is the short of Children's Online Privacy Protection Rule,
        /// the interface only works in the United States.
        /// Coppa 0 adult, 1 child
        /// You can change its value at any time
        public int coppa
        {
            get { return UnionPlatform_coppa(); }
            set { UnionPlatform_setCoppa(value); }
        }
        /// additional user information.
        public string userExtData
        {
            get { return UnionPlatform_userExtData(); }
            set { UnionPlatform_setUserExtData(value); }
        }
        /// Solve the problem when your WKWebview post message empty,
        /// default is BUOfflineTypeWebview
        public BUOfflineType webViewOfflineType
        {
            get { return UnionPlatform_webViewOfflineType(); }
            set { UnionPlatform_setWebViewOfflineType(value); }
        }
        /// Custom set the GDPR of the user,GDPR is the short of General Data Protection Regulation,the interface only works in The European.
        /// GDPR 0 close privacy protection, 1 open privacy protection
        /// You can change its value at any time
        public int GDPR
        {
            get { return UnionPlatform_GDPR(); }
            set { UnionPlatform_setGDPR(value); }
        }
        /// Custom set the CCPA of the user,CCPA is the short of General Data Protection Regulation,the interface only works in USA.
        /// CCPA  0: "sale" of personal information is permitted, 1: user has opted out of "sale" of personal information -1: default
        public int CCPA
        {
            get { return UnionPlatform_CCPA(); }
            set { UnionPlatform_setCCPA(value); }
        }

        ///主题类型，0：正常模式；1：夜间模式；默认为0；传非法值，按照0处理
        public int themeStatus
        {
            get { return UnionPlatform_themeStatus(); }
            set { UnionPlatform_setThemeStatus(value); }
        }

        ///the age group of the user,  0 : Default; 1 : Age 15~18 ; 2 : Age < 15 .
        public int ageGroup
        {
            get { return UnionPlatform_ageGroup(); }
            set { UnionPlatform_setAgeGroup(value); }
        }

        /// Custom set the tob ab sdk version of the user.
        public string abSDKVersion
        {
            get { return UnionPlatform_abSDKVersion(); }
            set { UnionPlatform_setAbSDKVersion(value); }
        }
        /// Custom set idfa value
        /// You can change its value at any time
        public string customIdfa
        {
            get { return UnionPlatform_customIdfa(); }
            set { UnionPlatform_setCustomIdfa(value); }
        }
        /**
        Whether to allow SDK to modify the category and options of AVAudioSession when playing audio, default is NO.
        The category set by the SDK is AVAudioSessionCategoryAmbient, and the options are AVAudioSessionCategoryOptionDuckOthers
        */
        public bool allowModifyAudioSessionSetting
        {
            get { return UnionPlatform_allowModifyAudioSessionSetting(); }
            set { UnionPlatform_setAllowModifyAudioSessionSetting(value); }
        }
        /// Custom set the AB vid of the user. Array element type is NSNumber
        private static int[] abvids;
        public void setAbvids(int[] abv, int length)
        {
            abvids = abv;
            UnionPlatform_setAbvids(abvids, length);
        }
        public int[] getAbvids()
        {
            return abvids;
        }

        /// <summary>
        /// set true if you are unity developer
        /// </summary>
        public bool unityDeveloper
        {
            get { return UnionPlatform_UnityDeveloper(); }
            set { UnionPlatform_SetUnityDeveloper(value); }
        }
        private static PangleConfiguration _Singleton = null;
        private static object Singleton_Lock = new object();
        public static PangleConfiguration CreateInstance()
        {
            if (_Singleton == null) //双if +lock
            {
                lock (Singleton_Lock)
                {
                    if (_Singleton == null)
                    {
                        _Singleton = new PangleConfiguration();
                    }
                }
            }
            return _Singleton;
        }

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setTerritory(BUAdSDKTerritory territory);
        [DllImport("__Internal")]
        private static extern BUAdSDKTerritory UnionPlatform_territory();

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setAppID(string appID);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_appID();

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setUseMediation(bool useMediation);
        [DllImport("__Internal")]
        private static extern bool UnionPlatform_useMediation();

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setLogLevel(BUAdSDKLogLevel logLevel);
        [DllImport("__Internal")]
        private static extern BUAdSDKLogLevel UnionPlatform_logLevel();

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setCoppa(int coppa);
        [DllImport("__Internal")]
        private static extern int UnionPlatform_coppa();

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setUserExtData(string userExtData);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_userExtData();

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setWebViewOfflineType(BUOfflineType webViewOfflineType);
        [DllImport("__Internal")]
        private static extern BUOfflineType UnionPlatform_webViewOfflineType();

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setGDPR(int GDPR);
        [DllImport("__Internal")]
        private static extern int UnionPlatform_GDPR();

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setCCPA(int CCPA);
        [DllImport("__Internal")]
        private static extern int UnionPlatform_CCPA();

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setThemeStatus(int themeStatus);
        [DllImport("__Internal")]
        private static extern int UnionPlatform_themeStatus();

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setAgeGroup(int ageGroup);
        [DllImport("__Internal")]
        private static extern int UnionPlatform_ageGroup();

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setAbvids(int[] abvids, int length);

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setAbSDKVersion(string abSDKVersion);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_abSDKVersion();

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setCustomIdfa(string customIdfa);
        [DllImport("__Internal")]
        private static extern string UnionPlatform_customIdfa();

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setAllowModifyAudioSessionSetting(bool allowModifyAudioSessionSetting);
        [DllImport("__Internal")]
        private static extern bool UnionPlatform_allowModifyAudioSessionSetting();

        [DllImport("__Internal")]
        private static extern void UnionPlatform_SetUnityDeveloper(bool unityDeveloper);
        [DllImport("__Internal")]
        private static extern bool UnionPlatform_UnityDeveloper();


        [DllImport("__Internal")]
        private static extern void UnionPlatform_setLimitPersonalAds(bool limitPersonalAds);
        [DllImport("__Internal")]
        private static extern bool UnionPlatform_limitPersonalAds();

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setLimitProgrammaticAds(bool limitProgrammaticAds);
        [DllImport("__Internal")]
        private static extern bool UnionPlatform_limitProgrammaticAds();

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setPublisherDid(string publisherDid);

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setUserInfoForSegment(string user_id, string user_value_group, int age, string gender, string channel, string sub_channel, string customized_id);

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setPrivacyProvider(
            bool canUseLocation,
            double latitude,
            double longitude,
            string motion_info,
            string bum_advertiser_tracking_enabled,
            string bum_loc_time,
            string bum_limit_personal_cpus,
            string bum_disable_use_phone_status,
            string bum_custom_idfv,
            string bum_forbidden_idfv,
            string forbidden_idfa
            );

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setdebugLogebugLog(bool debugLog);
        [DllImport("__Internal")]
        private static extern bool UnionPlatform_debugLog();

        [DllImport("__Internal")]
        private static extern void UnionPlatform_setSupportSplashZoomout(bool supportSplashZoomout);
        [DllImport("__Internal")]
        private static extern bool UnionPlatform_SupportSplashZoomout();

    }
#endif
}