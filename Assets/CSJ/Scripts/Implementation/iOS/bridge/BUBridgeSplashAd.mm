//
//  BUBridgeSplashAd.cpp
//  Unity-iPhone
//
//  Created by wangchao on 2020/6/8.
//

#import <BUAdSDK/BUAdSDK.h>

#import "UnityAppController.h"
#import "BUToUnityAdManager.h"
#import "AdSlot.h"

const char* AutonomousStringCopy_Splash(const char* string)
{
    if (string == NULL) {
        return NULL;
    }
    
    char* res = (char*)malloc(strlen(string) + 1);
    strcpy(res, string);
    return res;
}


// ISplashAdListener callbacks.
typedef void(*SplashAd_OnLoad)(void* splashAd, int context);
typedef void(*SplashAd_OnLoadError)(int errCode, const char* errMsg, int context);
typedef void(*SplashAd_WillShow)(int context, int type);
typedef void(*SplashAd_DidShow)(int context, int type);
typedef void(*SplashAd_DidClick)(int context, int type);
typedef void(*SplashAd_DidClose)(int contextm,int closeType);
typedef void(*SplashAd_VideoAdDidPlayFinish)(int context);
typedef void(*SplashAd_RenderSuccess)(int context);
typedef void(*SplashAd_RenderFailed)(int context);
typedef void(*SplashAd_SplashAdVCClosed)(int context);
typedef void(*SplashAd_DidCloseOtherController)(int interactionType, int context);

@interface BUBridgeSplashAd : NSObject<BUMSplashAdDelegate,BUAdObjectProtocol>

@property (nonatomic, strong) BUSplashAd *splashAd;

@property (nonatomic, assign) int loadContext;
@property (nonatomic, assign) int listenContext;

@property (nonatomic, assign) SplashAd_OnLoad onLoad;
@property (nonatomic, assign) SplashAd_OnLoadError onLoadError;
@property (nonatomic, assign) SplashAd_WillShow willShow;
@property (nonatomic, assign) SplashAd_DidShow didShow;
@property (nonatomic, assign) SplashAd_DidClick didClick;
@property (nonatomic, assign) SplashAd_DidClose didClose;
@property (nonatomic, assign) SplashAd_RenderSuccess renderSuccess;
@property (nonatomic, assign) SplashAd_RenderFailed renderFailed;
@property (nonatomic, assign) SplashAd_VideoAdDidPlayFinish didPlayFinish;
@property (nonatomic, assign) SplashAd_SplashAdVCClosed vcClosed;
@property (nonatomic, assign) SplashAd_DidCloseOtherController didCloseOtherController;

@end


@implementation BUBridgeSplashAd

// new splash ad delegate
/// This method is called when material load successful
- (void)splashAdLoadSuccess:(BUSplashAd *)splashAd {
    if (self.onLoad) {
        self.onLoad((__bridge void*)self, self.loadContext);
    }
}

/// This method is called when material load failed
- (void)splashAdLoadFail:(BUSplashAd *)splashAd error:(BUAdError *_Nullable)error {
    if (self.onLoadError) {
        self.onLoadError((int)error.code, AutonomousStringCopy_Splash([[error localizedDescription] UTF8String]), self.loadContext);
    }
    [self.splashAd.splashView removeFromSuperview];
    _splashAd = nil;
}

/// This method is called when splash view render successful
- (void)splashAdRenderSuccess:(BUSplashAd *)splashAd {
    if (self.renderSuccess) {
        self.renderSuccess(self.listenContext);
        [self.splashAd showSplashViewInRootViewController:GetAppController().rootViewController];
    }
}

/// This method is called when splash view render failed
- (void)splashAdRenderFail:(BUSplashAd *)splashAd error:(BUAdError *_Nullable)error {
    if (self.renderFailed) {
        self.renderFailed(self.listenContext);
    }
}

/// This method is called when splash view will show
- (void)splashAdWillShow:(BUSplashAd *)splashAd {
    if (self.willShow) {
        self.willShow(self.listenContext, 0);
    }
}

/// This method is called when splash view did show
- (void)splashAdDidShow:(BUSplashAd *)splashAd {
    if(self.didShow) {
        self.didShow(self.listenContext, 0);
    }
}

