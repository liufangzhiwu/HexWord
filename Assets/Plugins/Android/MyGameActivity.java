package com.mygame.honor;

import android.os.Bundle;
import android.util.Log;
import com.unity3d.player.UnityPlayer;
import com.unity3d.player.UnityPlayerActivity;
import com.hihonor.gamecenter.gcjointsdk.sdk.GCJointSdk;

public class MyGameActivity extends UnityPlayerActivity{

    private static final String TAG = "HonorBridge";

    @Override
    protected void onCreate(Bundle savedInstanceState){
        super.onCreate(savedInstanceState);

        Log.d(TAG, "MyGameActivity 启动了， 准备初始化荣耀 SDK");
        GCJointSdk.setApplication(this);

    }

        /**
     * 开始游戏初始化
     */
    private void init() {
        // 开始游戏初始化，GCJointSdk.init接口调用有以下注意事项
        // 1. 需要在有前台显示页面后再调用GCJointSdk.init进行初始化
        // 2. 实名制需要将新闻出版局申请的bizID配置到荣耀开发者平台:https://developer.hihonor.com/cn/doc/guides/101032#h1-1684328652807
        // 3. 确保已在荣耀开发者平台提交应用SHA256证书指纹，否则将返回202002错误,录入SHA256证书指纹方法:https://developer.hihonor.com/cn/kitdoc?category=%E5%9F%BA%E7%A1%80%E6%9C%8D%E5%8A%A1&kitId=11001&navigation=guides&docId=android-generate-appsign.md&token=
        AppParams params = new AppParams.Builder()
                .setAppId(getString(R.string.the_appid)) // 必填，荣耀开发者服务平台申请的应用AppId
                .setCpId(getString(R.string.the_cpid)) // 必填，开发者ID，在荣耀开发者服务平台申请开发者帐号后生成，在开发者资料中查看。
                .setEnableLog(false) // 开发调试时打开日志，正式发布时关闭
                .setAntiAddictionCallback(() -> { // 防沉迷回调
                    // 防沉迷时间到，处理退出逻辑
                    System.exit(0);
                })
                .build();
        GCJointSdk.init(params, new APICallback() {
            @Override
            public void onSuccess(String result) {
                // 初始化成功，获取登录态及账号相关信息
                try {
                    JSONObject json = new JSONObject(result);
                    String openId = json.getString("openid");//荣耀用户标识，同一用户在不同appid下openID不同
                    String unionId = json.getString("unionId");//荣耀用户标识，同一用户在同一个开发者下unionId相同
                    String token = json.getString("token");//身份验证信息，可用于服务端校验
                    String isAdult = json.getString("isAdult");//用户是否成年，"true"为成年，"false"为非成年
                    String displayName = json.getString("displayName");//用户名称
                    String headPictureURL = json.getString("headPictureURL");//用户头像地址，没有头像时为"null"
                } catch (JSONException e) {
                }
                // 开始进入游戏
                enterGame();
            }

            @Override
            public void onFailure(int code, String message) {
                // 初始化失败，获取失败code
                initFailure(code);
            }
        });
    }

       /**
     * 初始化游戏，错误码处理
     *
     * @param code
     */

