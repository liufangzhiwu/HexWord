namespace ByteDance.Union.Mediation
{
#if !UNITY_EDITOR && UNITY_IOS
   /// <summary>
    /// The slot of a Mediation.
    /// </summary>
    public sealed class MediationAdSlot
    {

        internal bool splashShakeButton;
        internal bool allowShowCloseBtn;
        internal float shakeViewSizeW;
        internal float shakeViewSizeH;
        internal string wxAppId;
        internal MediationSplashRequestInfo mediationSplashRequestInfo;
        internal bool splashPreLoad;
        internal bool muted;
        internal float volume;
        internal bool useSurfaceView;
        internal bool bidNotify;
        internal string scenarioId;
        internal string rewardName;
        internal int rewardAmount;
        internal bool enableRotate = false;
        internal string autoPlayPolicy;
        internal string twoStageInfo;

        /// <summary>
        /// The builder used to build an Mediation Ad slot.
        /// </summary>
        public class Builder
        {
            private MediationAdSlot mAdSlot = new MediationAdSlot();

            public Builder SetSplashShakeButton(bool splashShakeBtn)
            {
                this.mAdSlot.splashShakeButton = splashShakeBtn;
                return this;
            }

            public Builder SetAllowShowCloseBtn(bool allowShowCloseBtn)
            {
                this.mAdSlot.allowShowCloseBtn = allowShowCloseBtn;
                return this;
            }

            public Builder SetShakeViewSize(float w, float h)
            {
                this.mAdSlot.shakeViewSizeW = w;
                this.mAdSlot.shakeViewSizeH = h;
                return this;
            }

            public Builder SetWxAppId(string wxAppId)
            {
                this.mAdSlot.wxAppId = wxAppId;
                return this;
            }

            public Builder SetMediationSplashRequestInfo(MediationSplashRequestInfo info)
            {
                this.mAdSlot.mediationSplashRequestInfo = info;
                return this;
            }

            public Builder SetSplashPreLoad(bool isPreload)
            {
                this.mAdSlot.splashPreLoad = isPreload;
                return this;
            }

            /// <summary>
            /// Sets the muted.
            /// </summary>
            public Builder SetMuted(bool isMuted)
            {
                this.mAdSlot.muted = isMuted;
                return this;
            }

            public Builder SetVolume(float volume)
            {
                this.mAdSlot.volume = volume;
                return this;
            }

            public Builder SetUseSurfaceView(bool isUseSurfaceview)
            {
                this.mAdSlot.useSurfaceView = isUseSurfaceview;
                return this;
            }

            public Builder SetBidNotify(bool bidNotify)
            {
                this.mAdSlot.bidNotify = bidNotify;
                return this;
            }

            public Builder SetScenarioId(string scenarioId)
            {
                this.mAdSlot.scenarioId = scenarioId;
                return this;
            }

            public Builder SetRewardName(string rewardName)
            {
                this.mAdSlot.rewardName = rewardName;
                return this;
            }

            public Builder SetRewardAmount(int rewardAmount)
            {
                this.mAdSlot.rewardAmount = rewardAmount;
                return this;
            }

            public Builder SetExtraObject(string key, string value)
            {
                if (key == AdConst.KEY_M_MEDIA_AD_ROTATE_VIEW_ENABLE && value == "1") {
                    this.mAdSlot.enableRotate = true;
                }
                if (key == AdConst.KEY_M_AUTO_PLAY_POLICY) {
                    this.mAdSlot.autoPlayPolicy = value;
                }
                if (key == AdConst.KEY_M_TWO_STAGE_INFO)
                {
                    this.mAdSlot.twoStageInfo = value;
                }
                return this;
            }

            /// <summary>
            /// Build the Ad slot.
            /// </summary>
            public MediationAdSlot Build()
            {
                return this.mAdSlot;
            }
        }
    }
#endif
}