/// This method is called when splash view is clicked.
- (void)splashAdDidClick:(BUSplashAd *)splashAd {
    if (self.didClick) {
        self.didClick(self.listenContext, 0);
    }
}

/// This method is called when splash view is closed.
- (void)splashAdDidClose:(BUSplashAd *)splashAd closeType:(BUSplashAdCloseType)closeType {
    if (self.didClose) {
        self.didClose(self.listenContext,(int)closeType);
    }
}

/// This method is called when splash viewControllr is closed.
- (void)splashAdViewControllerDidClose:(BUSplashAd *)splashAd {
    if(self.vcClosed) {
        self.vcClosed(self.listenContext);
    }
}

/**
 This method is called when another controller has been closed.
 @param interactionType : open appstore in app or open the webpage or view video ad details page.
 */
- (void)splashDidCloseOtherController:(BUSplashAd *)splashAd interactionType:(BUInteractionType)interactionType {
    if (self.didCloseOtherController) {
        self.didCloseOtherController((int)interactionType, self.listenContext);
    }
}

/// This method is called when when video ad play completed or an error occurred.
- (void)splashVideoAdDidPlayFinish:(BUSplashAd *)splashAd didFailWithError:(NSError *)error {
    if (self.didPlayFinish) {
        self.didPlayFinish(self.listenContext);
    }
}

/// 广告展示失败回调
/// @param splashAd 广告管理对象
/// @param error 展示失败原因
- (void)splashAdDidShowFailed:(BUSplashAd *_Nonnull)splashAd error:(NSError *)error {
    
}

/// 广告即将展示广告详情页回调
/// @param splashAd 广告管理对象
- (void)splashAdWillPresentFullScreenModal:(BUSplashAd *)splashAd {
    
}

- (id<BUAdClientBiddingProtocol>)adObject {
    return self.splashAd;
}

