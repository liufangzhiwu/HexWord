package com.mygame.honor;

import android.os.Bundle;
import android.util.Log;
import com.unity3d.player.UnityPlayer;
import com.unity3d.player.UnityPlayerActivity;
import com.hihonor.gamecenter.gcjointsdk.sdk.GCJointSdk;

public class MyGameActivity extends UnityPlayerActivity{

    private static final String TAG = "HonorBridge";
    private String mContinueToken;

    @Override
    protected void onCreate(Bundle savedInstanceState){
        super.onCreate(savedInstanceState);

        Log.d(TAG, "MyGameActivity 启动了， 准备初始化荣耀 SDK");
        GCJointSdk.setApplication(this);
    }

        /**
     * 开始游戏初始化
     */
    public void init() {
        // 开始游戏初始化，GCJointSdk.init接口调用有以下注意事项
        // 1. 需要在有前台显示页面后再调用GCJointSdk.init进行初始化
        // 2. 实名制需要将新闻出版局申请的bizID配置到荣耀开发者平台:https://developer.hihonor.com/cn/doc/guides/101032#h1-1684328652807
        // 3. 确保已在荣耀开发者平台提交应用SHA256证书指纹，否则将返回202002错误,录入SHA256证书指纹方法:https://developer.hihonor.com/cn/kitdoc?category=%E5%9F%BA%E7%A1%80%E6%9C%8D%E5%8A%A1&kitId=11001&navigation=guides&docId=android-generate-appsign.md&token=
        AppParams params = new AppParams.Builder()
                .setAppId("104534282") // 必填，荣耀开发者服务平台申请的应用AppId
                .setCpId("110000134609") // 必填，开发者ID，在荣耀开发者服务平台申请开发者帐号后生成，在开发者资料中查看。
                .setEnableLog(true) // 开发调试时打开日志，正式发布时关闭
                .setSanBoxToken("3EA1D7ABBCA2B03D2EF5B295244EEEBB")
                .setAntiAddictionCallback(() -> { // 防沉迷回调
//                     Log.w(TAG, "【防沉迷触发】时间已到，通知 Unity 进行下线处理");
                    // 防沉迷时间到，处理退出逻辑
                    UnityPlayer.UnitySendMessage("HonorManager", "OnAntiAddictionTimeOut", "");
                })
                .build();
        GCJointSdk.init(params, new APICallback() {
            @Override
            public void onSuccess(String result) {
                // 初始化成功，获取登录态及账号相关信息
                    // JSONObject json = new JSONObject(result);
                    // String openId = json.getString("openid");//荣耀用户标识，同一用户在不同appid下openID不同
                    // String unionId = json.getString("unionId");//荣耀用户标识，同一用户在同一个开发者下unionId相同
                    // String token = json.getString("token");//身份验证信息，可用于服务端校验
                    // String isAdult = json.getString("isAdult");//用户是否成年，"true"为成年，"false"为非成年
                    // String displayName = json.getString("displayName");//用户名称
                    // String headPictureURL = json.getString("headPictureURL");//用户头像地址，没有头像时为"null"
                UnityPlayer.UnitySendMessage("HonorManager", "OnLoginSuccess", result);
        
                // 2. 进入游戏逻辑， 如果有
                // runOnUiThread(() -> {
                //     // hideLoading(); // 隐藏 Loading
                //     enterGame();     // 处理原生 UI 隐藏
                // });
            }

            @Override
            public void onFailure(int code, String message) {
                // 初始化失败，获取失败code
                initFailure(code);
                // 必须在主线程处理 UI
                runOnUiThread(() -> {
                    // hideLoading(); // 隐藏 Loading
                    initFailure(code, message);
                });
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
            case ErrorCode.GRS_INIT_FAILED:// 10011 网络错误 提示网络错误，再次点击重新链接网络
            case ErrorCode.NO_NETWORK_FAILED://10008 网络未连接 检查网络是否开启
            case ErrorCode.PROTOCOL_INIT_FAILED://10021 协议签署被拒绝 再次点击，拉起协议签署按钮
            case ErrorCode.REAL_NAME_INIT_CANCEL://107021 用户取消实名认证 再次点击打开实名认证
                //需要接入方自己处理
                String tip = "登录验证失败或取消，请重试。(" + code + ")";
                if(code == ErrorCode.NO_NETWORK_FAILED) tip = "网络连接失败，请检查网络设置。";
                if(code == ErrorCode.REAL_NAME_INIT_CANCEL) tip = "根据相关规定，未实名认证无法进入游戏。";
                
                showRetryDialog(tip);
                break;
            case ErrorCode.SDK_STATE_NO_SUPPORT://107006 当前地区不支持此业务 不处理
                //需要接入方自己处理
                showExitDialog("当前地区不支持此服务。", true);
                break;
            case ErrorCode.UNDERAGE_NOT_GAME_PERIOD:// 307004 未成年人非游戏时间段 正常流程, 退出游戏
                //需要接入方自己处理
                showExitDialog("根据防沉迷规定，当前时段未成年人无法登录游戏。", true);
                break;
            case ErrorCode.GAME_STATE_CALL_REPEAT://  307012 2 秒内不要重复调用接口 2 秒后再试
                //需要接入方自己处理
                Log.w(TAG, "操作太频繁，请稍后");
                new android.os.Handler().postDelayed(this::init, 2000);
                break;
            case ErrorCode.GAME_STATE_CALLBACK_NULL://  307013 数据返回空 反馈
            case ErrorCode.ACCESS_SERVER_RETURN_ERROR://    107007 服务端错误 反馈
            case ErrorCode.GAME_STATE_FAILED:// 307005 公共接口返回错误 建议检查传入参数，如果无误建议反馈
            case ErrorCode.NO_ACTIVITY_FAILED://10007 初始化没有activity传入 检查代码，是否初始化正确
            case ErrorCode.GAME_STATE_NOT_APPROVED://   307007 当前应用未审核通过 去开发者平台查看应用审核是否通过
            default:
                showRetryDialog("发生未知错误 (" + code + ")，请重试。");
                break;

        }
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
    public void reportUserGameInfoData(String roleId, String roleName, String chapter) {
        HashMap<String, String> ext = new HashMap<>();
        ext.put("combatValue", "1000");
        ext.put("pointValue", "2000");
        UserGameInfoParam userGameInfoParam = new UserGameInfoParam(roleId, roleName, 1, "一区", "全区", chapter, ext);
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
                    // android.os.Process.killProcess(android.os.Process.myPid());//杀进程
                     UnityPlayer.UnitySendMessage("HonorManager", "OnExitControl", code);
                }
                break;
                default:
                    break;
            }
        });
    }

    /**
     * 发起购买PMS商品
     */
    public void orderWithPMS(String product_id) {
        Log.d(TAG, "开始购买流程: " + product_id);
        ProductOrderIntentReq productOrderIntentReq = new ProductOrderIntentReq();
        productOrderIntentReq.setProductType(ProductType.CONSUME);
        productOrderIntentReq.setProductId(product_id);
        productOrderIntentReq.setNeedSandboxTest(1);//传1为沙盒测试
        //防止掉单
        obtainOwnedPurchases();
        //创建订单前，需要调用obtainOwnedPurchases 查询已购买，未消耗的商品，进行消耗
        GCJointSdk.launchPayFlow(this, productOrderIntentReq, new IapClient.QuickPayCallback() {
            @Override
            public void onSuccess(PurchaseResultInfo purchaseResultInfo, PurchaseProductInfo purchaseProductInfo) {
                Log.d(TAG, "launchPayFlow pms success:purchaseResultInfo = " + purchaseResultInfo + ";;;productInfo=" + purchaseProductInfo);
                Log.d(TAG, "支付成功，准备进行消耗: " + purchaseProductInfo.getProductId());
                consume(purchaseProductInfo);
            }

            @Override
            public void onFail(ApiException apiException) {
                Log.e(TAG, "launchPayFlow pms fail:" + apiException.getMessage());
                String errorMsg = "Code:" + apiException.errorCode + " Msg:" + apiException.getMessage();
                UnityPlayer.UnitySendMessage("HonorManager", "OnPurchaseFailed", errorMsg);
            }
        });
    }
     /**
     * 商品消耗
     */
    public void consume(PurchaseProductInfo purchaseProductInfo) {
        if (purchaseProductInfo == null) {
            Log.e(TAG, "consume error: purchaseProductInfo is null");
            return;
        }
        Log.d(TAG, "开始调用消耗接口, 商品ID: " + purchaseProductInfo.getProductId());
        //支付成功后默认消耗，用户也可以根据实际情况消耗
        ConsumeReq comsumeReq = new ConsumeReq();
        //根据PurchaseToken 进行消耗
        comsumeReq.setPurchaseToken(purchaseProductInfo.getPurchaseToken());
        Task<ConsumeResult> comsumeRespTask = GCJointSdk.consumeProduct(comsumeReq);
        comsumeRespTask.addOnSuccessListener(consumeResult -> {
//             PurchaseProductInfo purchase = JsonUtil.parse(consumeResult.getConsumeData(), PurchaseProductInfo.class);
            String productId = purchaseProductInfo.getProductId();
            UnityPlayer.UnitySendMessage("HonorManager", "OnDeliverProduct", productId);
        }).addOnFailureListener(e -> {
            //消耗失败
            Log.e(TAG, "SDK消耗失败 (Consume Failed): " + e.getErrorCode() + ", Msg: " + e.getMessage());
        });
    }

    /**
     * 查询已购买未消耗的列表
     */
    public void obtainOwnedPurchases() {
        // 查询已购买未消耗的列表
        OwnedPurchasesReq ownedPurchasesReq = new OwnedPurchasesReq();
        //传入上一次查询得到的continueToken，获取新的数据，第一次传空
        ownedPurchasesReq.setProductType(0);
        ownedPurchasesReq.setContinuationToken(mContinueToken);
        GCJointSdk.obtainOwnedPurchases(ownedPurchasesReq).addOnSuccessListener(ownedPurchasesResult -> {
            if(ownedPurchasesResult == null) return;
            //ContinueToken用于获取下一个列表的数据，第一次为空，如果有更多数据ContinueToken有值，为空则没有更多数据
            mContinueToken = ownedPurchasesResult.getContinueToken();
            if(mContinueToken != null && !nextToken.isEmpty()){
                Log.d(TAG, "还有更多订单，继续查询下一页...");
                obtainOwnedPurchases(); // 递归调用自己
            }
            dealPurchasesResult(ownedPurchasesResult);
        }).addOnFailureListener(e -> {
            //   e.errorCode 对应 OrderStatusCode的值
            Log.e(TAG, String.format("obtainOwnedPurchases %d %s", e.errorCode, e.message));
        });
    }

    private void dealPurchasesResult(OwnedPurchasesResult ownedPurchasesResult) {
        Log.i(TAG, ownedPurchasesResult.toString());
        // purchaseList 和 sigList 一一对应
        List<String> sigList = ownedPurchasesResult.getSigList();
        List<String> purchaseList = ownedPurchasesResult.getPurchaseList();
        //签名算法
        String sigAlgorithm = ownedPurchasesResult.getSigAlgorithm();
        //公钥验签
        if ("RSA_V2".equals(sigAlgorithm)) {
            try {
                PublicKey publicKey = RSAUtil.getPublicKey(IAP_PUBLIC_KEY);
                Log.i(TAG, " publicKey :" + publicKey);
                for (int i = 0; i < purchaseList.size(); i++) {
                    try {
                        String PurchaseProductInfoStr = purchaseList.get(i);
                        boolean verify = RSAUtil.verify(PurchaseProductInfoStr, publicKey, sigList.get(i));
                        Log.i(TAG, " PurchaseProductInfoStr verify " + verify + "  , " + PurchaseProductInfoStr);
                        if(verify){
                            PurchaseProductInfo info = new PurchaseProductInfo(json, sigList.get(i));
                            if(info.getPurchaseState() == 0){
                                Log.i(TAG, "正在补单: " + info.getProductId());
                                consume(info);
                            }
                        }
                    } catch (JSONException e) {
                        Log.e(TAG, "补单解析 JSON 失败: " + e.getMessage());
                    }
                }
            } catch (Exception e) {
                Log.e(TAG, "dealPurchasesResult error: " + e.getMessage());
            }
        }
    }

    /**
     * 显示重试对话框
     */
    private void showRetryDialog(String message) {
        if (isFinishing()) return; // 防止Activity已销毁导致崩溃

        new AlertDialog.Builder(this)
                .setTitle("提示")
                .setMessage(message)
                .setCancelable(false) // 禁止点击外部关闭，强制用户选
                .setPositiveButton("重试登录", new DialogInterface.OnClickListener() {
                    @Override
                    public void onClick(DialogInterface dialog, int which) {
                        // 用户点击重试，再次调用初始化
                        init();
                    }
                })
                .setNegativeButton("退出游戏", new DialogInterface.OnClickListener() {
                    @Override
                    public void onClick(DialogInterface dialog, int which) {
                        // 用户不想玩了，退出
                        System.exit(0);
                    }
                })
                .show();
    }

    /**
     * 显示强制退出对话框 (防沉迷用)
     */
    private void showExitDialog(String message, boolean killProcess) {
        if (isFinishing()) return;

        new AlertDialog.Builder(this)
                .setTitle("提示")
                .setMessage(message)
                .setCancelable(false)
                .setPositiveButton("确定", new DialogInterface.OnClickListener() {
                    @Override
                    public void onClick(DialogInterface dialog, int which) {
                        if (killProcess) {
                            finish();
                            System.exit(0);
                        }
                    }
                })
                .show();
    }
}
