package com.mygame.honor;

import android.app.AlertDialog;
import android.content.DialogInterface;
import android.os.Bundle;
import android.util.Log;
import android.view.Gravity;
import android.view.View;
import android.widget.FrameLayout;

import com.hihonor.adsdk.banner.api.BannerAdLoad;
import com.hihonor.adsdk.base.api.banner.BannerAdLoadListener;
import com.hihonor.adsdk.base.api.banner.BannerExpressAd;
import com.hihonor.adsdk.base.api.interstitial.InterstitialAdLoadListener;
import com.hihonor.adsdk.base.api.interstitial.InterstitialExpressAd;
import com.hihonor.adsdk.base.api.reward.RewardAdLoadListener;
import com.hihonor.adsdk.base.api.reward.RewardExpressAd;
import com.hihonor.adsdk.base.bean.DislikeInfo;
import com.hihonor.adsdk.base.callback.AdListener;
import com.hihonor.adsdk.base.callback.DislikeItemClickListener;
import com.hihonor.adsdk.base.init.HnAdConfig;
import com.hihonor.adsdk.interstitial.InterstitialAdLoad;
import com.hihonor.adsdk.reward.RewardAdLoad;
import com.hihonor.mms.ads.ipc.bean.AdSlot;
import com.hihonor.mms.ads.ipc.bean.RewardItem;
import com.unity3d.player.UnityPlayer;
import com.unity3d.player.UnityPlayerActivity;
// 补充缺失的 Import
import com.hihonor.gamecenter.gcjointsdk.APICallback;
import com.hihonor.gamecenter.gcjointsdk.model.UserGameInfoParam;
import com.hihonor.gamecenter.gcjointsdk.sdk.AppParams;
import com.hihonor.gamecenter.gcjointsdk.sdk.GCJointSdk;
import com.hihonor.iap.framework.data.ApiException;
import com.hihonor.iap.sdk.IapClient;
import com.hihonor.iap.sdk.bean.ConsumeReq;
import com.hihonor.iap.sdk.bean.ConsumeResult;
import com.hihonor.iap.sdk.bean.OwnedPurchasesReq;
import com.hihonor.iap.sdk.bean.OwnedPurchasesResult;
import com.hihonor.iap.sdk.bean.ProductOrderIntentReq;
import com.hihonor.iap.sdk.bean.ProductType;
import com.hihonor.iap.sdk.bean.PurchaseProductInfo;
import com.hihonor.iap.sdk.bean.PurchaseResultInfo;
import com.hihonor.iap.sdk.tasks.Task;

import com.hihonor.adsdk.base.HnAds;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.HashMap;
import java.util.List;

public class MyGameActivity extends UnityPlayerActivity{

    private static final String TAG = "HonorBridge";
    private String mContinueToken;
    
    private static final String ID_BANNER = "testw6vs28auh3";       // 横幅测试ID
    private static final String ID_INTERSTITIAL = "testb4znbj8796"; // 插屏测试ID
    private static final String ID_REWARD = "testx9dtjwj8hp";       // 激励测试ID
    
    // 广告对象
    private BannerExpressAd mBannerExpressAd;
    private InterstitialExpressAd mInterstitialExpressAd;
    private RewardExpressAd mRewardExpressAd;
 
    // 我们的广告容器（用来放广告 View）
    private FrameLayout mAdContainer;
    // 定义一个成员变量，用来记录当前这次广告是否拿到了奖励
    private boolean mIsEarnedReward = false;

    @Override
    protected void onCreate(Bundle savedInstanceState){
        super.onCreate(savedInstanceState);

        Log.d(TAG, "MyGameActivity 启动了， 准备初始化荣耀 SDK");
        GCJointSdk.setApplication(this.getApplication());
        HnAds.get().initActivityLifecycle(this.getApplication());
    }

    /***
     * 广告初始化
     */
    public void adsInit(){
        HnAdConfig buildConfig = new HnAdConfig.Builder()
            .setAppId("2001482629148442624")
            .setAppKey("pk/u2UXIAL1UtRaUBL2GzH7ZN0zpkEX/W3CtaR8qWfE=")
            .setDebug(true)
            .build();
        HnAds.get().init(this.getApplication(), buildConfig);
        initAdContainer();
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
                    Log.w(TAG, "【防沉迷触发】时间已到，通知 Unity 进行下线处理");
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
                Log.i(TAG, "游戏登录成功！ ,resultMessage:" + result);
                UnityPlayer.UnitySendMessage("HonorManager", "OnLoginSuccess", result);
            }

