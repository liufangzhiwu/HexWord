//------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
//------------------------------------------------------------------------------

#import <BUAdSDK/BUAdSDK.h>
#import "UnityAppController.h"
#import "AdSlot.h"
#import "BUToUnityAdManager.h"

static const char* AutonomousStringCopy(const char* string);

static const char* AutonomousStringCopy(const char* string)
{
    if (string == NULL) {
        return NULL;
    }
    
    char* res = (char*)malloc(strlen(string) + 1);
    strcpy(res, string);
    return res;
}

// IRewardVideoAdListener callbacks.
typedef void(*RewardVideoAd_OnError)(int code, const char* message, int context);
typedef void(*RewardVideoAd_OnRewardVideoAdLoad)(void* rewardVideoAd, int context);
typedef void(*RewardVideoAd_OnRewardVideoCached)(int context);

// IRewardAdInteractionListener callbacks.
typedef void(*RewardVideoAd_OnAdShow)(int context);
typedef void(*RewardVideoAd_OnAdVideoBarClick)(int context);
typedef void(*RewardVideoAd_OnAdClose)(int context);
typedef void(*RewardVideoAd_OnVideoComplete)(int context);
typedef void(*RewardVideoAd_OnVideoError)(int context);
typedef void(*RewardVideoAd_OnVideoSkip)(int context);
typedef void(*RewardVideoAd_OnRewardVerify)(bool rewardVerify,
                                            int rewardAmount,
                                            const char* rewardName,
                                            int rewardType,
                                            float rewardPropose,
                                            int serverErrorCode,
                                            const char* serverErrorMsg,
                                            bool isGromoreServersideVerify,
                                            const char* gromoreExtra,
                                            const char* transId,
                                            int reason,
                                            int errCode,
                                            const char* errMsg,
                                            const char* adnName,
                                            const char* ecpm,
                                            int context);

// The BURewardedVideoAdDelegate implement.
@interface BUBridgeRewardVideoAd : NSObject
@end

@interface BUBridgeRewardVideoAd () <BUNativeExpressRewardedVideoAdDelegate,BUAdObjectProtocol>
@property (nonatomic, strong) BUNativeExpressRewardedVideoAd *expressRewardedVideoAd;

@property (nonatomic, assign) int loadContext;
@property (nonatomic, assign) RewardVideoAd_OnError onError;
@property (nonatomic, assign) RewardVideoAd_OnRewardVideoAdLoad onRewardVideoAdLoad;
@property (nonatomic, assign) RewardVideoAd_OnRewardVideoCached onRewardVideoCached;

@property (nonatomic, assign) int interactionContext;
@property (nonatomic, assign) RewardVideoAd_OnAdShow onAdShow;
@property (nonatomic, assign) RewardVideoAd_OnAdVideoBarClick onAdVideoBarClick;
@property (nonatomic, assign) RewardVideoAd_OnAdClose onAdClose;
@property (nonatomic, assign) RewardVideoAd_OnVideoComplete onVideoComplete;
@property (nonatomic, assign) RewardVideoAd_OnVideoError onVideoError;
@property (nonatomic, assign) RewardVideoAd_OnVideoSkip onVideoSkip;
@property (nonatomic, assign) RewardVideoAd_OnRewardVerify onRewardVerify;

@end

@implementation BUBridgeRewardVideoAd
#pragma mark - BUNativeExpressRewardedVideoAdDelegate
- (void)nativeExpressRewardedVideoAdDidLoad:(BUNativeExpressRewardedVideoAd *)rewardedVideoAd {
    if (self.onRewardVideoAdLoad) {
        self.onRewardVideoAdLoad((__bridge void*)self, self.loadContext);
    }
}

- (void)nativeExpressRewardedVideoAd:(BUNativeExpressRewardedVideoAd *)rewardedVideoAd didFailWithError:(NSError *_Nullable)error {
    if (self.onError) {
        self.onError((int)error.code, AutonomousStringCopy([[error localizedDescription] UTF8String]), self.loadContext);
    }
}

