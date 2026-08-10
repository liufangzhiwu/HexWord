namespace ByteDance.Union
{
    public class AdConst
    {
        public const string PangleSdkVersion = "7.6.0.0";

        // 网络状态常量
        public const int NETWORK_STATE_MOBILE = 1;
        public const int NETWORK_STATE_2G = 2;
        public const int NETWORK_STATE_3G = 3;
        public const int NETWORK_STATE_WIFI = 4;
        public const int NETWORK_STATE_4G = 5;
        // adn name常量
        public const string ADN_GDT = "gdt";
        public const string ADN_PANGLE = "pangle";
        public const string ADN_ADMOB = "admob";
        public const string ADN_MINTEGRAL = "mintegral";
        public const string ADN_UNITY = "unity";
        public const string ADN_BAIDU = "baidu";
        public const string ADN_KS = "ks";
        public const string ADN_SIGMOB = "sigmob";
        public const string ADN_KLEVIN = "klevin";

        // 流量分组性别常量
        public const string GENDER_MALE = "male";
        public const string GENDER_FEMALE = "female";
        public const string GENDER_UNKNOWN = "unknown";

        // 以下是开启gromore服务验证时的参数
        public const string KEY_GROMORE_EXTRA = "gromoreExtra"; // gromore服务验证的参数，开发者通过adslot传入
        
        // 抖音授权成功状态回调, 媒体可以通过map获取抖音openuid用以判断是否下发奖励
        public const int AD_EVENT_AUTH_DOUYIN = 1;

        // title bar主题, 0:亮色主题 1:暗色主题 -1:无title bar
        public const int TITLE_BAR_THEME_LIGHT = 0;
        public const int TITLE_BAR_THEME_DARK = 1;
        public const int TITLE_BAR_THEME_NO_TITLE_BAR = -1;

        /* 
         * 通过MediationAdSlot中SetExtraObject可设置的key合集
         */
        ///（聚合）扭动视图Enable
        ///value为string类型，"0"不允许，"1"允许
        ///生效的adn为：ks
        ///生效的平台为：iOS, Android。
        public const string KEY_M_MEDIA_AD_ROTATE_VIEW_ENABLE = "rotate_enable";
        ///（聚合）设置不同网络下的视频播放策略
        ///value为string类型，不设置则执行对应adn的默认策略 "0"-WIFI 下自动播放 "1"-总是自动播放，无论网络条件 "2"-从不自动播放，无论网络条件
        ///生效的adn为：gdt
        ///生效的平台为：iOS
        public const string KEY_M_AUTO_PLAY_POLICY = "auto_play_policy";
        ///（聚合）激励视频新增二段激励的配置
        ///value为string json类型
        ///生效的adn为：baidu
        ///生效的平台为：iOS, Android。
        public const string KEY_M_TWO_STAGE_INFO = "TwoStageInfo";
        ///（聚合）是否要获取各个adn加载失败的信息，加载失败回调里会有adn加载失败信息
        ///value为string类型，"0"不获取，"1"获取
        ///生效的adn为：所有adn
        ///生效的平台为：Android。
        public const string KEY_M_SHOW_ADN_LOAD_ERROR_DETAIL = "show_adn_load_error_detail";


        /* 
         * 通过PrivacyConfiguration中UserPrivacyConfig可设置的key合集
         */
        ///（聚合）是否允许获取运营商信息
        ///value为string类型，"0"标识禁止获取，"1"标识允许获取
        ///生效的adn为：gdt
        ///生效的平台为：Android。
        public const string KEY_NET_OP = "netop"; // 
        ///（聚合）是否允许获取设备ip
        ///value为string类型，"0"标识禁止获取，"1"标识允许获取
        ///生效的adn为：gdt
        ///生效的平台为：Android。
        public const string KEY_M_IP_ADDR = "mipaddr"; 
        ///（聚合）是否允许获取设备ip
        ///value为string类型，"0"标识禁止获取，"1"标识允许获取
        ///生效的adn为：gdt
        ///生效的平台为：Android。
        public const string KEY_W_IP_ADDR = "wipaddr";
        ///（聚合）是否允许获取taid
        ///value为string类型，"0"标识禁止获取，"1"标识允许获取
        ///生效的adn为：gdt
        ///生效的平台为：Android。
        public const string KEY_TAID = "taid"; 
        ///（聚合）是否允许监听安装卸载app
        ///value为string类型，"0"标识禁止，"1"标识允许
        ///生效的adn为：gdt和baidu均适用。
        ///生效的平台为：Android。
        public const string KEY_INSTALL_UNINSTALL_LISTEN = "installUninstallListen"; 
        ///（聚合）用户对广告商跟踪的同意 ，
        ///value为string类型，@"1"代表同意，@"0"代表不同意，默认@"0"，
        ///生效的adn为：GDT。
        ///生效的平台为：iOS。
        public const string bum_advertiser_tracking_enabled = "bum_advertiser_tracking_enabled";
        ///（聚合）实时的地理位置获取时间，值为字符串格式的unix时间戳，单位秒，比如@"1639450944"，
        ///value为string类型，默认null，
        ///生效的adn为：GDT。
        ///生效的平台为：iOS。
        public const string bum_loc_time = "bum_loc_time";
        ///（聚合）限制个性化CPU，
        ///value为string类型，@"1"代表限制，@"0"代表不限制，默认@"0"，
        ///生效的adn为：BAIDU。
        ///生效的平台为：iOS。
        public const string bum_limit_personal_cpus = "bum_limit_personal_cpus";
        ///（聚合）禁用使用电话状态，
        ///value为string类型，@"1"代表禁用，@"0"代表不禁用，默认@"0"，
        ///生效的adn为：KS。
        ///生效的平台为：iOS。
        public const string bum_disable_use_phone_status = "bum_disable_use_phone_status";
        ///（聚合）设置自定义idfv，
        ///value为string类型，默认null。
        ///生效的adn为：KS，Mintegral，Sigmob。
        ///生效的平台为：iOS。
        public const string bum_custom_idfv = "bum_custom_idfv";
        ///（聚合）是否禁止IDFV，
        ///value为string类型，@"1"代表禁用，@"0"代表不禁用，默认@"0"，
        ///生效的adn为：Mintegral，Sigmob。
        ///生效的平台为：iOS。
        public const string bum_forbidden_idfv = "bum_forbidden_idfv";
    }
}
