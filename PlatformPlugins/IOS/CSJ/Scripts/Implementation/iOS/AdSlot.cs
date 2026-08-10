//------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
//------------------------------------------------------------------------------

using System.Runtime.InteropServices;
using ByteDance.Union.Mediation;

namespace ByteDance.Union
{
#if !UNITY_EDITOR && UNITY_IOS
    /// <summary>
    /// The slot of a advertisement for iOS.
    /// </summary>
    public sealed class AdSlot
    {
        /// <summary>
        /// Gets or sets the code ID.
        /// </summary>
        internal string CodeId { get; set; }

        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        internal string UserId { get; set; }

        internal int adCount;
        internal int width;
        internal int height;
        internal AdSlotType type;
        internal int viewwidth;
        internal int viewheight;
        internal string mediaExtra;
        internal int intervalTime;
        internal AdLoadType adLoadType;
        internal MediationAdSlot mAdSlot;
        internal int rewardAmount;
        internal string rewardName;
        internal int supportIconStyle;
        internal AdOrientation orientation;

        /// <summary>
        /// The builder used to build an Ad slot.
        /// </summary>
        public class Builder
        {
            private AdSlot slot = new AdSlot();

            /// <summary>
            /// Sets the code ID.
            /// </summary>
            public Builder SetCodeId(string codeId)
            {
                this.slot.CodeId = codeId;
                return this;
            }

            /// <summary>
            /// Sets the image accepted size.
            /// </summary>
            public Builder SetImageAcceptedSize(int width, int height)
            {
                this.slot.width = width;
                this.slot.height = height;
                return this;
            }

            /// <summary>
            /// Sets the size of the express view accepted in dp
            /// </summary>
            public Builder SetExpressViewAcceptedSize(float width, float height)
            {
                this.slot.viewwidth = (int)width;
                this.slot.viewheight = (int)height;
                return this;
            }

            /// <summary>
            /// Sets a value indicating wheteher the Ad support deep link.
            /// </summary>
            public Builder SetSupportDeepLink(bool support)
            {
                return this;
            }

            /// <summary>
            /// Sets a value indicating whether this Ad is express Ad.
            /// </summary>

            public Builder IsExpressAd(bool isExpressAd)
            {
                return this;
            }

            /// <summary>
            /// Sets the Ad count.
            /// </summary>
            public Builder SetAdCount(int count)
            {
                this.slot.adCount = count;
                return this;
            }

            /// <summary>
            /// Sets the Native Ad type.
            /// </summary>
            public Builder SetNativeAdType(AdSlotType type)
            {
                this.slot.type = type;
                return this;
            }

            /// <summary>
            /// Sets the user ID.
            /// </summary>
            public Builder SetUserID(string id)
            {
                this.slot.UserId = id;
                return this;
            }

            /// <summary>
            /// Sets the Ad orientation.
            /// </summary>
            public Builder SetOrientation(AdOrientation orientation)
            {
                this.slot.orientation = orientation;
                return this;
            }

            /// <summary>
            /// Sets the extra media for Ad.
            /// </summary>
            public Builder SetMediaExtra(string extra)
            {
                this.slot.mediaExtra = extra;
                return this;
            }
            /// <summary>
            /// Sets the express banner intervalTime.
            /// </summary>
            public Builder SetSlideIntervalTime(int intervalTime)
            {
                this.slot.intervalTime = intervalTime;
                return this;
            }
            /// <summary>
            /// Sets the reward name.
            /// </summary>
            public Builder SetRewardName(string name)
            {
                this.slot.rewardName = name;
                return this;
            }
            /// <summary>
            /// Sets the reward amount.
            /// </summary>
            public Builder SetRewardAmount(int amount)
            {
                this.slot.rewardAmount = amount;
                return this;
            }
            /// <summary>
            /// set ad load type
            /// </summary>
            /// <param name="type">AdLoadType</param>
            /// <returns>Builder</returns>
            public Builder SetAdLoadType(AdLoadType type)
            {
                this.slot.adLoadType = type;
                return this;
            }
            /// <summary>
            /// 开启信息流icon样式广告
            /// </summary>
            /// <returns></returns>
            public Builder SupportIconStyle()
            {
                this.slot.supportIconStyle = 1;
                return this;
            }            
            /// <summary>
            /// 
            /// Sets the Mediation Ad slot.
            /// </summary>
            public Builder SetMediationAdSlot(MediationAdSlot mAdSlot)
            {
                this.slot.mAdSlot = mAdSlot;
                return this;
            }
            /// <summary>
            /// Build the Ad slot.
            /// </summary>
            public AdSlot Build()
            {
                return this.slot;
            }
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct AdSlotStruct
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string slotId;
        