- (void)nativeExpressRewardedVideoAdDidDownLoadVideo:(BUNativeExpressRewardedVideoAd *)rewardedVideoAd {
    if (self.onRewardVideoCached) {
        self.onRewardVideoCached(self.loadContext);
    }
}

- (void)nativeExpressRewardedVideoAdViewRenderSuccess:(BUNativeExpressRewardedVideoAd *)rewardedVideoAd {
}

- (void)nativeExpressRewardedVideoAdViewRenderFail:(BUNativeExpressRewardedVideoAd *)rewardedVideoAd error:(NSError *_Nullable)error {
    if (self.onError) {
        self.onError((int)error.code, AutonomousStringCopy([[error localizedDescription] UTF8String]), self.loadContext);
    }
}

- (void)nativeExpressRewardedVideoAdWillVisible:(BUNativeExpressRewardedVideoAd *)rewardedVideoAd {
    if (rewardedVideoAd.mediation == nil && self.onAdShow) {
        self.onAdShow(self.interactionContext);
    }
}

- (void)nativeExpressRewardedVideoAdDidVisible:(BUNativeExpressRewardedVideoAd *)rewardedVideoAd {
    if (rewardedVideoAd.mediation != nil && self.onAdShow) {
        self.onAdShow(self.interactionContext);
    }
}

- (void)nativeExpressRewardedVideoAdWillClose:(BUNativeExpressRewardedVideoAd *)rewardedVideoAd {
}

- (void)nativeExpressRewardedVideoAdDidClose:(BUNativeExpressRewardedVideoAd *)rewardedVideoAd {
    if (self.onAdClose) {
        self.onAdClose(self.interactionContext);
    }
}

- (void)nativeExpressRewardedVideoAdDidClick:(BUNativeExpressRewardedVideoAd *)rewardedVideoAd {
    if (self.onAdVideoBarClick) {
        self.onAdVideoBarClick(self.interactionContext);
    }
}

- (void)nativeExpressRewardedVideoAdDidClickSkip:(BUNativeExpressRewardedVideoAd *)rewardedVideoAd {
    if (self.onVideoSkip) {
        self.onVideoSkip(self.interactionContext);
    }
}

- (void)nativeExpressRewardedVideoAdDidPlayFinish:(BUNativeExpressRewardedVideoAd *)rewardedVideoAd didFailWithError:(NSError *_Nullable)error {
    if (error) {
        if (self.onVideoError) {
            self.onVideoError(self.interactionContext);
        }
    } else {
        if (self.onVideoComplete) {
            self.onVideoComplete(self.interactionContext);
        }
    }
}

- (void)nativeExpressRewardedVideoAdServerRewardDidSucceed:(BUNativeExpressRewardedVideoAd *)rewardedVideoAd verify:(BOOL)verify {
    if (self.onRewardVerify) {
        NSString *rewardName = rewardedVideoAd.rewardedVideoModel.rewardName?:@"";
        BOOL isGromoreServersideVerify = rewardedVideoAd.rewardedVideoModel.mediation ? rewardedVideoAd.rewardedVideoModel.mediation.verifyByGroMoreS2S : NO;
        NSString *transId = rewardedVideoAd.rewardedVideoModel.mediation ? rewardedVideoAd.rewardedVideoModel.mediation.tradeId : nil;
        NSString *adnName = rewardedVideoAd.rewardedVideoModel.mediation ? rewardedVideoAd.rewardedVideoModel.mediation.adnName : nil;
        NSString *ecpm = rewardedVideoAd.rewardedVideoModel.mediation ? rewardedVideoAd.rewardedVideoModel.mediation.ecpm : nil;
        NSNumber *errorCodeNum = rewardedVideoAd.rewardedVideoModel.rewardExtraInfo[@"error_code"]?:@(0);
        NSString *errorMsgStr = rewardedVideoAd.rewardedVideoModel.rewardExtraInfo[@"error_msg"]?:@"";
        self.onRewardVerify(verify,
                            (int)rewardedVideoAd.rewardedVideoModel.rewardAmount,
                            (char*)[rewardName cStringUsingEncoding:NSUTF8StringEncoding],
                            (int)rewardedVideoAd.rewardedVideoModel.rewardType,
                            rewardedVideoAd.rewardedVideoModel.rewardPropose,
                            (int)(errorCodeNum.integerValue),
                            (char*)[errorMsgStr cStringUsingEncoding:NSUTF8StringEncoding],
                            isGromoreServersideVerify,
                            (char*)[@"" cStringUsingEncoding:NSUTF8StringEncoding],
                            (char*)[transId cStringUsingEncoding:NSUTF8StringEncoding],
                            0,
                            0,
                            (char*)[@"" cStringUsingEncoding:NSUTF8StringEncoding],
                            (char*)[adnName cStringUsingEncoding:NSUTF8StringEncoding],
                            (char*)[ecpm cStringUsingEncoding:NSUTF8StringEncoding],
                            self.interactionContext);
    }
    
}