    private void initFailure(int code) {
        switch (code) {
            case ErrorCode.SDK_LOAD_FAILED://10009 SDK加载失败 再次调用初始化
            case ErrorCode.SIGN_IN_AUTH://202002 用户尚未授权给应用 点击再次尝试SDK初始化，拉起登录
            case ErrorCode.SDK_TOKEN_INVALID://307001 Token无效 点击再次尝试SDK初始化，拉起登录
            case ErrorCode.SDK_TOKEN_EXPIRED://307002 Token过期 跳转授权登录界面，重新登录账号
            case ErrorCode.SDK_TOKEN_LOGIN_INVALID://307003 登录状态无效 跳转授权登录界面，重新登录账号
            case ErrorCode.GAME_STATE_NOT_LOGIN://   207013 未登录荣耀帐号 请重新调用init方法初始化
            case ErrorCode.REMOVE_ACCOUNT://    207014 切换 / 退出了荣耀账号 请重新调用init方法初始化
                //需要接入方自己处理以上错误code 添加提示让用户选择是继续登录还是退出游戏，重新初始化会再次调起登录页。
                init();
                break;
            case ErrorCode.GRS_INIT_FAILED:// 10011 网络错误 提示网络错误，再次点击重新链接网络
            case ErrorCode.NO_NETWORK_FAILED://10008 网络未连接 检查网络是否开启
                //需要接入方自己处理
                break;
            case ErrorCode.NO_ACTIVITY_FAILED://10007 初始化没有activity传入 检查代码，是否初始化正确
            case ErrorCode.GAME_STATE_FAILED:// 307005 公共接口返回错误 建议检查传入参数，如果无误建议反馈
                //需要接入方自己处理
                break;
            case ErrorCode.GAME_STATE_CALLBACK_NULL://  307013 数据返回空 反馈
            case ErrorCode.ACCESS_SERVER_RETURN_ERROR://    107007 服务端错误 反馈
                //需要接入方自己处理
                break;
            case ErrorCode.PROTOCOL_INIT_FAILED://10021 协议签署被拒绝 再次点击，拉起协议签署按钮
                //需要接入方自己处理
                break;
            case ErrorCode.REAL_NAME_INIT_CANCEL://107021 用户取消实名认证 再次点击打开实名认证
                //需要接入方自己处理
                break;
            case ErrorCode.SDK_STATE_NO_SUPPORT://107006 当前地区不支持此业务 不处理
                //需要接入方自己处理
                break;
            case ErrorCode.UNDERAGE_NOT_GAME_PERIOD:// 307004 未成年人非游戏时间段 正常流程, 退出游戏
                //需要接入方自己处理
                break;
            case ErrorCode.GAME_STATE_CALL_REPEAT://  307012 2 秒内不要重复调用接口 2 秒后再试
                //需要接入方自己处理
                break;
            case ErrorCode.GAME_STATE_NOT_APPROVED://   307007 当前应用未审核通过 去开发者平台查看应用审核是否通过
                //需要接入方自己处理
                break;
            default:
                break;

        }
    }
    /**
     * 初始化成功，进入游戏
     */
    private void enterGame() {
        findViewById(R.id.disable_zone).setVisibility(View.GONE);
    }

    /**
     * 上报玩家游戏角色
     * roleId	String	是	角色Id
     * roleName	String	是	角色昵称
     * roleLevel	Int	是	角色等级
     * realmId	String	是	区服Id
     * realmName	String	是	区服名称
     * chapter	String	是	关卡章节
     * ext	Map<String,String>	否	扩展字段，键值对必须都是String类型
     * combatValue	String	String	否	战力值，有战力值的网游必须上传
     * pointValue	String	String	否	积分值，积分类休闲游戏必须上传
     */
    public void reportUserGameInfoData(roleId, roleName, roleLevel, chapter) {
        HashMap<String, String> ext = new HashMap<>();
        ext.put("combatValue", "1000");
        ext.put("pointValue", "2000");
        UserGameInfoParam userGameInfoParam = new UserGameInfoParam(roleId, roleName, roleLevel, "一区", "全区", chapter, ext);
        GCJointSdk.reportUserGameInfoData(userGameInfoParam, new APICallback() {
            @Override
            public void onSuccess(String resultMessage) {
                Log.i(TAG, "上报游戏角色数据成功 ,resultMessage:" + resultMessage);
            }

            @Override
            public void onFailure(int resultCode, String resultMessage) {
                Log.i(TAG, "上报游戏角色数据失败 ,resultCode:" + resultCode + "resultMessage:" + resultMessage);
            }
        });
    }

    /**
     * 退出游戏挽留弹框
     */
    public void exitControl() {
        //退出管控
        GCJointSdk.exit(this, code -> {
            switch (code) {
                case 0://弹框消失，用户在挽留弹框上选择了取消退出
                case 2://弹框消失，用户在挽留弹框上选择了跳转
                    break;
                case 1://弹框消失，用户在挽留弹框上选择了退出游戏
                case 3://未配置或者未登录，未弹出挽留弹框
                {
                    // 此处如何退出可以执行unity的退出应用生命周期？
                    android.os.Process.killProcess(android.os.Process.myPid());//杀进程
                }
                break;
                default:
                    break;
            }
        });
    }

}