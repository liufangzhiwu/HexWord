#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)

namespace ByteDance.Union.Mediation
{
    /// <summary>
    /// The slot of a Mediation.
    /// </summary>
    public sealed class MediationAdSlot
    {
        /// <summary>
        /// The builder used to build an Mediation Ad slot.
        /// </summary>
        public class Builder
        {
            public Builder SetSplashShakeButton(bool splashShakeBtn) { return this; }

            public Builder SetAllowShowCloseBtn(bool allowShowCloseBtn) { return this; }

            public Builder SetShakeViewSize(float w, float h) { return this; }

            public Builder SetWxAppId(string wxAppId) { return this; }

            public Builder SetMediationSplashRequestInfo(MediationSplashRequestInfo info) { return this; }

            public Builder SetSplashPreLoad(bool isPreload) { return this; }

            /// <summary>
            /// Sets the muted.
            /// </summary>
            public Builder SetMuted(bool isMuted) { return this; }

            public Builder SetVolume(float volume) { return this; }

            public Builder SetUseSurfaceView(bool isUseSurfaceview) { return this; }

            public Builder SetBidNotify(bool bidNotify) { return this; }

            public Builder SetScenarioId(string scenarioId) { return this; }

            public Builder SetRewardName(string rewardName) { return this; }

            public Builder SetRewardAmount(int rewardAmount) { return this; }

            public Builder SetExtraObject(string key, string value) { return this; }

            /// <summary>
            /// Build the Ad slot.
            /// </summary>
            public MediationAdSlot Build() { return new MediationAdSlot(); }
        }
    }

}

#endif