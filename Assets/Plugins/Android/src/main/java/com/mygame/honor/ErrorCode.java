package com.mygame.honor;


public class ErrorCode {
    public static final int SDK_LOAD_FAILED = 10009;//	SDK加载失败	再次调用初始化
    public static final int GRS_INIT_FAILED = 10011;//	网络错误	提示网络错误，再次点击重新链接网络
    public static final int PROTOCOL_INIT_FAILED = 10021;//	协议签署被拒绝	再次点击，拉起协议签署按钮
    public static final int REAL_NAME_INIT_CANCEL = 107021;//	用户取消实名认证	再次点击打开实名认证
    public static final int SDK_STATE_NO_SUPPORT = 107006;//	当前地区不支持此业务	不处理
    public static final int SIGN_IN_AUTH = 202002;//	用户尚未授权给应用	点击再次尝试SDK初始化，拉起登录
    public static final int NO_ACTIVITY_FAILED = 10007;//初始化没有activity传入	检查代码，是否初始化正确
    public static final int NO_NETWORK_FAILED = 10008;//网络未连接	检查网络是否开启
    public static final int SDK_TOKEN_INVALID = 307001;//Token无效	点击再次尝试SDK初始化，拉起登录
    public static final int SDK_TOKEN_EXPIRED = 307002;//Token过期	    跳转授权登录界面，重新登录账号
    public static final int SDK_TOKEN_LOGIN_INVALID = 307003;//登录状态无效	跳转授权登录界面，重新登录账号
    public static final int UNDERAGE_NOT_GAME_PERIOD = 307004;//未成年人非游戏时间段	正常流程,退出游戏
    public static final int GAME_STATE_FAILED = 307005;//公共接口返回错误	建议检查传入参数，如果无误建议反馈
    public static final int GAME_STATE_CALL_REPEAT = 307012;//2秒内不要重复调用接口	2秒后再试
    public static final int GAME_STATE_CALLBACK_NULL = 307013;//数据返回空	反馈
    public static final int GAME_STATE_NOT_APPROVED = 307007;//当前应用未审核通过	去开发者平台查看应用审核是否通过
    public static final int GAME_STATE_NOT_LOGIN = 207013;//未登录荣耀帐号	请重新调用init方法初始化
    public static final int ACCESS_SERVER_RETURN_ERROR = 107007;//服务端错误	反馈
    public static final int REMOVE_ACCOUNT = 207014;//  切换 / 退出了荣耀账号 请重新调用init方法初始化

}
