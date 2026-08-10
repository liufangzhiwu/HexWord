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

static const char* AutonomousStringCopy_ExpressFullscreen(const char* string)
{
    if (string == NULL) {
        return NULL;
    }
    
    char* res = (char*)malloc(strlen(string) + 1);
    strcpy(res, string);
    return res;
}

// Load Status
typedef void(*FullScreenVideoAd_OnError)(int code, const char* message, int context);
typedef void(*FullScreenVideoAd_OnFullScreenVideoAdLoad)(void* fullScreenVideoAd, int context);
typedef void(*FullScreenVideoAd_OnFullScreenVideoCached)(int context);

// InteractionListener callbacks.
typedef void(*FullScreenVideoAd_OnAdShow)(int context);
typedef void(*FullScreenVideoAd_OnAdVideoBarClick)(int context);
typedef void(*FullScreenVideoAd_OnAdVideoClickSkip)(int context);
typedef void(*FullScreenVideoAd_OnAdClose)(int context);
typedef void(*FullScreenVideoAd_OnVideoComplete)(int context);
typedef void(*FullScreenVideoAd_OnVideoError)(int context);

//
@interface BUBridgeFullscreenVideoAd : NSObject
@end

@interface BUBridgeFullscreenVideoAd () <BUNativeExpressFullscreenVideoAdDelegate,BUAdObjectProtocol>
@property (nonatomic, strong) BUNativeExpressFullscreenVideoAd *fullScreenVideoAd;

@property (nonatomic, assign) int loadContext;
@property (nonatomic, assign) FullScreenVideoAd_OnError onError;
@property (nonatomic, assign) FullScreenVideoAd_OnFullScreenVideoAdLoad onFullScreenVideoAdLoad;
@property (nonatomic, assign) FullScreenVideoAd_OnFullScreenVideoCached onFullScreenVideoCached;

@property (nonatomic, assign) int interactionContext;
@property (nonatomic, assign) FullScreenVideoAd_OnAdShow onAdShow;
@property (nonatomic, assign) FullScreenVideoAd_OnAdVideoBarClick onAdVideoBarClick;
@property (nonatomic, assign) FullScreenVideoAd_OnAdVideoClickSkip onAdVideoClickSkip;
@property (nonatomic, assign) FullScreenVideoAd_OnAdClose onAdClose;
@property (nonatomic, assign) FullScreenVideoAd_OnVideoComplete onVideoComplete;
@property (nonatomic, assign) FullScreenVideoAd_OnVideoError onVideoError;

@end

@implementation BUBridgeFullscreenVideoAd

#pragma mark - BUFullscreenVideoAdDelegate
- (void)nativeExpressFullscreenVideoAdDidLoad:(BUNativeExpressFullscreenVideoAd *)fullscreenVideoAd {
    self.onFullScreenVideoAdLoad((__bridge void*)self, self.loadContext);
}

- (void)nativeExpressFullscreenVideoAd:(BUNativeExpressFullscreenVideoAd *)fullscreenVideoAd didFailWithError:(NSError *_Nullable)error {
    self.onError((int)error.code, AutonomousStringCopy_ExpressFullscreen([[error localizedDescription] UTF8String]), self.loadContext);
}

- (void)nativeExpressFullscreenVideoAdViewRenderSuccess:(BUNativeExpressFullscreenVideoAd *)rewardedVideoAd {
}

- (void)nativeExpressFullscreenVideoAdViewRenderFail:(BUNativeExpressFullscreenVideoAd *)rewardedVideoAd error:(NSError *_Nullable)error {
    self.onError((int)error.code, AutonomousStringCopy_ExpressFullscreen([[error localizedDescription] UTF8String]), self.loadContext);
}

- (void)nativeExpressFullscreenVideoAdDidDownLoadVideo:(BUNativeExpressFullscreenVideoAd *)fullscreenVideoAd {
    self.onFullScreenVideoCached(self.loadContext);
}

- (void)nativeExpressFullscreenVideoAdWillVisible:(BUNativeExpressFullscreenVideoAd *)fullscreenVideoAd {
    if (fullscreenVideoAd.mediation == nil) {
        self.onAdShow(self.interactionContext);
    }
}

