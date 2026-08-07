package com.bytedance.android;

import com.bytedance.sdk.openadsdk.mediation.init.IMediationPrivacyConfig;

import java.util.List;

public class MediationPrivacyConfig implements IMediationPrivacyConfig {

    private List<String> customAppList;
    private List<String> customDevImeis;
    private boolean canUseOaid = true;
    private boolean limitPersonalAds = false;
    private boolean programmaticRecommend = true;

    public MediationPrivacyConfig() {
    }

    public void setCustomAppList(List<String> customAppList) {
        this.customAppList = customAppList;
    }

    public void setCustomDevImeis(List<String> customDevImeis) {
        this.customDevImeis = customDevImeis;
    }

    public void setCanUseOaid(boolean canUseOaid) {
        this.canUseOaid = canUseOaid;
    }

    public void setLimitPersonalAds(boolean limitPersonalAds) {
        this.limitPersonalAds = limitPersonalAds;
    }

    public void setProgrammaticRecommend(boolean programmaticRecommend) {
        this.programmaticRecommend = programmaticRecommend;
    }

    public List<String> getCustomAppList() {
        return customAppList;
    }

    public List<String> getCustomDevImeis() {
        return customDevImeis;
    }

    public boolean isCanUseOaid() {
        return canUseOaid;
    }

    public boolean isLimitPersonalAds() {
        return limitPersonalAds;
    }

    public boolean isProgrammaticRecommend() {
        return programmaticRecommend;
    }
}
