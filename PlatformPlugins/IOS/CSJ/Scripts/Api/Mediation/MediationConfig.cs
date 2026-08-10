using System;
using System.Collections.Generic;

namespace ByteDance.Union.Mediation
{
    public class MediationConfig
    {
        public string PublisherDid { get; set; }
        public MediationConfigUserInfoForSegment MediationConfigUserInfoForSegment { get; set; }
        public Dictionary<string, Object> LocalExtra { get; set; }
        public bool UseHttps { get; set; }
        public string CustomLocalConfig { get; set; }
        public string OpensdkVer { get; set; }
        public bool WxInstalled { get; set; }
        public bool SupportH265 { get; set; }
        public bool SupportSplashZoomout { get; set; }
        public string WxAppId { get; set; }
    }
}