- (void)nativeExpressFullscreenVideoAdDidVisible:(BUNativeExpressFullscreenVideoAd *)fullscreenVideoAd {
    if (fullscreenVideoAd.mediation != nil) {
        self.onAdShow(self.interactionContext);
    }
}

- (void)nativeExpressFullscreenVideoAdDidClick:(BUNativeExpressFullscreenVideoAd *)fullscreenVideoAd {
    self.onAdVideoBarClick(self.interactionContext);
}

- (void)nativeExpressFullscreenVideoAdDidClickSkip:(BUNativeExpressFullscreenVideoAd *)fullscreenVideoAd {
    self.onAdVideoClickSkip(self.interactionContext);
}

- (void)nativeExpressFullscreenVideoAdWillClose:(BUNativeExpressFullscreenVideoAd *)fullscreenVideoAd {
}

- (void)nativeExpressFullscreenVideoAdDidClose:(BUNativeExpressFullscreenVideoAd *)fullscreenVideoAd {
    self.onAdClose(self.interactionContext);
}

- (void)nativeExpressFullscreenVideoAdDidPlayFinish:(BUNativeExpressFullscreenVideoAd *)fullscreenVideoAd didFailWithError:(NSError *_Nullable)error {
    if (error) {
        self.onVideoError(self.interactionContext);
    } else {
        self.onVideoComplete(self.interactionContext);
    }
}
- (id<BUAdClientBiddingProtocol>)adObject {
    return self.fullScreenVideoAd;
}
@end