- (void)nativeExpressRewardedVideoAdServerRewardDidFail:(BUNativeExpressRewardedVideoAd *)rewardedVideoAd error:(NSError *)error {
    if (self.onRewardVerify) {
        NSString *rewardName = rewardedVideoAd.rewardedVideoModel.rewardName?:@"";
        NSInteger serverErrorCode = error ? error.code : 0;
        NSString *serverErrorMsg = error ? error.userInfo[NSLocalizedDescriptionKey] : @"";
        BOOL isGromoreServersideVerify = rewardedVideoAd.rewardedVideoModel.mediation ? rewardedVideoAd.rewardedVideoModel.mediation.verifyByGroMoreS2S : NO;
        NSString *transId = rewardedVideoAd.rewardedVideoModel.mediation ? rewardedVideoAd.rewardedVideoModel.mediation.tradeId : @"";
        NSString *adnName = rewardedVideoAd.rewardedVideoModel.mediation ? rewardedVideoAd.rewardedVideoModel.mediation.adnName : @"";
        NSString *extra = rewardedVideoAd.rewardedVideoModel.extra?:@"";
        NSString *ecpm = rewardedVideoAd.rewardedVideoModel.mediation ? rewardedVideoAd.rewardedVideoModel.mediation.ecpm : @"";
        self.onRewardVerify(false,
                            (int)rewardedVideoAd.rewardedVideoModel.rewardAmount,
                            (char*)[rewardName cStringUsingEncoding:NSUTF8StringEncoding],
                            (int)rewardedVideoAd.rewardedVideoModel.rewardType,
                            rewardedVideoAd.rewardedVideoModel.rewardPropose,
                            (int)serverErrorCode,
                            (char*)[serverErrorMsg cStringUsingEncoding:NSUTF8StringEncoding],
                            isGromoreServersideVerify,
                            (char*)[extra cStringUsingEncoding:NSUTF8StringEncoding],
                            (char*)[transId cStringUsingEncoding:NSUTF8StringEncoding],
                            0,
                            (int)serverErrorCode,
                            (char*)[serverErrorMsg cStringUsingEncoding:NSUTF8StringEncoding],
                            (char*)[adnName cStringUsingEncoding:NSUTF8StringEncoding],
                            (char*)[ecpm cStringUsingEncoding:NSUTF8StringEncoding],
                            self.interactionContext);
    }
    
}



- (id<BUAdClientBiddingProtocol>)adObject {
    return self.expressRewardedVideoAd;
}

@end

