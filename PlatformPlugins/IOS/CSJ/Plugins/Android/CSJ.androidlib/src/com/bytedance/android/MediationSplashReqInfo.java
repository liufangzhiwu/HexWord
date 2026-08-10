package com.bytedance.android;

import com.bytedance.sdk.openadsdk.mediation.ad.MediationSplashRequestInfo;

/**
 * 开屏自定义兜底配置信息
 */
public class MediationSplashReqInfo extends MediationSplashRequestInfo {
    public MediationSplashReqInfo(String adnName, String adnSlotId, String appId, String appkey) {
        super(adnName, adnSlotId, appId, appkey);
    }
}