        [MarshalAs(UnmanagedType.I4)]
        public int adCount;
        
        [MarshalAs(UnmanagedType.I4)]
        public int width;
        
        [MarshalAs(UnmanagedType.I4)]
        public int height;
        
        [MarshalAs(UnmanagedType.I4)]
        public int adType;
        
        [MarshalAs(UnmanagedType.I4)]
        public int adLoadType;
        
        [MarshalAs(UnmanagedType.I4)]
        public int intervalTime;
        
        [MarshalAs(UnmanagedType.I4)]
        public int viewHeight;

        [MarshalAs(UnmanagedType.I4)]
        public int viewWidth;

        [MarshalAs(UnmanagedType.I4)]
        public int supportIconStyle;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
        public string mediaExtra;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string userId;

        [MarshalAs(UnmanagedType.I4)]
        public int rewardAmount;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string rewardName;

        [MarshalAs(UnmanagedType.I4)]
        public int m_bidNotify;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
        public string m_cenarioId;

        [MarshalAs(UnmanagedType.I4)]
        public int m_isMuted;

        [MarshalAs(UnmanagedType.I4)]
        public int m_isRotate;

        [MarshalAs(UnmanagedType.I4)]
        public int m_expectedHorizontal;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string m_s_AdnName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string m_s_AdnSlotId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string m_s_AppId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string m_s_Appkey;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string m_autoPlayPolicy;

       [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
        public string m_twoStageInfo;
    }

    public class AdSlotBuilder
    {
        public static AdSlotStruct getAdSlot(AdSlot adSlot)
        {
            AdSlotStruct slot = new AdSlotStruct()
            {
                m_expectedHorizontal = adSlot.orientation == AdOrientation.Horizontal ? 1 : 0,
                slotId = adSlot.CodeId,
                adCount = adSlot.adCount,
                width = adSlot.width,
                height = adSlot.height,
                adType = (int)adSlot.type,
                adLoadType = (int)adSlot.adLoadType,
                intervalTime = adSlot.intervalTime,
                viewHeight = adSlot.viewheight,
                viewWidth = adSlot.viewwidth,
                supportIconStyle = adSlot.supportIconStyle,
                mediaExtra = adSlot.mediaExtra,
                userId = adSlot.UserId,
                rewardAmount = adSlot.rewardAmount,
                rewardName = adSlot.rewardName,
                m_bidNotify = adSlot.mAdSlot != null ? (adSlot.mAdSlot.bidNotify ? 1 : 0) : 0,
                m_cenarioId = adSlot.mAdSlot != null ? adSlot.mAdSlot.scenarioId : null,
                m_isMuted = adSlot.mAdSlot != null ? (adSlot.mAdSlot.muted ? 1 : 0) : 0,
                m_isRotate = adSlot.mAdSlot != null ? (adSlot.mAdSlot.enableRotate ? 1 : 0) : 0,
                m_s_AdnName = adSlot.mAdSlot != null ? (adSlot.mAdSlot.mediationSplashRequestInfo != null ? adSlot.mAdSlot.mediationSplashRequestInfo.AdnName : null) : null,
                m_s_AdnSlotId = adSlot.mAdSlot != null ? (adSlot.mAdSlot.mediationSplashRequestInfo != null ? adSlot.mAdSlot.mediationSplashRequestInfo.AdnSlotId : null) : null,
                m_s_AppId = adSlot.mAdSlot != null ? (adSlot.mAdSlot.mediationSplashRequestInfo != null ? adSlot.mAdSlot.mediationSplashRequestInfo.AppId : null) : null,
                m_s_Appkey = adSlot.mAdSlot != null ? (adSlot.mAdSlot.mediationSplashRequestInfo != null ? adSlot.mAdSlot.mediationSplashRequestInfo.Appkey : null) : null,
                m_autoPlayPolicy = adSlot.mAdSlot != null ? adSlot.mAdSlot.autoPlayPolicy : null,
                m_twoStageInfo = adSlot.mAdSlot != null ? adSlot.mAdSlot.twoStageInfo : null
            };
            return slot;
        }
    }
#endif
}
