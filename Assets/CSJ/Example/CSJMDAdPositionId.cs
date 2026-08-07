
// 测试广告位id 
public static class CSJMDAdPositionId
{
#if UNITY_IOS
        public const string APP_ID = "5000546";

        /* 穿山甲广告位ID */
        // 激励视频
        public const string CSJ_REWARD_V_ID = "900546826";
        public const string CSJ_REWARD_H_ID = "900546319";
        // 信息流
        public const string CSJ_NATIVE_EXPRESS_ID = "945520482";
        public const string CSJ_NATIVE_ID = "945870984";
        // 新插屏视频
        public const string CSJ_ExpressFullScreen_V_ID = "945113164";// 900546551 竖屏
        public const string CSJ_ExpressFullScreen_H_ID = "945113165";// 900546831 横屏
        // 开屏
        public const string CSJ_SPLASH_V_ID = "800546808";
        public const string CSJ_SPLASH_H_ID = "887341408";
        // 横幅
        public const string CSJ_BANNER_ID = "900546269";
        public const string CSJ_NATIVE_BANNER_ID = "900546687";
        // draw
        public const string CSJ_DRAW_ID = "";

        /* 聚合维度广告位ID */
        // 激励视频
        public const string M_REWARD_VIDEO_V_ID = "945801623";
        public const string M_REWARD_VIDEO_H_ID = "945494739";
        // 信息流
        public const string M_NATIVE_NORMAL_ID = "945494760";
        public const string M_NATIVE_EXPRESS_ID = "945494759";
        // 开屏
        public const string M_SPLASH_EXPRESS_ID = "887418500";
        // 开屏兜底
        public const string M_SPLASH_BASELINE_APPID = "5000546";
        public const string M_SPLASH_BASELINE_ID = "800546808";
        // 横幅
        public const string M_BANNER_ID = "945494753";
        // 插全屏
        public const string M_INTERSTITAL_FULL_SCREEN_ID = "948070210";
        public const string M_INTERSTITAL_FULL_SCREEN_ID_2 = "947028072";
        public const string M_INTERSTITAL_FULL_SCREEN_ID_3 = "946961656";
        // draw
        public const string M_DRAW_ID = "948423177";
#else
    public const string APP_ID = "5001121";

    /* 穿山甲广告位ID */
    // 激励视频
    public const string CSJ_REWARD_V_ID = "901121365";
    public const string CSJ_REWARD_H_ID = "901121430";
    // 信息流
    public const string CSJ_NATIVE_EXPRESS_ID = "901121253";
    public const string CSJ_NATIVE_ID = "901121737";
    // 新插屏视频
    public const string CSJ_ExpressFullScreen_V_ID = "901121375";//901121375
    public const string CSJ_ExpressFullScreen_H_ID = "901121516";
    // 开屏
    public const string CSJ_SPLASH_V_ID = "801121648";
    public const string CSJ_SPLASH_H_ID = "887341406";
    // 横幅
    // public const string CSJ_BANNER_ID = "945666755"; // 普通代码位
    public const string CSJ_BANNER_ID = "901121834"; // client bidding代码位
    public const string CSJ_NATIVE_BANNER_ID = "901121423";
    // draw
    public const string CSJ_DRAW_ID = "";


    /* 聚合维度广告位ID */
    // 激励视频
    public const string M_REWARD_VIDEO_H_ID = "945493668";
    public const string M_REWARD_VIDEO_V_ID = "945700410";
    // 信息流
    public const string M_NATIVE_NORMAL_ID = "945493689";
    public const string M_NATIVE_EXPRESS_ID = "102161114";
    // 开屏
    public const string M_SPLASH_EXPRESS_ID = "102117864";
    // 开屏兜底
    public const string M_SPLASH_BASELINE_APPID = "5001121";
    public const string M_SPLASH_BASELINE_ID = "887382976";
    // 横幅
    public const string M_BANNER_ID = "945493677";
    // 插全屏
    public const string M_INTERSTITAL_FULL_SCREEN_ID = "945493675"; // 插全屏、全屏
    public const string M_INTERSTITAL_FULL_SCREEN_ID_2 = "945864901";
    public const string M_INTERSTITAL_FULL_SCREEN_ID_3 = "945854486";
    // draw
    public const string M_DRAW_ID = "102132711";
#endif
}