#if defined (__cplusplus)
extern "C" {
#endif

void UnionPlatform_RewardVideoAd_Load(
                                             AdSlotStruct *adSlot,
                                             RewardVideoAd_OnError onError,
                                             RewardVideoAd_OnRewardVideoAdLoad onRewardVideoAdLoad,
                                             RewardVideoAd_OnRewardVideoCached onRewardVideoCached,
                                             int context) {
    BURewardedVideoModel *model = [[BURewardedVideoModel alloc] init];
    model.userId = [[NSString alloc] initWithCString:adSlot->userId encoding:NSUTF8StringEncoding];
    model.extra =  [[NSString alloc] initWithCString:adSlot->mediaExtra encoding:NSUTF8StringEncoding];
    model.rewardName =  [[NSString alloc] initWithCString:adSlot->rewardName encoding:NSUTF8StringEncoding];
    model.rewardAmount = adSlot->rewardAmount;
    
    BUAdSlot *slot1 = [[BUAdSlot alloc] init];
    slot1.ID = [[NSString alloc] initWithCString:adSlot->slotId encoding:NSUTF8StringEncoding];
    if (adSlot->viewWidth > 0 && adSlot->viewHeight > 0) {
        CGSize adSize = CGSizeMake(adSlot->viewWidth, adSlot->viewHeight);
        slot1.adSize = adSize;
    }
    slot1.mediation.bidNotify = adSlot->m_bidNotify;
    slot1.mediation.scenarioID = [[NSString alloc] initWithCString:adSlot->m_cenarioId encoding:NSUTF8StringEncoding];
    slot1.mediation.mutedIfCan = adSlot->m_isMuted;

    BUNativeExpressRewardedVideoAd* expressRewardedVideoAd = [[BUNativeExpressRewardedVideoAd alloc] initWithSlot:slot1 rewardedVideoModel:model];
    if (adSlot->m_expectedHorizontal == 1) {
        [expressRewardedVideoAd.mediation addParam:@(1) withKey:BUMAdLoadingParamRVShowDirection];
    }
    if (strlen(adSlot->twoStageInfo) > 0) {
        NSString *str = [[NSString alloc] initWithCString:adSlot->twoStageInfo encoding:NSUTF8StringEncoding];
        NSData *jsonData = [str dataUsingEncoding:NSUTF8StringEncoding];
        // 2. JSON 转 NSDictionary
        NSError *error;
        NSDictionary *dict = [NSJSONSerialization JSONObjectWithData:jsonData
                                                             options:0
                                                               error:&error];
        if (dict) {
            [expressRewardedVideoAd.mediation addParam:@[dict] withKey:BUMAdLoadingParamRVTwoStageInfoList];
            NSLog(@"CSJM_Unity  设置百度二段激励信息为 %@", dict);
        }
    }
    if (strlen(adSlot->autoPlayPolicy) > 0) {
        NSString *str = [[NSString alloc] initWithCString:adSlot->autoPlayPolicy encoding:NSUTF8StringEncoding];
        NSNumber *num = @(str.integerValue);
        [expressRewardedVideoAd.mediation addParam:num withKey:BUMAdAddParamKeyAutoPlayPolicy];
    }
    
    BUBridgeRewardVideoAd* instance = [[BUBridgeRewardVideoAd alloc] init];
    
    instance.expressRewardedVideoAd = expressRewardedVideoAd;
    
    instance.onError = onError;
    instance.onRewardVideoAdLoad = onRewardVideoAdLoad;
    instance.onRewardVideoCached = onRewardVideoCached;
    
    instance.loadContext = context;
    
    expressRewardedVideoAd.delegate = instance;
    [expressRewardedVideoAd loadAdData];
    
    NSLog(@"CSJM_Unity  激励视屏 设置 adSlot，其中 %@, %@, %@,  %@, %@, %@",
          [NSString stringWithFormat:@"slotId-%@ ，",slot1.ID],
          [NSString stringWithFormat:@"bidNotify-%d ，",slot1.mediation.bidNotify],
          [NSString stringWithFormat:@"scenarioID-%@ ，",slot1.mediation.scenarioID],
          [NSString stringWithFormat:@"mutedIfCan-%d ，",slot1.mediation.mutedIfCan],
          [NSString stringWithFormat:@"adSize-%d-%d ，",adSlot->viewWidth,adSlot->viewHeight],
          [NSString stringWithFormat:@"rewardedVideoModel userId-%@ extra-%@ rewardName-%@ rewardAmount-%ld，",model.userId, model.extra, model.rewardName, (long)model.rewardAmount]
          );
    
    (__bridge_retained void*)instance;
}

void UnionPlatform_RewardVideoAd_SetInteractionListener(void* rewardedVideoAdPtr,
                                                               RewardVideoAd_OnAdShow onAdShow,
                                                               RewardVideoAd_OnAdVideoBarClick onAdVideoBarClick,
                                                               RewardVideoAd_OnAdClose onAdClose,
                                                               RewardVideoAd_OnVideoComplete onVideoComplete,
                                                               RewardVideoAd_OnVideoError onVideoError,
                                                               RewardVideoAd_OnVideoSkip onVideoSkip,
                                                               RewardVideoAd_OnRewardVerify onRewardVerify,
                                                               int context) {
    BUBridgeRewardVideoAd* rewardedVideoAd = (__bridge BUBridgeRewardVideoAd*)rewardedVideoAdPtr;
    rewardedVideoAd.onAdShow = onAdShow;
    rewardedVideoAd.onAdVideoBarClick = onAdVideoBarClick;
    rewardedVideoAd.onAdClose = onAdClose;
    rewardedVideoAd.onVideoComplete = onVideoComplete;
    rewardedVideoAd.onVideoError = onVideoError;
    rewardedVideoAd.onVideoSkip = onVideoSkip;
    rewardedVideoAd.onRewardVerify = onRewardVerify;
    rewardedVideoAd.interactionContext = context;
}

void UnionPlatform_RewardVideoAd_ShowRewardVideoAd(void* rewardedVideoAdPtr) {
    BUBridgeRewardVideoAd* rewardedVideoAd = (__bridge BUBridgeRewardVideoAd*)rewardedVideoAdPtr;
    [rewardedVideoAd.expressRewardedVideoAd showAdFromRootViewController:GetAppController().rootViewController];
}

void UnionPlatform_RewardVideoAd_Dispose(void* rewardedVideoAdPtr) {
    (__bridge_transfer BUBridgeRewardVideoAd*)rewardedVideoAdPtr;
}

bool UnionPlatform_RewardVideoAd_MaterialMetaIsFromPreload(void* rewardedVideoAdPtr) {
    BUBridgeRewardVideoAd* rewardedVideoAd = (__bridge BUBridgeRewardVideoAd*)rewardedVideoAdPtr;
    BOOL preload = [rewardedVideoAd.expressRewardedVideoAd materialMetaIsFromPreload];
    return preload == YES;
}

long UnionPlatform_RewardVideoAd_ExpireTime(void * rewardedVideoAdPtr) {
    BUBridgeRewardVideoAd* rewardedVideoAd = (__bridge BUBridgeRewardVideoAd*)rewardedVideoAdPtr;
    return [rewardedVideoAd.expressRewardedVideoAd getExpireTimestamp];
}

bool UnionPlatform_RewardVideoAd_HaveMediationManager(void * rewardedVideoAdPtr) {
    BUBridgeRewardVideoAd* rewardedVideoAd = (__bridge BUBridgeRewardVideoAd*)rewardedVideoAdPtr;
    return rewardedVideoAd.expressRewardedVideoAd.mediation ? true : false;
}

bool UnionPlatform_RewardVideoMediation_isReady(void * rewardedVideoAdPtr) {
    BUBridgeRewardVideoAd* rewardedVideoAd = (__bridge BUBridgeRewardVideoAd*)rewardedVideoAdPtr;
    if (rewardedVideoAd.expressRewardedVideoAd.mediation) {
        return rewardedVideoAd.expressRewardedVideoAd.mediation.isReady;
    } else {
        return false;
    }
}

const char* UnionPlatform_RewardVideoMediation_GetShowEcpmInfo(void * rewardedVideoAdPtr) {
    BUBridgeRewardVideoAd* rewardedVideoAd = (__bridge BUBridgeRewardVideoAd*)rewardedVideoAdPtr;
    BUMRitInfo *info = rewardedVideoAd.expressRewardedVideoAd.mediation.getShowEcpmInfo;
    if (info) {
        NSString *infoJson = [BUMRitInfo toJson:info];
        return AutonomousStringCopy([infoJson UTF8String]);
    } else {
        return NULL;
    }
}

const char* UnionPlatform_RewardVideoMediation_GetCurrentBestEcpmInfo(void * rewardedVideoAdPtr) {
    BUBridgeRewardVideoAd* rewardedVideoAd = (__bridge BUBridgeRewardVideoAd*)rewardedVideoAdPtr;
    BUMRitInfo *info = rewardedVideoAd.expressRewardedVideoAd.mediation.getCurrentBestEcpmInfo;
    if (info) {
        NSString *infoJson = [BUMRitInfo toJson:info];
        return AutonomousStringCopy([infoJson UTF8String]);
    } else {
        return NULL;
    }
}

const char* UnionPlatform_RewardVideoMediation_MultiBiddingEcpmInfos(void * rewardedVideoAdPtr) {
    BUBridgeRewardVideoAd* rewardedVideoAd = (__bridge BUBridgeRewardVideoAd*)rewardedVideoAdPtr;
    NSArray<BUMRitInfo *> * infos = rewardedVideoAd.expressRewardedVideoAd.mediation.multiBiddingEcpmInfos;
    if (infos && infos.count > 0) {
        NSMutableArray *muArr = [[NSMutableArray alloc]initWithCapacity:infos.count];
        for (BUMRitInfo *info in infos) {
            NSString *infoJson = [BUMRitInfo toJson:info];
            [muArr addObject:infoJson];
        }
        NSData *jsonData = [NSJSONSerialization dataWithJSONObject:muArr options:NSJSONWritingPrettyPrinted error:nil];
        NSString *strJson = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        return AutonomousStringCopy([strJson UTF8String]);
    } else {
        return NULL;
    }
}

const char* UnionPlatform_RewardVideoMediation_CacheRitList(void * rewardedVideoAdPtr) {
    BUBridgeRewardVideoAd* rewardedVideoAd = (__bridge BUBridgeRewardVideoAd*)rewardedVideoAdPtr;
    NSArray<BUMRitInfo *> * infos = rewardedVideoAd.expressRewardedVideoAd.mediation.cacheRitList;
    if (infos && infos.count > 0) {
        NSMutableArray *muArr = [[NSMutableArray alloc]initWithCapacity:infos.count];
        for (BUMRitInfo *info in infos) {
            NSString *infoJson = [BUMRitInfo toJson:info];
            [muArr addObject:infoJson];
        }
        NSData *jsonData = [NSJSONSerialization dataWithJSONObject:muArr options:NSJSONWritingPrettyPrinted error:nil];
        NSString *strJson = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        return AutonomousStringCopy([strJson UTF8String]);
    } else {
        return NULL;
    }
}

const char* UnionPlatform_RewardVideoMediation_GetAdLoadInfoList(void * rewardedVideoAdPtr) {
    BUBridgeRewardVideoAd* rewardedVideoAd = (__bridge BUBridgeRewardVideoAd*)rewardedVideoAdPtr;
    NSArray<BUMAdLoadInfo *> * infos = rewardedVideoAd.expressRewardedVideoAd.mediation.getAdLoadInfoList;
    if (infos && infos.count > 0) {
        NSMutableArray *muArr = [[NSMutableArray alloc]initWithCapacity:infos.count];
        for (BUMAdLoadInfo *info in infos) {
            NSString *infoJson = [BUMAdLoadInfo toJson:info];
            [muArr addObject:infoJson];
        }
        NSData *jsonData = [NSJSONSerialization dataWithJSONObject:muArr options:NSJSONWritingPrettyPrinted error:nil];
        NSString *strJson = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        return AutonomousStringCopy([strJson UTF8String]);
    } else {
        return NULL;
    }
}
#if defined (__cplusplus)
}
#endif
