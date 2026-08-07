using System;
using System.Collections.Generic;
using ByteDance.Union.Mediation;

namespace ByteDance.Union
{
    public class PrivacyConfiguration
    {
        /// <summary>
        ///是否允许SDK主动使用ACCESS_WIFI_STATE权限
        /// </summary>
        /// <returns> true可以使用，false禁止使用。默认为true</returns>
        public bool CanUseWifiState { get; set; } = true;

        /// <summary>
        ///  是否允许SDK主动使用地理位置信息 true可以获取，false禁止获取。默认为true
        /// </summary>
        public bool CanUseLocation { get; set; } = true;

        /// <summary>
        /// 是否允许sdk上报手机app安装列表 true可以上报、false禁止上报。默认为true
        /// </summary>
        public bool CanReadAppList { get; set; } = true;

        /// <summary>
        /// 是否允许SDK主动使用手机硬件参数，如：imei true可以使用，false禁止使用。默认为true
        /// </summary>
        public bool CanUsePhoneState { get; set; } = true;

        /// <summary>
        /// 是否允许SDK主动使用ACCESS_WIFI_STATE权限  true可以使用，false禁止使用。默认为true
        /// </summary>
        public bool CanUseWriteExternal { get; set; } = true;

        /// <summary>
        /// 当isCanUseWifiState=false时，可传入Mac地址信息，穿山甲sdk使用您传入的Mac地址信息
        /// </summary>
        /// <returns>Mac地址信息</returns>
        public string MacAddress { get; set; }

        //纬度
        public double Latitude { get; set; } = double.NaN;

        //经度
        public double Longitude { get; set; } = double.NaN;
        
        public string DevImei { get; set; }
        public string DevOaid { get; set; }

        /**
         * 是否允许SDK主动获取ANDROID_ID
         *
         * @return true 允许  false 不允许，默认是true
         */
        public bool CanUseAndroidId { get; set; } = true;

        /**
         * 当CanUseAndroidId返回false时，可传入AndroidId，穿山甲sdk使用您传入的AndroidId
         */
        public string AndroidId { get; set; }

        /**
         * 开发者自定义传入的隐私相关设置，可设置key值可参考AdConst
         */
        public Dictionary<string, System.Object> UserPrivacyConfig { get; set; }

        /**
         * 使用融合功能时，传给聚合使用的隐私合规配置
         */
        public MediationPrivacyConfig MediationPrivacyConfig { get; set; }

        /**
         * iOS平台，设置自定义idfa
         */
        public string CustomIdfa { get; set; }

        /**
         * android平台，是否允许穿山甲弹出下载安装/打开通知
         */
        public bool IsCanUseMessage { get; set; }
    }
}