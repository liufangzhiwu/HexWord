namespace ByteDance.Union.Mediation
{
    using System.Collections.Generic;

    public class MediationPrivacyConfig
    {
        /// <summary>
        ///  仅android支持
        /// </summary>
        public List<string> CustomAppList { get; set; }
        /// <summary>
        ///  仅android支持
        /// </summary>
        public List<string> CustomDevImeis { get; set; }

        /// <summary>
        ///  仅android支持，默认值true.
        /// </summary>
        public bool CanUseOaid { get; set; } = true;

        /// <summary>
        /// 是否限制个性化广告，需adn支持，默认值false.
        /// </summary>
        public bool LimitPersonalAds { get; set; } = false;

        /// <summary>
        /// 是否程序化广告，需adn支持，仅iOS支持，默认值true.
        /// </summary>
        public bool ProgrammaticRecommend { get; set; } = true;

    }
}
