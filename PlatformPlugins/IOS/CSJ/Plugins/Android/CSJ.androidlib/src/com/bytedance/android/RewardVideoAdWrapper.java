package com.bytedance.android;

import android.app.Activity;

import com.bytedance.sdk.openadsdk.TTAdConstant;
import com.bytedance.sdk.openadsdk.TTAdInteractionListener;
import com.bytedance.sdk.openadsdk.TTAppDownloadListener;
import com.bytedance.sdk.openadsdk.TTRewardVideoAd;
import com.bytedance.sdk.openadsdk.mediation.manager.MediationRewardManager;

import java.util.HashMap;
import java.util.Map;

/**
 * created by jijiachun on 2023/5/9
 */
public class RewardVideoAdWrapper implements TTRewardVideoAd {

    private TTRewardVideoAd realAdObject;

    public RewardVideoAdWrapper(TTRewardVideoAd object) {
        this.realAdObject = object;
    }

    @Override
    public void setRewardAdInteractionListener(RewardAdInteractionListener rewardAdInteractionListener) {
        if (realAdObject != null) {
            realAdObject.setRewardAdInteractionListener(rewardAdInteractionListener);
        }
    }

    @Override
    public void setDownloadListener(TTAppDownloadListener ttAppDownloadListener) {
        if (realAdObject != null) {
            realAdObject.setDownloadListener(ttAppDownloadListener);
        }
    }

    @Override
    public int getInteractionType() {
        if (realAdObject != null) {
            return realAdObject.getInteractionType();
        }
        return 0;
    }

    @Override
    public void showRewardVideoAd(Activity activity) {
        if (realAdObject != null) {
            realAdObject.showRewardVideoAd(activity);
        }
    }

    @Override
    public Map<String, Object> getMediaExtraInfo() {
        if (realAdObject != null) {
            return realAdObject.getMediaExtraInfo();
        }
        return new HashMap<>();
    }

    @Override
    public void showRewardVideoAd(Activity activity, TTAdConstant.RitScenes ritScenes, String s) {
        if (realAdObject != null) {
            realAdObject.showRewardVideoAd(activity, ritScenes, s);
        }
    }

    @Override
    public int getRewardVideoAdType() {
        if (realAdObject != null) {
            realAdObject.getRewardVideoAdType();
        }
        return 0;
    }

    @Override
    public long getExpirationTimestamp() {
        if (realAdObject != null) {
            realAdObject.getExpirationTimestamp();
        }
        return 0;
    }

    @Override
    public void win(Double aDouble) {
        if (realAdObject != null) {
            realAdObject.win(aDouble);
        }
    }

    @Override
    public void loss(Double aDouble, String s, String s1) {
        if (realAdObject != null) {
            realAdObject.loss(aDouble, s, s1);
        }
    }

    @Override
    public void setPrice(Double aDouble) {
        if (realAdObject != null) {
            realAdObject.setPrice(aDouble);
        }
    }

    @Override
    public void setAdInteractionListener(TTAdInteractionListener ttAdInteractionListener) {
        if (realAdObject != null && ttAdInteractionListener != null) {
            realAdObject.setAdInteractionListener(ttAdInteractionListener);
        }
    }

    @Override
    public MediationRewardManager getMediationManager() {
        if (realAdObject != null) {
            return realAdObject.getMediationManager();
        }
        return null;
    }
}