#if defined (__cplusplus)
extern "C" {
#endif

void UnionPlatform_FullScreenVideoAd_Load(
    AdSlotStruct *adSlot,
    FullScreenVideoAd_OnError onError,
    FullScreenVideoAd_OnFullScreenVideoAdLoad onFullScreenVideoAdLoad,
    FullScreenVideoAd_OnFullScreenVideoCached onFullScreenVideoCached,
    int context) {

    BUAdSlot *slot1 = [[BUAdSlot alloc] init];
    slot1.ID = [[NSString alloc] initWithCString:adSlot->slotId encoding:NSUTF8StringEncoding];
    if (adSlot->viewWidth > 0 && adSlot->viewHeight > 0) {
        CGSize adSize = CGSizeMake(adSlot->viewWidth, adSlot->viewHeight);
        slot1.adSize = adSize;
    }
    slot1.mediation.bidNotify = adSlot->m_bidNotify;
    slot1.mediation.scenarioID = [[NSString alloc] initWithCString:adSlot->m_cenarioId encoding:NSUTF8StringEncoding];
    slot1.mediation.mutedIfCan = adSlot->m_isMuted;
    BUNativeExpressFullscreenVideoAd* fullScreenVideoAd = [[BUNativeExpressFullscreenVideoAd alloc] initWithSlot:slot1];
    if (adSlot->m_expectedHorizontal == 1) {
        [fullScreenVideoAd.mediation addParam:@(1) withKey:BUMAdLoadingParamFVShowDirection];
    }
    if (strlen(adSlot->autoPlayPolicy) > 0) {
        NSString *str = [[NSString alloc] initWithCString:adSlot->autoPlayPolicy encoding:NSUTF8StringEncoding];
        NSNumber *num = @(str.integerValue);
        [fullScreenVideoAd.mediation addParam:num withKey:BUMAdAddParamKeyAutoPlayPolicy];
    }
    
    BUBridgeFullscreenVideoAd* instance = [[BUBridgeFullscreenVideoAd alloc] init];
    instance.fullScreenVideoAd = fullScreenVideoAd;
    instance.onError = onError;
    instance.onFullScreenVideoAdLoad = onFullScreenVideoAdLoad;
    instance.onFullScreenVideoCached = onFullScreenVideoCached;
    instance.loadContext = context;
    fullScreenVideoAd.delegate = instance;
    [fullScreenVideoAd loadAdData];
    
    NSLog(@"CSJM_Unity  插全屏 设置 adSlot，其中 %@, %@, %@,  %@, %@",
          [NSString stringWithFormat:@"slotId-%@ ，",slot1.ID],
          [NSString stringWithFormat:@"bidNotify-%d ，",slot1.mediation.bidNotify],
          [NSString stringWithFormat:@"scenarioID-%@ ，",slot1.mediation.scenarioID],
          [NSString stringWithFormat:@"mutedIfCan-%d ，",slot1.mediation.mutedIfCan],
          [NSString stringWithFormat:@"adSize-%d-%d ，",adSlot->viewWidth, adSlot->viewHeight]
          );

    (__bridge_retained void*)instance;
}

void UnionPlatform_FullScreenVideoAd_SetInteractionListener(
    void* fullScreenVideoAdPtr,
    FullScreenVideoAd_OnAdShow onAdShow,
    FullScreenVideoAd_OnAdVideoBarClick onAdVideoBarClick,
    FullScreenVideoAd_OnAdVideoClickSkip onAdVideoClickSkip,
    FullScreenVideoAd_OnAdClose onAdClose,
    FullScreenVideoAd_OnVideoComplete onVideoComplete,
    FullScreenVideoAd_OnVideoError onVideoError,
    int context) {
    BUBridgeFullscreenVideoAd* fullScreenVideoAd = (__bridge BUBridgeFullscreenVideoAd*)fullScreenVideoAdPtr;
    fullScreenVideoAd.onAdShow = onAdShow;
    fullScreenVideoAd.onAdVideoBarClick = onAdVideoBarClick;
    fullScreenVideoAd.onAdVideoClickSkip = onAdVideoClickSkip;
    fullScreenVideoAd.onAdClose = onAdClose;
    fullScreenVideoAd.onVideoComplete = onVideoComplete;
    fullScreenVideoAd.onVideoError = onVideoError;
    fullScreenVideoAd.interactionContext = context;
}

void UnionPlatform_FullScreenVideoAd_ShowFullScreenVideoAd(void* fullscreenVideoAdPtr) {
    BUBridgeFullscreenVideoAd* fullscreenVideoAd = (__bridge BUBridgeFullscreenVideoAd*)fullscreenVideoAdPtr;
    [fullscreenVideoAd.fullScreenVideoAd showAdFromRootViewController:GetAppController().rootViewController];}

void UnionPlatform_FullScreenVideoAd_Dispose(void* fullscreenVideoAdPtr) {
    (__bridge_transfer BUBridgeFullscreenVideoAd*)fullscreenVideoAdPtr;
}

bool UnionPlatform_FullScreenVideoAd_MaterialMetaIsFromPreload(void* fullscreenAd) {
    BUBridgeFullscreenVideoAd* fullScreenVideoAd = (__bridge BUBridgeFullscreenVideoAd*)fullscreenAd;
    BOOL preload = [fullScreenVideoAd.fullScreenVideoAd materialMetaIsFromPreload];
    return preload == YES;
}

long UnionPlatform_FullScreenVideoAd_ExpireTime(void * fullscreenAd) {
    BUBridgeFullscreenVideoAd* fullScreenVideoAd = (__bridge BUBridgeFullscreenVideoAd*)fullscreenAd;
    return [fullScreenVideoAd.fullScreenVideoAd getExpireTimestamp];
}

bool UnionPlatform_FullScreenVideoAd_HaveMediationManager(void * fullscreenAd) {
    BUBridgeFullscreenVideoAd* fullScreenVideoAd = (__bridge BUBridgeFullscreenVideoAd*)fullscreenAd;
    return fullScreenVideoAd.fullScreenVideoAd.mediation ? true : false;
}

bool UnionPlatform_FullScreenMediation_isReady(void * fullscreenAd) {
    BUBridgeFullscreenVideoAd* fullScreenVideoAd = (__bridge BUBridgeFullscreenVideoAd*)fullscreenAd;
    if (fullScreenVideoAd.fullScreenVideoAd.mediation) {
        return fullScreenVideoAd.fullScreenVideoAd.mediation.isReady;
    } else {
        return false;
    }
}

const char* UnionPlatform_FullScreenMediation_GetShowEcpmInfo(void * fullscreenAd) {
    BUBridgeFullscreenVideoAd* fullScreenVideoAd = (__bridge BUBridgeFullscreenVideoAd*)fullscreenAd;
    BUMRitInfo *info = fullScreenVideoAd.fullScreenVideoAd.mediation.getShowEcpmInfo;
    if (info) {
        NSString *infoJson = [BUMRitInfo toJson:info];
        return AutonomousStringCopy_ExpressFullscreen([infoJson UTF8String]);
    } else {
        return NULL;
    }
}

const char* UnionPlatform_FullScreenMediation_GetCurrentBestEcpmInfo(void * fullscreenAd) {
    BUBridgeFullscreenVideoAd* fullScreenVideoAd = (__bridge BUBridgeFullscreenVideoAd*)fullscreenAd;
    BUMRitInfo *info = fullScreenVideoAd.fullScreenVideoAd.mediation.getCurrentBestEcpmInfo;
    if (info) {
        NSString *infoJson = [BUMRitInfo toJson:info];
        return AutonomousStringCopy_ExpressFullscreen([infoJson UTF8String]);
    } else {
        return NULL;
    }
}

const char* UnionPlatform_FullScreenMediation_MultiBiddingEcpmInfos(void * fullscreenAd) {
    BUBridgeFullscreenVideoAd* fullScreenVideoAd = (__bridge BUBridgeFullscreenVideoAd*)fullscreenAd;
    NSArray<BUMRitInfo *> * infos = fullScreenVideoAd.fullScreenVideoAd.mediation.multiBiddingEcpmInfos;
    if (infos && infos.count > 0) {
        NSMutableArray *muArr = [[NSMutableArray alloc]initWithCapacity:infos.count];
        for (BUMRitInfo *info in infos) {
            NSString *infoJson = [BUMRitInfo toJson:info];
            [muArr addObject:infoJson];
        }
        NSData *jsonData = [NSJSONSerialization dataWithJSONObject:muArr options:NSJSONWritingPrettyPrinted error:nil];
        NSString *strJson = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        return AutonomousStringCopy_ExpressFullscreen([strJson UTF8String]);
    } else {
        return NULL;
    }
}

const char* UnionPlatform_FullScreenMediation_CacheRitList(void * fullscreenAd) {
    BUBridgeFullscreenVideoAd* fullScreenVideoAd = (__bridge BUBridgeFullscreenVideoAd*)fullscreenAd;
    NSArray<BUMRitInfo *> * infos = fullScreenVideoAd.fullScreenVideoAd.mediation.cacheRitList;
    if (infos && infos.count > 0) {
        NSMutableArray *muArr = [[NSMutableArray alloc]initWithCapacity:infos.count];
        for (BUMRitInfo *info in infos) {
            NSString *infoJson = [BUMRitInfo toJson:info];
            [muArr addObject:infoJson];
        }
        NSData *jsonData = [NSJSONSerialization dataWithJSONObject:muArr options:NSJSONWritingPrettyPrinted error:nil];
        NSString *strJson = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        return AutonomousStringCopy_ExpressFullscreen([strJson UTF8String]);
    } else {
        return NULL;
    }
}

const char* UnionPlatform_FullScreenMediation_GetAdLoadInfoList(void * fullscreenAd) {
    BUBridgeFullscreenVideoAd* fullScreenVideoAd = (__bridge BUBridgeFullscreenVideoAd*)fullscreenAd;
    NSArray<BUMAdLoadInfo *> * infos = fullScreenVideoAd.fullScreenVideoAd.mediation.getAdLoadInfoList;
    if (infos && infos.count > 0) {
        NSMutableArray *muArr = [[NSMutableArray alloc]initWithCapacity:infos.count];
        for (BUMAdLoadInfo *info in infos) {
            NSString *infoJson = [BUMAdLoadInfo toJson:info];
            [muArr addObject:infoJson];
        }
        NSData *jsonData = [NSJSONSerialization dataWithJSONObject:muArr options:NSJSONWritingPrettyPrinted error:nil];
        NSString *strJson = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        return AutonomousStringCopy_ExpressFullscreen([strJson UTF8String]);
    } else {
        return NULL;
    }
}


#if defined (__cplusplus)
}
#endif
