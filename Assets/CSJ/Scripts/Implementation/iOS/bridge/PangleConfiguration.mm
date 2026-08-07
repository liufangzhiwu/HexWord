//------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
//------------------------------------------------------------------------------

#import <BUAdSDK/BUAdSDK.h>
#import "BUDPrivacyProvider.h"
#import "AdSlot.h"

#if defined (__cplusplus)
extern "C" {
#endif

void UnionPlatform_setTerritory(int territory) {
    
}

int UnionPlatform_territory() {
    return 1;
}

void UnionPlatform_setAppID(const char* appID) {
    NSString *ocString = [[NSString alloc] initWithUTF8String:appID?:""];
    if (ocString.length) {
        [BUAdSDKConfiguration configuration].appID = ocString;
    }
    NSLog(@"CSJM_Unity  %@ %@",@"设置appid", ocString);
}

const void UnionPlatform_setPrivacyProvider(
    bool canUseLocation,
    double latitude,
    double longitude,
    const char* motion_info,
    const char* bum_advertiser_tracking_enabled,
    const char* bum_loc_time,
    const char* bum_limit_personal_cpus,
    const char* bum_disable_use_phone_status,
    const char* bum_custom_idfv,
    const char* bum_forbidden_idfv,
    const char* forbidden_idfa){
    BUDPrivacyProvider *pr = [[BUDPrivacyProvider alloc]init];
    pr.canUseLocation = canUseLocation;
    pr.longitude = isnan(longitude) ? 0 : longitude;
    pr.latitude = isnan(latitude) ? 0 : latitude;
    NSMutableDictionary *privacyConfig = [[NSMutableDictionary alloc]init];
    if (motion_info) {
        [privacyConfig setValue:[[NSString alloc] initWithUTF8String:motion_info]
                         forKey:@"motion_info"];
    }
    if (bum_advertiser_tracking_enabled) {
        [privacyConfig setValue:[[NSString alloc] initWithUTF8String:bum_advertiser_tracking_enabled]
                         forKey:@"bum_advertiser_tracking_enabled"];
    }
    if (bum_loc_time) {
        [privacyConfig setValue:[[NSString alloc] initWithUTF8String:bum_loc_time]
                         forKey:@"bum_loc_time"];
    }
    if (bum_limit_personal_cpus) {
        [privacyConfig setValue:[[NSString alloc] initWithUTF8String:bum_limit_personal_cpus]
                         forKey:@"bum_limit_personal_cpus"];
    }
    if (bum_disable_use_phone_status) {
        [privacyConfig setValue:[[NSString alloc] initWithUTF8String:bum_disable_use_phone_status]
                         forKey:@"bum_disable_use_phone_status"];
    }
    if (bum_custom_idfv) {
        [privacyConfig setValue:[[NSString alloc] initWithUTF8String:bum_custom_idfv]
                         forKey:@"bum_custom_idfv"];
    }
    if (bum_forbidden_idfv) {
        [privacyConfig setValue:[[NSString alloc] initWithUTF8String:bum_forbidden_idfv]
                         forKey:@"bum_forbidden_idfv"];
    }
    if (forbidden_idfa) {
        [privacyConfig setValue:[[NSString alloc] initWithUTF8String:forbidden_idfa]
                         forKey:@"forbidden_idfa"];
    }
    if (privacyConfig.count > 0) {
        pr.privacyConfig = privacyConfig;
    }
    [BUAdSDKConfiguration configuration].privacyProvider = pr;
    NSLog(@"CSJM_Unity  %@ canUseLocation %d canUseLocation %f longitude%f privacyConfigDict%@",@"设置privacyProvider", canUseLocation, latitude, longitude, privacyConfig);
};

const char* UnionPlatform_appID() {
    return (char*)[[BUAdSDKConfiguration configuration].appID?:@"" cStringUsingEncoding:NSUTF8StringEncoding];
}

void UnionPlatform_setUseMediation(bool useMediation) {
    [BUAdSDKConfiguration configuration].useMediation = useMediation;
    NSLog(@"CSJM_Unity  %@ %d",@"设置useMediation", useMediation);
}

bool UnionPlatform_useMediation() {
    return [BUAdSDKConfiguration configuration].useMediation;
}

void UnionPlatform_setLogLevel(int logLevel) {
    
}

void UnionPlatform_setdebugLogebugLog(bool debugLog) {
    [BUAdSDKConfiguration configuration].debugLog = debugLog ? @(1) : @(0);
    NSLog(@"CSJM_Unity  %@ %d",@"设置debugLog", debugLog);
}

bool UnionPlatform_debugLog() {
    return [BUAdSDKConfiguration configuration].debugLog.boolValue;
}


int UnionPlatform_logLevel() {
    return 0;
}

void UnionPlatform_setCoppa(int coppa) {
    
}

int UnionPlatform_coppa() {
    return 0;
}

void UnionPlatform_setUserExtData(const char* userExtData) {
    NSString *ocString = [[NSString alloc] initWithUTF8String:userExtData?:""];
    if (ocString.length) {
        [BUAdSDKConfiguration configuration].userExtData = ocString;
    }
    NSLog(@"CSJM_Unity  %@ %@",@"设置userExtData", ocString);
}

const char* UnionPlatform_userExtData() {
    return (char*)[[BUAdSDKConfiguration configuration].userExtData?:@"" cStringUsingEncoding:NSUTF8StringEncoding];
}

void UnionPlatform_setWebViewOfflineType(int webViewOfflineType) {
    [BUAdSDKConfiguration configuration].webViewOfflineType = (BUOfflineType)webViewOfflineType;
}

BUOfflineType UnionPlatform_webViewOfflineType() {
    return [BUAdSDKConfiguration configuration].webViewOfflineType;
}

void UnionPlatform_setGDPR(int GDPR) {
    
}

int UnionPlatform_GDPR() {
    return 0;
}

void UnionPlatform_setCCPA(int CCPA) {
    
}

int UnionPlatform_CCPA() {
    return -1;
}

void UnionPlatform_setThemeStatus(int themeStatus) {
    [BUAdSDKConfiguration configuration].themeStatus = @(themeStatus);
    NSLog(@"CSJM_Unity  %@ %d",@"设置themeStatus", themeStatus);
}

int UnionPlatform_themeStatus() {
    return [BUAdSDKConfiguration configuration].themeStatus.intValue;
}

void UnionPlatform_setAgeGroup(int ageGroup) {
    switch (ageGroup) {
        case 0:
            [BUAdSDKConfiguration configuration].ageGroup = BUAdSDKAgeGroupAdult;
            break;
        case 1:
            [BUAdSDKConfiguration configuration].ageGroup = BUAdSDKAgeGroupTeenager;
            break;
        case 2:
            [BUAdSDKConfiguration configuration].ageGroup = BUAdSDKAgeGroupMinor;
            break;
        default:
            break;
    }
    NSLog(@"CSJM_Unity  %@ %ld",@"ageGroup", (long)[BUAdSDKConfiguration configuration].ageGroup);
}

int UnionPlatform_ageGroup() {
    switch ([BUAdSDKConfiguration configuration].ageGroup) {
        case BUAdSDKAgeGroupAdult:
            return 0;
            break;
        case BUAdSDKAgeGroupTeenager:
            return 1;
            break;
        case BUAdSDKAgeGroupMinor:
            return 2;
            break;
        default:
            return 0;
            break;
    }
}

void UnionPlatform_setAbvids(int abvids[],int length) {
    NSMutableArray *mutableArr = [[NSMutableArray alloc]initWithCapacity:length];
    for (int i = 0; i < length; i++) {
        [mutableArr addObject:@(abvids[i])];
    }
    [BUAdSDKConfiguration configuration].abvids = mutableArr.copy;
}

void UnionPlatform_setAbSDKVersion(const char* abSDKVersion) {
    NSString *ocString = [[NSString alloc] initWithUTF8String:abSDKVersion?:""];
    if (ocString.length) {
        [BUAdSDKConfiguration configuration].abSDKVersion = ocString;
    }
}

const char* UnionPlatform_abSDKVersion() {
    return (char*)[[BUAdSDKConfiguration configuration].abSDKVersion?:@"" cStringUsingEncoding:NSUTF8StringEncoding];
}

void UnionPlatform_setCustomIdfa(const char* customIdfa) {
    NSString *ocString = [[NSString alloc] initWithUTF8String:customIdfa?:""];
    if (ocString.length) {
        [BUAdSDKConfiguration configuration].customIdfa = ocString;
    }
    NSLog(@"CSJM_Unity  %@ %@",@"设置customIdfa", ocString);
}

const char* UnionPlatform_customIdfa() {
    return (char*)[[BUAdSDKConfiguration configuration].customIdfa?:@"" cStringUsingEncoding:NSUTF8StringEncoding];
}

void UnionPlatform_setAllowModifyAudioSessionSetting(bool allowModifyAudioSessionSetting) {
    [BUAdSDKConfiguration configuration].allowModifyAudioSessionSetting = allowModifyAudioSessionSetting;
}

bool UnionPlatform_allowModifyAudioSessionSetting() {
    return [BUAdSDKConfiguration configuration].allowModifyAudioSessionSetting;
}

bool UnionPlatform_UnityDeveloper() {
    return [BUAdSDKConfiguration configuration].unityDeveloper;
}

void UnionPlatform_SetUnityDeveloper(bool unityDeveloper) {
    [BUAdSDKConfiguration configuration].unityDeveloper = (unityDeveloper == true);
}

bool UnionPlatform_limitPersonalAds() {
    return [BUAdSDKConfiguration configuration].mediation.limitPersonalAds.boolValue;
}

void UnionPlatform_setLimitPersonalAds(bool limitPersonalAds) {
    [BUAdSDKConfiguration configuration].mediation.limitPersonalAds = limitPersonalAds ? @(1) : @(0) ;
    NSLog(@"CSJM_Unity  %@ %d",@"设置limitPersonalAds", limitPersonalAds);
}

bool UnionPlatform_limitProgrammaticAds() {
    return [BUAdSDKConfiguration configuration].mediation.limitProgrammaticAds.boolValue;
}

void UnionPlatform_setLimitProgrammaticAds(bool limitProgrammaticAds) {
    [BUAdSDKConfiguration configuration].mediation.limitProgrammaticAds = limitProgrammaticAds ? @(1) : @(0) ;
    NSLog(@"CSJM_Unity  %@ %d",@"设置limitProgrammaticAds", limitProgrammaticAds);
}

void UnionPlatform_setPublisherDid(const char* publisherDid) {
    NSString *ocString = [[NSString alloc] initWithUTF8String:publisherDid?:""];
    if (ocString.length) {
        [BUAdSDKConfiguration configuration].mediation.extraDeviceMap = @{ @"device_id": ocString };
    }
    NSLog(@"CSJM_Unity  %@ %@",@"设置publisherDid", ocString);
}

void UnionPlatform_setSupportSplashZoomout(bool supportSplashZoomout) {
    NSLog(@"CSJM_Unity  %@ %d",@"设置supportSplashZoomout", supportSplashZoomout);
    SupportSplashZoomout = supportSplashZoomout;
}

bool UnionPlatform_SupportSplashZoomout() {
    return SupportSplashZoomout;
}

void UnionPlatform_setUserInfoForSegment(const char* user_id, const char* user_value_group, int age, const char* gender, const char* channel, const char* sub_channel, const char* customized_id) {
    BUAdSDKConfiguration *configuration = [BUAdSDKConfiguration configuration];
    // 聚合流量分组
    BUMUserInfoForSegment *segment = [[BUMUserInfoForSegment alloc] init];
    NSString *oc_user_id = [[NSString alloc] initWithUTF8String:user_id?:""];
    if (oc_user_id.length) {
        segment.user_id = oc_user_id;
    }
    NSString *oc_user_value_group = [[NSString alloc] initWithUTF8String:user_value_group?:""];
    if (oc_user_value_group.length) {
        segment.user_value_group = oc_user_value_group;
    }
    segment.age = age;
    NSString *oc_gender = [[NSString alloc] initWithUTF8String:gender?:""];
    if (oc_gender.length) {
        if ([oc_gender isEqual: @"female"]) {
            segment.gender = BUUserInfoGenderFemale;
        } else if ([oc_gender isEqual: @"male"]) {
            segment.gender = BUUserInfoGenderMale;
        } else if ([oc_gender isEqual: @"unknown"]) {
            segment.gender = BUUserInfoGenderUnknown;
        } else {
            segment.gender = BUUserInfoGenderUnSet;
        }
    }
    
    NSString *oc_channel = [[NSString alloc] initWithUTF8String:channel?:""];
    if (oc_channel.length) {
        segment.channel = oc_channel;
    }
    NSString *oc_sub_channel = [[NSString alloc] initWithUTF8String:sub_channel?:""];
    if (oc_sub_channel.length) {
        segment.sub_channel = oc_sub_channel;
    }
    NSString *oc_customized_id = [[NSString alloc] initWithUTF8String:customized_id?:""];
    if (oc_customized_id.length) {
        NSData *jsonData = [oc_customized_id dataUsingEncoding:NSUTF8StringEncoding];
        NSError *err;
        NSDictionary *dic = [NSJSONSerialization JSONObjectWithData:jsonData options:NSJSONReadingMutableContainers error:&err];
        if (dic && dic.count) {
            segment.customized_id = dic;
        }
    }
    configuration.mediation.userInfoForSegment = segment;
    NSLog(@"CSJM_Unity  %@ user_id %@ user_value_group %@ age %d gender %@ channel %@ sub_channel %@ customized_id %@ ",@"设置UserInfoForSegment", oc_user_id, oc_user_value_group, age, oc_gender, oc_channel, oc_sub_channel, oc_customized_id);
}


#if defined (__cplusplus)
}
#endif