#if defined (__cplusplus)
extern "C" {
#endif
    
    void* UnionPlatform_SplashAd_Load(AdSlotStruct *adSlot,
                                      int timeout,
                                      SplashAd_OnLoadError onLoadError,
                                      SplashAd_OnLoad onLoad,
                                      SplashAd_RenderSuccess renderSuccess,
                                      SplashAd_RenderFailed renderFailed,
                                      int context) {
        
        BUBridgeSplashAd* instance = [[BUBridgeSplashAd alloc] init];
        instance.loadContext = context;
        instance.onLoad = onLoad;
        instance.onLoadError = onLoadError;
        instance.renderSuccess = renderSuccess;
        instance.renderFailed = renderFailed;
        
        CGRect frame = CGRectMake(0, 0, CGRectGetWidth([UIScreen mainScreen].bounds), CGRectGetHeight([UIScreen mainScreen].bounds));
        BUAdSlot *slot = [BUAdSlot new];
        slot.ID = [[NSString alloc] initWithCString:adSlot->slotId encoding:NSUTF8StringEncoding];
        slot.AdType = BUAdSlotAdTypeSplash;
        slot.position = BUAdSlotPositionFullscreen;
        BUSize *size = [[BUSize alloc] init];
        size.width = frame.size.width * [[UIScreen mainScreen] scale];
        size.height = frame.size.height * [[UIScreen mainScreen] scale];
        slot.imgSize = size;
        slot.adloadSeq = 0;
        slot.primeRit = nil;
        if (adSlot->adLoadType != 0) {
            slot.adLoadType = (BUAdLoadType)[@(adSlot->adLoadType) integerValue];
        }
        if (strlen(adSlot->m_s_AdnName) > 0 && strlen(adSlot->m_s_AppId) > 0 && strlen(adSlot->m_s_AdnSlotId) > 0) {
            BUMSplashUserData * splashUserData = [[BUMSplashUserData alloc]init];
            splashUserData.adnName = [[NSString alloc] initWithCString:adSlot->m_s_AdnName encoding:NSUTF8StringEncoding];
            splashUserData.rit = [[NSString alloc] initWithCString:adSlot->m_s_AdnSlotId encoding:NSUTF8StringEncoding];
            splashUserData.appID = [[NSString alloc] initWithCString:adSlot->m_s_AppId encoding:NSUTF8StringEncoding];
            splashUserData.appKey = [[NSString alloc] initWithCString:adSlot->m_s_Appkey encoding:NSUTF8StringEncoding];
            slot.mediation.splashUserData = splashUserData;
        }
        
        slot.mediation.bidNotify = adSlot->m_bidNotify;
        slot.mediation.scenarioID = [[NSString alloc] initWithCString:adSlot->m_cenarioId encoding:NSUTF8StringEncoding];
        
        instance.splashAd = [[BUSplashAd alloc] initWithSlot:slot adSize:CGSizeMake(frame.size.width, frame.size.height)];
        instance.splashAd.delegate = instance;
        
        if (strlen(adSlot->autoPlayPolicy) > 0) {
            NSString *str = [[NSString alloc] initWithCString:adSlot->autoPlayPolicy encoding:NSUTF8StringEncoding];
            NSNumber *num = @(str.integerValue);
            [instance.splashAd.mediation addParam:num withKey:BUMAdAddParamKeyAutoPlayPolicy];
        }
        
        if (timeout > 0) {
            instance.splashAd.tolerateTimeout = timeout;
        }
        
        [instance.splashAd loadAdData];
        
        NSLog(@"CSJM_Unity  开屏 设置 adSlot，其中 %@, %@, %@,  %@, %@",
              [NSString stringWithFormat:@"slotId-%@ ，",slot.ID],
              [NSString stringWithFormat:@"bidNotify-%d ，",slot.mediation.bidNotify],
              [NSString stringWithFormat:@"scenarioID-%@ ，",slot.mediation.scenarioID],
              [NSString stringWithFormat:@"mutedIfCan-%d ，",slot.mediation.mutedIfCan],
              [NSString stringWithFormat:@"开屏兜底 adnName-%@ rit-%@ appID-%@ appKey-%@，",slot.mediation.splashUserData.adnName, slot.mediation.splashUserData.rit, slot.mediation.splashUserData.appID, slot.mediation.splashUserData.appKey]
              );
        
        // 强持有，是引用加+1
        
        return (__bridge_retained void*)instance;
    }
    
    void UnionPlatform_SplashAd_SetInteractionListener(void* splashAdPtr,
                                                       SplashAd_WillShow willshow,
                                                       SplashAd_DidShow didShow,
                                                       SplashAd_DidClick didClick,
                                                       SplashAd_DidClose didClose,
                                                       SplashAd_VideoAdDidPlayFinish playFinish,
                                                       int context) {
        BUBridgeSplashAd* splashAd = (__bridge BUBridgeSplashAd*)splashAdPtr;
        
        splashAd.listenContext = context;
        splashAd.willShow = willshow;
        splashAd.didClick = didClick;
        splashAd.didClose = didClose;
        splashAd.didShow  = didShow;
        splashAd.didPlayFinish = playFinish;
    }
    
    void UnionPlatform_SplashAd_Show (void* splashAdPtr) {
        BUBridgeSplashAd *splashAd = (__bridge BUBridgeSplashAd*)splashAdPtr;
        [splashAd.splashAd showSplashViewInRootViewController:GetAppController().rootViewController];
    }
    
    void UnionPlatform_SplashAd_Dispose(void* splashAdPtr) {
        
        //        (__bridge_transfer BUBridgeSplashAd*)splashAd;
        dispatch_async(dispatch_get_main_queue(), ^{
            BUBridgeSplashAd *splashAd = (__bridge BUBridgeSplashAd*)splashAdPtr;
            [splashAd.splashAd.splashView removeFromSuperview];
            splashAd.splashAd = nil;
            (__bridge_transfer BUBridgeSplashAd*)splashAdPtr;
        });
        // [[BUToUnityAdManager sharedInstance] deleteAdManager:splashAd];
        
    }
    
    bool UnionPlatform_SplashAdHaveMediationManager(void * splashAdPtr) {
        BUBridgeSplashAd *splashAd = (__bridge BUBridgeSplashAd*)splashAdPtr;
        return splashAd.splashAd.mediation ? true : false;
    }
    
    bool UnionPlatform_SplashAdMediationisReady(void * splashAdPtr) {
        BUBridgeSplashAd *splashAd = (__bridge BUBridgeSplashAd*)splashAdPtr;
        if (splashAd.splashAd.mediation) {
            return splashAd.splashAd.mediation.isReady;
        } else {
            return false;
        }
    }
    
    const char* UnionPlatform_SplashAdMediationGetShowEcpmInfo(void * splashAdPtr) {
        BUBridgeSplashAd *splashAd = (__bridge BUBridgeSplashAd*)splashAdPtr;
        BUMRitInfo *info = splashAd.splashAd.mediation.getShowEcpmInfo;
        if (info) {
            NSString *infoJson = [BUMRitInfo toJson:info];
            return AutonomousStringCopy_Splash([infoJson UTF8String]);
        } else {
            return NULL;
        }
    }
    
    const char* UnionPlatform_SplashAdMediationGetCurrentBestEcpmInfo(void * splashAdPtr) {
        BUBridgeSplashAd *splashAd = (__bridge BUBridgeSplashAd*)splashAdPtr;
        BUMRitInfo *info = splashAd.splashAd.mediation.getCurrentBestEcpmInfo;
        if (info) {
            NSString *infoJson = [BUMRitInfo toJson:info];
            return AutonomousStringCopy_Splash([infoJson UTF8String]);
        } else {
            return NULL;
        }
    }
    
    const char* UnionPlatform_SplashAdMediationMultiBiddingEcpmInfos(void * splashAdPtr) {
        BUBridgeSplashAd *splashAd = (__bridge BUBridgeSplashAd*)splashAdPtr;
        NSArray<BUMRitInfo *> * infos = splashAd.splashAd.mediation.multiBiddingEcpmInfos;
        if (infos && infos.count > 0) {
            NSMutableArray *muArr = [[NSMutableArray alloc]initWithCapacity:infos.count];
            for (BUMRitInfo *info in infos) {
                NSString *infoJson = [BUMRitInfo toJson:info];
                [muArr addObject:infoJson];
            }
            NSData *jsonData = [NSJSONSerialization dataWithJSONObject:muArr options:NSJSONWritingPrettyPrinted error:nil];
            NSString *strJson = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
            return AutonomousStringCopy_Splash([strJson UTF8String]);
        } else {
            return NULL;
        }
    }
    
    const char* UnionPlatform_SplashAdMediationCacheRitList(void * splashAdPtr) {
        BUBridgeSplashAd *splashAd = (__bridge BUBridgeSplashAd*)splashAdPtr;
        NSArray<BUMRitInfo *> * infos = splashAd.splashAd.mediation.cacheRitList;
        if (infos && infos.count > 0) {
            NSMutableArray *muArr = [[NSMutableArray alloc]initWithCapacity:infos.count];
            for (BUMRitInfo *info in infos) {
                NSString *infoJson = [BUMRitInfo toJson:info];
                [muArr addObject:infoJson];
            }
            NSData *jsonData = [NSJSONSerialization dataWithJSONObject:muArr options:NSJSONWritingPrettyPrinted error:nil];
            NSString *strJson = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
            return AutonomousStringCopy_Splash([strJson UTF8String]);
        } else {
            return NULL;
        }
    }
    
    const char* UnionPlatform_SplashAdMediationGetAdLoadInfoList(void * splashAdPtr) {
        BUBridgeSplashAd *splashAd = (__bridge BUBridgeSplashAd*)splashAdPtr;
        NSArray<BUMAdLoadInfo *> * infos = splashAd.splashAd.mediation.getAdLoadInfoList;
        if (infos && infos.count > 0) {
            NSMutableArray *muArr = [[NSMutableArray alloc]initWithCapacity:infos.count];
            for (BUMAdLoadInfo *info in infos) {
                NSString *infoJson = [BUMAdLoadInfo toJson:info];
                [muArr addObject:infoJson];
            }
            NSData *jsonData = [NSJSONSerialization dataWithJSONObject:muArr options:NSJSONWritingPrettyPrinted error:nil];
            NSString *strJson = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
            return AutonomousStringCopy_Splash([strJson UTF8String]);
        } else {
            return NULL;
        }
    }
    
#if defined (__cplusplus)
}
#endif

@end