            @Override
            public void onFailure(int code, String message) {
                // 初始化失败，获取失败code
                Log.e(TAG, "游戏登录失败！ ,resultMessage: " + code + " " + message);
                // 必须在主线程处理 UI
                runOnUiThread(() -> initFailure(code));
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
                case 1://弹框消失，用户在挽留弹框上选择了退出游戏
                case 3://未配置或者未登录，未弹出挽留弹框
                {
                    // 此处如何退出可以执行unity的退出应用生命周期？
                    // android.os.Process.killProcess(android.os.Process.myPid());//杀进程
                     UnityPlayer.UnitySendMessage("HonorManager", "OnExitControl", String.valueOf(code));
                }
                break;
                default: break;
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
            if(mContinueToken != null && !mContinueToken.isEmpty()){
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
        
        if (purchaseList == null) return;
        //签名算法
//         String sigAlgorithm = ownedPurchasesResult.getSigAlgorithm();
        //公钥验签
//         if ("RSA_V2".equals(sigAlgorithm)) {
//             try {
//                 PublicKey publicKey = RSAUtil.getPublicKey(IAP_PUBLIC_KEY);
//                 Log.i(TAG, " publicKey :" + publicKey);
                for (int i = 0; i < purchaseList.size(); i++) {
                    try {
                        String PurchaseProductInfoStr = purchaseList.get(i);
//                         String signature = (sigList != null && i < sigList.size()) ? sigList.get(i) : null;
//                         boolean verify = RSAUtil.verify(PurchaseProductInfoStr, publicKey, sigList.get(i));
//                         Log.i(TAG, " PurchaseProductInfoStr verify " + verify + "  , " + PurchaseProductInfoStr);
//                         if(verify){
                            JSONObject json = new JSONObject(PurchaseProductInfoStr);
                            String productId = json.optString("productId");
                            int purchaseState = json.optInt("purchaseState", -1);
                            String purchaseToken = json.optString("purchaseToken");
                            if(purchaseState == 0){
                                Log.i(TAG, "发现漏单，正在补单: " + productId);
                                PurchaseProductInfo info = new PurchaseProductInfo();
                                info.setProductId(productId);
                                info.setPurchaseToken(purchaseToken);
                                consume(info);
                            }
//                         }
                    } catch (JSONException e) {
                        Log.e(TAG, "补单解析 JSON 失败: " + e.getMessage());
                    }
                }
//             } catch (Exception e) {
//                 Log.e(TAG, "dealPurchasesResult error: " + e.getMessage());
//             }
//         }
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
    
    public void checkLostOrders() {
            mContinueToken = null; // 重置
            obtainOwnedPurchases();
    }
    
    private void initAdContainer(){
        mAdContainer = new FrameLayout(this);
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT,
            FrameLayout.LayoutParams.WRAP_CONTENT
        );
        params.gravity = Gravity.BOTTOM | Gravity.CENTER_HORIZONTAL;
        
        addContentView(mAdContainer, params);
        mAdContainer.setVisibility(View.GONE);
    }
    // 展示banner广告
    public void showBannerAd(){
        runOnUiThread(() -> {
                   // 如果容器里有旧广告，先清理掉
                   if (mAdContainer.getChildCount() > 0) {
                       mAdContainer.removeAllViews();
                   }
                   if (mBannerExpressAd != null) {
                       mBannerExpressAd.release();
                   }
                    // Step 1: 创建广告请求参数 (AdSlot)
                    AdSlot adSlot = new AdSlot.Builder()
                            .setSlotId(ID_BANNER) 
                            .setWidth(360)  // 设置宽度 (单位 dp)，360 是标准手机宽度
                            .setHeight(60)  // 设置高度 (单位 dp)，60 是标准横幅高度
                            .build();   
                                    
                    // Step 2: 构建加载器
                    BannerAdLoad load = new BannerAdLoad.Builder()
                            .setAdSlot(adSlot)
                            .setBannerAdLoadListener(new BannerAdLoadListener() {
                                
                                // 加载失败
                                @Override
                                public void onFailed(String code, String errorMsg) {
                                    Log.e(TAG, "Banner加载失败 code: " + code + ", msg: " + errorMsg);
                                    // 通知 Unity (可选)
                                    // UnityPlayer.UnitySendMessage("HonorManager", "OnBannerLoadFailed", errorMsg);
                                }
        
                                // 加载成功
                                @Override
                                public void onLoadSuccess(BannerExpressAd bannerExpressAd) {
                                    Log.i(TAG, "Banner加载成功");
                                    mBannerExpressAd = bannerExpressAd;
                                    
                                    // 显示容器
                                    mAdContainer.setVisibility(View.VISIBLE);
        
                                    // 设置广告基本回调 (点击、关闭等)
                                    mBannerExpressAd.setAdListener(new AdListener() {
                                        @Override
                                        public void onAdClosed() {
                                            Log.i(TAG, "Banner被关闭");
                                            // 隐藏容器
                                            mAdContainer.setVisibility(View.GONE);
                                            mAdContainer.removeAllViews();
                                            if (mBannerExpressAd != null) mBannerExpressAd.release();
                                        }
                                        @Override
                                        public void onAdClicked() {
                                            Log.i(TAG, "Banner被点击");
                                        }
                                    });
        
                                    // 设置负反馈回调 (处理那个小小的 'x' 关闭按钮)
                                    mBannerExpressAd.setDislikeClickListener(new DislikeItemClickListener() {
                                        @Override
                                        public void onFeedItemClick(int i, DislikeInfo dislikeInfo, View view) {
                                            // 用户点击了不喜欢，移除广告
                                            mAdContainer.removeAllViews();
                                            mAdContainer.setVisibility(View.GONE);
                                        }
                                        @Override
                                        public void onCancel() {}
                                        @Override
                                        public void onShow() {}
                                    });
        
                                    // Step 3: 渲染广告
                                    View bannerView = mBannerExpressAd.getExpressAdView();
                                    if (bannerView != null) {
                                        mAdContainer.removeAllViews();
                                        mAdContainer.addView(bannerView);
                                    }
                                }
                            })
                            .build();
        
                    // Step 4: 开始加载
                    load.loadAd();
        });
    }
    // ==========================================
    // 供 Unity 调用：隐藏广告
    // ==========================================
    public void releaseBanner() {
        runOnUiThread(() -> {
            if (mAdContainer != null) {
                mAdContainer.removeAllViews();
                mAdContainer.setVisibility(View.GONE);
            }
            if (mBannerExpressAd != null) {
                mBannerExpressAd.release();
                mBannerExpressAd = null;
            }
        });
    }
    // 加载插屏
    public void loadInterstitialAd(){
        runOnUiThread(() -> {
            // 如果之前有没展示的，先释放
            releaseInterstitial();

            // Step 1: 创建参数 (AdSlot)
            AdSlot adSlot = new AdSlot.Builder()
                    .setSlotId(ID_INTERSTITIAL) 
                    .build();

            // Step 2: 构建加载器 (InterstitialAdLoad)
            InterstitialAdLoad load = new InterstitialAdLoad.Builder()
                    .setAdSlot(adSlot)
                    .setInterstitialAdLoadListener(new InterstitialAdLoadListener() {
                        
                        // 加载失败
                        @Override
                        public void onFailed(String code, String errorMsg) {
                            Log.e(TAG, "插屏加载失败: " + code + ", msg: " + errorMsg);
                            // 通知 Unity 加载失败
                            UnityPlayer.UnitySendMessage("HonorManager", "OnInterstitialLoadFailed", errorMsg);
                        }

                        // 加载成功
                        @Override
                        public void onAdLoaded(InterstitialExpressAd interstitialExpressAd) {
                            Log.i(TAG, "插屏加载成功，等待展示");
                            mInterstitialExpressAd = interstitialExpressAd;
                            // 通知 Unity 加载成功
                            UnityPlayer.UnitySendMessage("HonorManager", "OnInterstitialLoaded", "");
                        }
                    })
                    .build();

            // Step 3: 开始加载
            load.loadAd();
        });    
    }
    /**
     * 2. 展示插屏广告
     */
    public void showInterstitialAd() {
        runOnUiThread(() -> {
            if (mInterstitialExpressAd != null) {
                // 设置监听器 (监听广告关闭、点击等)
                mInterstitialExpressAd.setAdListener(new AdListener() {
                    @Override
                    public void onAdClosed() {
                        Log.i(TAG, "插屏广告被关闭");
                        // 释放资源
                        releaseInterstitial();
                        // 通知 Unity 恢复游戏 (因为插屏通常会暂停游戏)
                        UnityPlayer.UnitySendMessage("HonorManager", "OnInterstitialClosed", "");
                        
                        // 可选：关闭后自动加载下一条，保证下次有点
                        loadInterstitialAd();
                    }

                    @Override
                    public void onAdClicked() {
                        Log.i(TAG, "插屏广告被点击");
                    }

                    @Override
                    public void onAdImpression() {
                        Log.i(TAG, "插屏广告展示成功");
                    }
                });

                // 正式展示
                mInterstitialExpressAd.show(MyGameActivity.this);
            } else {
                Log.e(TAG, "插屏广告尚未加载完成，尝试重新加载...");
                UnityPlayer.UnitySendMessage("HonorManager", "OnInterstitialClosed", "");
                loadInterstitialAd(); // 没加载好就重新加载
            }
        });
    }    
    private void releaseInterstitial() {
         if (mInterstitialExpressAd != null) {
             mInterstitialExpressAd.release();
             mInterstitialExpressAd = null;
         }
    }   
/**
     * 1. 加载激励视频
     */
    public void loadRewardAd() {
        runOnUiThread(() -> {
            releaseReward();

            // Step 1: 创建 AdSlot
            AdSlot adSlot = new AdSlot.Builder()
                    .setSlotId(ID_REWARD)
                    .setRewardAmount(100)       // 奖励数量 (可选)
                    .setRewardName("GoldCoin")  // 奖励名称 (可选)
                    .build();

            // Step 2: 构建加载器
            RewardAdLoad load = new RewardAdLoad.Builder()
                    .setAdSlot(adSlot)
                    .setRewardAdLoadListener(new RewardAdLoadListener() {
                        @Override
                        public void onFailed(String code, String errorMsg) {
                            Log.e(TAG, "激励视频加载失败: " + errorMsg);
                            mRewardExpressAd = null;
                           // 建议把错误码传过去，Unity 可以据此决定是否重试（例如网络错误可重试，无填充则不重试）
                           UnityPlayer.UnitySendMessage("HonorManager", "OnRewardAdLoadFailed", code + ":" + errorMsg);
                        }

                        @Override
                        public void onLoadSuccess(RewardExpressAd rewardExpressAd) {
                            if (rewardExpressAd == null) {
                                Log.e(TAG, "加载成功但对象为空");
                                return;
                            }
                            Log.i(TAG, "激励视频加载成功 (Loaded)");
                            mRewardExpressAd = rewardExpressAd;
                            
                            // 设置基础监听
                            mRewardExpressAd.setAdListener(new AdListener() {
                                @Override
                                public void onAdClosed() {
                                    Log.i(TAG, "激励视频页面关闭");
                                    loadRewardAd(); // 自动预加载下一条
                                }
                                @Override
                                public void onAdClicked() {
                                    Log.i(TAG, "激励视频被点击");
                                }
                                @Override
                                public void onAdImpression() {
                                     Log.i(TAG, "广告展示曝光成功");
                                }
                            });
                            // 通知 Unity 按钮可以点击了
                            UnityPlayer.UnitySendMessage("HonorManager", "OnRewardAdLoaded", "");                  
                        }
                    }).build();

            // Step 3: 开始加载
            load.loadAd();
        });
    }    
    /**
     * 2. 展示激励视频
     */
    public void showRewardAd() {
        runOnUiThread(() -> {
            if (mRewardExpressAd != null) {
                // 🔥 每次播放前，先重置奖励状态
                mIsEarnedReward = false;
                // 展示并设置播放状态监听 (包括是否看完拿到奖励)
                mRewardExpressAd.show(MyGameActivity.this, new RewardExpressAd.RewardAdStatusListener() {
                    @Override
                    public void onRewardAdOpened() {
                        Log.i(TAG, "视频开始播放");
                    }

                    @Override
                    public void onVideoError(int errorCode) {
                        Log.e(TAG, "视频播放出错: " + errorCode);
                        UnityPlayer.UnitySendMessage("HonorManager", "OnRewardAdShowFailed", "VideoError:" + errorCode);
                    }
                    
                    @Override
                    public void onRewarded(RewardItem rewardItem) {
                        // 🔥 玩家完整看完了视频，发放奖励！
                        mIsEarnedReward = true;
                        String rewardMsg = "Amount:" + rewardItem.getAmount() + ",Type:" + rewardItem.getType();
                        Log.i(TAG, "发放奖励: " + rewardMsg);
                        
                        // 通知 Unity 发奖励
                        UnityPlayer.UnitySendMessage("HonorManager", "OnAdRewarded", rewardMsg);
                    }
                    // 🏁 广告页面关闭 (无论是中途关的，还是看完关的，都会走这里)
                    @Override
                    public void onRewardAdClosed(boolean isVideoEnd) {
                        Log.i(TAG, "视频关闭, 是否播完: " + isVideoEnd);
                        // 注意：这里是关闭回调，奖励回调在 onRewarded
                        // 🔥 关键检查：如果没有拿到奖励，说明是中途关闭/跳过
                        if (!mIsEarnedReward) {
                            Log.i(TAG, "用户未看完视频，提前关闭");
                            // 通知 Unity: 这是一个无效的观看
                            UnityPlayer.UnitySendMessage("HonorManager", "OnRewardAdShowFailed", "UserSkipped");
                        }                        
                        releaseReward();
                        loadRewardAd();
                    }                    
                });
            } else {
                Log.e(TAG, "激励视频未加载完成，尝试重新加载");
                UnityPlayer.UnitySendMessage("HonorManager", "OnRewardAdShowFailed", "NotReady");
                loadRewardAd();
            }
        });
    }    
    private void releaseReward() {
            if (mRewardExpressAd != null) {
                mRewardExpressAd.release();
                mRewardExpressAd = null;
            }
    }
    @Override
    protected void onDestroy() {
            super.onDestroy();
            releaseBanner();
            releaseInterstitial();
            releaseReward();
    }
}
