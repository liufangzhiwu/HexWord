//------------------------------------------------------------------------------
// Copyright (c) 2018-2019 Beijing Bytedance Technology Co., Ltd.
// All Right Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.
//------------------------------------------------------------------------------

#import <BUAdSDK/BUNativeAd.h>
#import <BUAdSDK/BUNativeAdRelatedView.h>
#import "UnityAppController.h"
#import "BUToUnityBundleHelper.h"
#import "AdSlot.h"
#import "CanvasViewLayout.h"
#import "BUToUnityAdManager.h"

extern const char* AutonomousStringCopy_FeedAd(const char* string);

const char* AutonomousStringCopy_FeedAd(const char* string)
{
    if (string == NULL) {
        return NULL;
    }
    
    char* res = (char*)malloc(strlen(string) + 1);
    strcpy(res, string);
    return res;
}

// IRewardVideoAdListener callbacks.
typedef void(*FeedAd_OnError)(int code, const char* message, int context);
typedef void(*FeedAd_OnNativeAdLoad)(void* nativeAd, int context, int slotType);

typedef void(*FeedAd_OnAdShow)(int context);
typedef void(*FeedAd_OnAdDidClick)(int context);
typedef void(*FeedAd_OnAdClose)(int context);
typedef void(*FeedAd_OnAdRemove)(int context);

// The BURewardedVideoAdDelegate implement.
@interface BUBridgeNativeAdsManager : NSObject
@end

@interface BUBridgeNativeAdsManager () <BUMNativeAdsManagerDelegate, BUMNativeAdDelegate, BUVideoAdViewDelegate, BUAdObjectProtocol, BUCustomEventProtocol>
@property (nonatomic, strong) BUNativeAdsManager *nativeAdsManager;
@property (nonatomic, strong) BUNativeAd *nativeAd_0;

@property (nonatomic, assign) int loadContext;
@property (nonatomic, assign) FeedAd_OnError onError;
@property (nonatomic, assign) FeedAd_OnNativeAdLoad onNativeAdLoad;

@property (nonatomic, assign) int interactionContext;
@property (nonatomic, assign) FeedAd_OnAdShow onAdShow;
@property (nonatomic, assign) FeedAd_OnAdDidClick onAdDidClick;
@property (nonatomic, assign) FeedAd_OnAdClose onAdClose;
@property (nonatomic, assign) FeedAd_OnAdRemove onAdRemove;

@property (nonatomic, assign) CGFloat ad_width;
@property (nonatomic, assign) CGFloat ad_height;

// CSJ布局
@property (nonatomic, strong) UIView *customview;
@property (nonatomic, strong) UILabel *infoLabel;
@property (nonatomic, strong) UILabel *titleLabel;
@property (nonatomic, strong) UIImageView *imageView;
@property (nonatomic, strong) UIButton *actionButton;
@property (nonatomic, strong) UIButton *closeButton;
@property (nonatomic, strong) UILabel *adLabel;
@property (nonatomic, strong) BUNativeAdRelatedView *relatedView;
@property (nonatomic, assign) BUAdSlotAdType adType;

@end

@implementation BUBridgeNativeAdsManager

- (instancetype)init
{
    self = [super init];
    if (self) {
      // 快手 摇扭view 功能相关
      //  [[NSNotificationCenter defaultCenter] addObserver:self selector:@selector(didReceiveWidgetNotification:) name:BUMMediaAdWidgetViewCreateNotificationName object:nil];
    }
    return self;
}

- (void)dealloc {
    // 快手 摇扭view 功能相关
    //[[NSNotificationCenter defaultCenter]removeObserver:self];
    [self dispose];
}

- (void)dispose {
    if (self.nativeAd_0 == nil) {
        return;
    }
    if (self.nativeAd_0.mediation) {
        if ([NSThread currentThread].isMainThread) {
            [self.nativeAd_0.mediation.canvasView removeFromSuperview];
        } else {
            dispatch_async(dispatch_get_main_queue(), ^{
                [self.nativeAd_0.mediation.canvasView removeFromSuperview];
            });
        }
    } else {
        if ([NSThread currentThread].isMainThread) {
            self.relatedView = nil;
            [self.customview removeFromSuperview];
            self.customview = nil;
        } else {
            dispatch_async(dispatch_get_main_queue(), ^{
                self.relatedView = nil;
                [self.customview removeFromSuperview];
                self.customview = nil;
            });
        }
    }
}

#warning 该位置布局为样例，请根据自己项目调整
- (void)didReceiveWidgetNotification:(NSNotification *)notify {
    BUMAdViewWidget *rotateWidget = notify.userInfo[BUMMediaAdRotateViewCreator];
    if (rotateWidget) {
        CGRect newFrame1;
        newFrame1.origin.x = 0;//(rotateWidget.adView.frame.size.width - 100)/2;
        newFrame1.origin.y = 0;//(rotateWidget.adView.frame.size.height - 100)/2;
        newFrame1.size.height = 100;
        newFrame1.size.width = 100;
        
        UIView *rotateView = [rotateWidget renderWithFrame:newFrame1];
        if (rotateView) {
            [rotateWidget.adView addSubview:rotateView];
        }
    }
}

#pragma mark - BUNativeAdsManagerDelegate
- (void)nativeAdsManagerSuccessToLoad:(BUNativeAdsManager *)adsManager nativeAds:(NSArray<BUNativeAd *> *_Nullable)nativeAdDataArray {
    if (nativeAdDataArray.count <= 0) {
        return;
    }
    
    self.nativeAd_0 = nativeAdDataArray[0];
    self.nativeAd_0.delegate = self;
    
    UIViewController *rootVc = GetAppController().rootViewController;
    self.nativeAd_0.rootViewController = rootVc;
    
    if (self.nativeAd_0.mediation.isExpressAd) {
        [self.nativeAd_0.mediation render];
    }
    
    if (self.onNativeAdLoad) {
        self.onNativeAdLoad((__bridge void*)self, self.loadContext, 0);
    }
    
    }

- (void)nativeAdsManager:(BUNativeAdsManager *)adsManager didFailWithError:(NSError *_Nullable)error {
    if (self.onError) {
        self.onError((int)error.code, AutonomousStringCopy_FeedAd([[error localizedDescription] UTF8String]), self.loadContext);
    }
}

#pragma mark - BUMNativeAdsManagerDelegate
/// 暂不开放使用
- (void)nativeAdsManager:(BUNativeAdsManager *_Nonnull)adsManager didWaitingBiddingResultWithParameters:(NSDictionary *)parameters andResumeHandler:(void(^)(NSDictionary *_Nullable data, NSError *_Nullable error))handler {
    
}

/// 暂不开放使用
- (void)nativeAdsManagerDidFinishLoadAdnAd:(BUNativeAdsManager *_Nonnull)adsManager nativeAd:(BUNativeAd *_Nullable)nativeAd error:(NSError *_Nullable)error {
    
}

#pragma mark - BUNativeAdDelegate
/**
 This method is called when native ad material loaded successfully. This method will be deprecated. Use nativeAdDidLoad:view: instead
 @Note :  Mediation dimension does not support this interface.
 */
- (void)nativeAdDidLoad:(BUNativeAd *)nativeAd {
    
}


/**
 This method is called when native ad material loaded successfully.
 @Note :  Mediation dimension does not support this interface.
 */
- (void)nativeAdDidLoad:(BUNativeAd *)nativeAd view:(UIView *_Nullable)view {
    
}

/**
 This method is called when native ad materia failed to load.
 @param error : the reason of error
 @Note :  Mediation dimension does not support this interface.
 */
- (void)nativeAd:(BUNativeAd *)nativeAd didFailWithError:(NSError *_Nullable)error {
    
}

/**
 This method is called when native ad slot has been shown.
 */
- (void)nativeAdDidBecomeVisible:(BUNativeAd *)nativeAd {
    if (self.onAdShow) {
        self.onAdShow(self.interactionContext);
    }
}

/**
 This method is called when another controller has been closed.
 @param interactionType : open appstore in app or open the webpage or view video ad details page.
 */
- (void)nativeAdDidCloseOtherController:(BUNativeAd *)nativeAd interactionType:(BUInteractionType)interactionType {
    
}

/**
 This method is called when native ad is clicked.
 */
- (void)nativeAdDidClick:(BUNativeAd *)nativeAd withView:(UIView *_Nullable)view {
    if (self.onAdDidClick) {
        self.onAdDidClick(self.interactionContext);
    }
}

/**
 This method is called when the user clicked dislike reasons.
 Only used for dislikeButton in BUNativeAdRelatedView.h
 @param filterWords : reasons for dislike
 */
- (void)nativeAd:(BUNativeAd *_Nullable)nativeAd dislikeWithReason:(NSArray<BUDislikeWords *> *_Nullable)filterWords {
    if (self.onAdClose) {
        self.onAdClose(self.interactionContext);
    }
    
    [self dispose];
}

/**
 This method is called when the Ad view container is forced to be removed.
 @param nativeAd : Ad material
 @param adContainerView : Ad view container
 @Note :  Mediation dimension does not support this interface.
 */
- (void)nativeAd:(BUNativeAd *_Nullable)nativeAd adContainerViewDidRemoved:(UIView *)adContainerView {
    if (self.onAdRemove) {
        self.onAdRemove(self.interactionContext);
    }
}

#pragma mark - BUMNativeAdDelegate
/// 广告即将展示全屏页面/商店时触发
/// @param nativeAd 广告视图
- (void)nativeAdWillPresentFullScreenModal:(BUNativeAd *_Nonnull)nativeAd {
    
}

/// 聚合维度混出模板广告时渲染成功回调，可能不会回调
/// @param nativeAd 模板广告对象
- (void)nativeAdExpressViewRenderSuccess:(BUNativeAd *_Nonnull)nativeAd {
    
}

/// 聚合维度混出模板广告时渲染失败回调，可能不会回调
/// @param nativeAd 模板广告对象
/// @param error 渲染出错原因
- (void)nativeAdExpressViewRenderFail:(BUNativeAd *_Nonnull)nativeAd error:(NSError *_Nullable)error {
    
}

/// 当视频播放状态改变之后触发
/// @param nativeAd 广告视图
/// @param playerState 变更后的播放状态
- (void)nativeAdVideo:(BUNativeAd *_Nullable)nativeAd stateDidChanged:(BUPlayerPlayState)playerState {
    
}

/// 广告视图中视频视图被点击时触发
/// @param nativeAd 广告视图
- (void)nativeAdVideoDidClick:(BUNativeAd *_Nullable)nativeAd {
    
}

/// 广告视图中视频播放完成时触发
/// @param nativeAd 广告视图
- (void)nativeAdVideoDidPlayFinish:(BUNativeAd *_Nullable)nativeAd {
    
}

/// 广告摇一摇提示view消除时调用该方法
/// @param nativeAd 广告视图
- (void)nativeAdShakeViewDidDismiss:(BUNativeAd *_Nullable)nativeAd {
    
}

/// 激励信息流视频进入倒计时状态时调用
/// @param nativeAdView 广告视图
/// @param countDown : 倒计时
/// @Note : 当前该回调仅适用于CSJ广告 - v4200
- (void)nativeAdVideo:(BUNativeAd *_Nullable)nativeAdView rewardDidCountDown:(NSInteger)countDown {
    
}

#pragma mark - layout
#warning 该位置布局为样例，请根据自己项目调整
- (void)buildupViewFeed {
    CGFloat margin = 5;
    CGFloat cusViewWidth = 200 - margin*2;
    CGFloat cusViewHeight = 200 - margin*2;
    CGFloat leftMargin = cusViewWidth/20;
    [_customview removeFromSuperview];
    _customview = nil;
    _relatedView = nil;
    _relatedView = [[BUNativeAdRelatedView alloc] init];
    // Custom view test
    _customview = [[UIView alloc] initWithFrame:CGRectMake(0, 0, cusViewWidth, cusViewHeight)];
    _customview.backgroundColor = [UIColor lightGrayColor];
    
    CGFloat swidth = CGRectGetWidth(_customview.frame);
    
    _infoLabel = [[UILabel alloc] initWithFrame:CGRectMake(leftMargin, leftMargin, swidth - leftMargin * 2, 30)];
    _infoLabel.backgroundColor = [UIColor magentaColor];
    _infoLabel.text = @"info";
    _infoLabel.adjustsFontSizeToFitWidth = YES;
    [_customview addSubview:_infoLabel];
    
    CGFloat buttonWidth = ceilf((swidth-4 * leftMargin)/3);
    _actionButton = [[UIButton alloc] initWithFrame:CGRectMake(CGRectGetMinX(_infoLabel.frame), CGRectGetMaxY(_infoLabel.frame)+5, buttonWidth, 30)];
    [_actionButton setTitle:@"action" forState:UIControlStateNormal];
    _actionButton.userInteractionEnabled = YES;
    _actionButton.backgroundColor = [UIColor orangeColor];
    _actionButton.titleLabel.adjustsFontSizeToFitWidth = YES;
    [_customview addSubview:_actionButton];
    
    _titleLabel = [[UILabel alloc] initWithFrame:CGRectMake(CGRectGetMaxX(_actionButton.frame)+5, CGRectGetMaxY(_infoLabel.frame)+5, 100, 30)];
    _titleLabel.backgroundColor = [UIColor clearColor];
    _titleLabel.text = @"AdsTitle";
    _titleLabel.adjustsFontSizeToFitWidth = YES;
    [_customview addSubview:_titleLabel];
    
    _imageView = [[UIImageView alloc] init];
    _imageView.userInteractionEnabled = YES;
    _imageView.backgroundColor = [UIColor redColor];
    [_customview addSubview:_imageView];
    
    // add video view
    [_customview addSubview:self.relatedView.mediaAdView];
    self.relatedView.mediaAdView.delegate = self;
    // add logo view
    self.relatedView.logoADImageView.frame = CGRectZero;
    [_customview addSubview:self.relatedView.logoADImageView];
    // add dislike view
    self.relatedView.dislikeButton.frame = CGRectMake(CGRectGetMaxX(_infoLabel.frame) - 20, CGRectGetMaxY(_infoLabel.frame)+5, 24, 20);
    [_customview addSubview:self.relatedView.dislikeButton];
    // add ad lable
    self.relatedView.adLabel.frame = CGRectZero;
    [_customview addSubview:self.relatedView.adLabel];
}

#warning 该位置布局为样例，请根据自己项目调整
- (void)refreshNativeAd:(BUNativeAd *)nativeAd {
    self.infoLabel.text = nativeAd.data.AdDescription;
    self.titleLabel.text = nativeAd.data.AdTitle;
    self.imageView.hidden = YES;
    BUMaterialMeta *adMeta = nativeAd.data;
    CGFloat contentWidth = CGRectGetWidth(_customview.frame) - 50;
    BUImage *image = adMeta.imageAry.firstObject;
    const CGFloat imageHeight = contentWidth * (image.height / image.width);
    CGRect rect = CGRectMake(25, CGRectGetMaxY(_actionButton.frame) + 5, contentWidth, imageHeight);
//    self.relatedView.buildupViewFeed.frame = CGRectMake(CGRectGetMaxX(rect) - 15 , CGRectGetMaxY(rect) - 15, 15, 15);
    self.relatedView.adLabel.frame = CGRectMake(CGRectGetMinX(rect), CGRectGetMaxY(rect) - 14, 26, 14);
    [self.relatedView refreshData:nativeAd];
    // imageMode decides whether to show video or not
    if (adMeta.imageMode == BUFeedVideoAdModeImage ||
        adMeta.imageMode == BUFeedVideoAdModePortrait ||
        adMeta.imageMode == BUFeedADModeSquareVideo ||
        adMeta.imageMode == BUFeedADModeCreativeVideoImage ||
        adMeta.imageMode == BUFeedADModeCreativeVideoImagePortrait) {
        self.imageView.hidden = YES;
        self.relatedView.mediaAdView.hidden = NO;
        self.relatedView.mediaAdView.frame = rect;
    } else {
        self.imageView.hidden = NO;
        self.relatedView.mediaAdView.hidden = YES;
        if (adMeta.imageAry.count > 0) {
            if (image.imageURL.length > 0) {
                self.imageView.frame = rect;
                UIImage *imagePic = [UIImage imageWithData:[NSData dataWithContentsOfURL:[NSURL URLWithString:image.imageURL]]];
                [self.imageView setImage:imagePic];
            }
        }
    }
    // Register UIView with the native ad; the whole UIView will be clickable.
    [nativeAd registerContainer:self.customview withClickableViews:@[self.infoLabel, self.actionButton]];
}

- (void)refreshUIWithLoaction:(CGPoint)point {
    if (self.nativeAd_0 == nil) {
        return;
    }
    [CanvasViewLayout exampleLayoutWithFrame:CGRectMake(point.x, point.y, self.ad_width, self.ad_height) canvasView:self.nativeAd_0.mediation.canvasView];
    [self.nativeAd_0.mediation.canvasView.dislikeBtn addTarget:self action:@selector(closeNativeAd:) forControlEvents:UIControlEventTouchUpInside];
}

- (id<BUAdClientBiddingProtocol>)adObject {
    return self.nativeAd_0;
}

- (void)closeNativeAd:(UIButton *)btn {
    if (self.onAdClose) {
        self.onAdClose(self.interactionContext);
    }
    [self dispose];
}

@end

#if defined (__cplusplus)
extern "C" {
#endif

void UnionPlatform_FeedAd_Load(
                               AdSlotStruct *slot,
                               FeedAd_OnError onError,
                               FeedAd_OnNativeAdLoad onNativeAdLoad,
                               int context) {
    BUAdSlot *slot1 = [[BUAdSlot alloc] init];
    slot1.ID = [[NSString alloc] initWithCString:slot->slotId encoding:NSUTF8StringEncoding];
    if (slot->width > 0 && slot->height > 0) {
        BUSize *imgSize1 = [[BUSize alloc] init];
        imgSize1.width = slot->width/[UIScreen mainScreen].scale;
        imgSize1.height = slot->height/[UIScreen mainScreen].scale;
        slot1.imgSize = imgSize1;
    }
    if (slot->viewWidth > 0 && slot->viewHeight > 0) {
        CGSize adSize = CGSizeMake(slot->viewWidth, slot->viewHeight);
        slot1.adSize = adSize;
    }
    if (slot->adLoadType != 0) {
        slot1.adLoadType = (BUAdLoadType)[@(slot->adLoadType) integerValue];
    }
    slot1.AdType = BUAdSlotAdTypeFeed;
    slot1.position = BUAdSlotPositionFeed;
    slot1.mediation.bidNotify = slot->m_bidNotify;
    slot1.mediation.scenarioID = [[NSString alloc] initWithCString:slot->m_cenarioId encoding:NSUTF8StringEncoding];
    slot1.mediation.mutedIfCan = slot->m_isMuted;
    slot1.supportIconStyle = slot->supportIconStyle;
    
    BUBridgeNativeAdsManager *instance = [[BUBridgeNativeAdsManager alloc]init];
    BUNativeAdsManager *nativeAdsManager = [[BUNativeAdsManager alloc]initWithSlot:slot1];
    if (strlen(slot->autoPlayPolicy) > 0) {
        NSString *str = [[NSString alloc] initWithCString:slot->autoPlayPolicy encoding:NSUTF8StringEncoding];
        NSNumber *num = @(str.integerValue);
        [nativeAdsManager.mediation addParam:num withKey:BUMAdAddParamKeyAutoPlayPolicy];
        NSLog(@"CSJM_Unity  设置不同网络下的视频播放策略为 %@", num);
    }
    nativeAdsManager.delegate = instance;
    instance.nativeAdsManager = nativeAdsManager;
    instance.onError = onError;
    instance.onNativeAdLoad = onNativeAdLoad;
    instance.loadContext = context;
    instance.ad_width = slot->viewWidth;
    instance.ad_height = slot->viewHeight;
    // 快手 摇扭view 功能相关
    //[nativeAdsManager.mediation addParam:@(slot->m_isRotate) withKey:BUMMediaAdRotateViewEnable];
 
     
    [nativeAdsManager loadAdDataWithCount:1];
    
    NSLog(@"CSJM_Unity  信息流/draw 设置 adSlot，其中 %@, %@, %@,  %@, %@",
          [NSString stringWithFormat:@"slotId-%@ ，",slot1.ID],
          [NSString stringWithFormat:@"bidNotify-%d ，",slot1.mediation.bidNotify],
          [NSString stringWithFormat:@"scenarioID-%@ ，",slot1.mediation.scenarioID],
          [NSString stringWithFormat:@"mutedIfCan-%d ，",slot1.mediation.mutedIfCan],
          [NSString stringWithFormat:@"adSize-%d-%d ，",slot->viewWidth, slot->viewHeight]
          );
    
    (__bridge_retained void*)instance;
}

void UnionPlatform_FeedAd_SetInteractionListener (
                                                  void* nativeAdPtr,
                                                  FeedAd_OnAdShow onAdShow,
                                                  FeedAd_OnAdDidClick onAdDidClick,
                                                  FeedAd_OnAdClose onAdClose,
                                                  FeedAd_OnAdRemove onAdRemove,
                                                  int context) {
    BUBridgeNativeAdsManager *instance = (__bridge BUBridgeNativeAdsManager*)nativeAdPtr;
    instance.onAdShow = onAdShow;
    instance.onAdDidClick = onAdDidClick;
    instance.onAdClose = onAdClose;
    instance.onAdRemove = onAdRemove;
    instance.interactionContext = context;
}

void UnionPlatform_FeedAd_ShowNativeAd (void* nativeAdPtr,
                                        int slotType,
                                        float x,
                                        float y) {
    BUBridgeNativeAdsManager *instance = (__bridge BUBridgeNativeAdsManager*)nativeAdPtr;
    if (instance.nativeAd_0 == nil) {
        return;
    }
    
    CGFloat originX = x/[UIScreen mainScreen].scale;
    CGFloat originY = y/[UIScreen mainScreen].scale;
    
    if (instance.nativeAd_0.mediation) {
        if (instance.nativeAd_0.mediation.isExpressAd) {// 模板直接添加
            instance.nativeAd_0.mediation.canvasView.frame = CGRectMake(originX,originY,CGRectGetWidth(instance.nativeAd_0.mediation.canvasView.frame),CGRectGetHeight(instance.nativeAd_0.mediation.canvasView.frame));
        } else {
#warning 开发者如有自渲染布局需求，可在此处处理
            [instance refreshUIWithLoaction:CGPointMake(originX, originY)];
        }
        [GetAppController().rootViewController.view addSubview:instance.nativeAd_0.mediation.canvasView];
    } else {
        [instance buildupViewFeed];
        [instance refreshNativeAd:instance.nativeAd_0];
        instance.customview.frame = CGRectMake(originX,originY,CGRectGetWidth(instance.customview.frame),CGRectGetHeight(instance.customview.frame));
        [GetAppController().rootViewController.view addSubview:instance.customview];
    }
    
    
}

void  UnionPlatform_FeedAd_Dispose(void* nativeAdPtr) {
    BUBridgeNativeAdsManager *instance = (__bridge BUBridgeNativeAdsManager*)nativeAdPtr;
    [instance dispose];
    (__bridge_transfer BUBridgeNativeAdsManager*)nativeAdPtr;
}

bool UnionPlatform_FeedAd_HaveMediationManager(void* nativeAdPtr) {
    BUBridgeNativeAdsManager *instance = (__bridge BUBridgeNativeAdsManager*)nativeAdPtr;
    if (instance.nativeAd_0 == nil) {
        return false;
    }
    return instance.nativeAd_0.mediation ? true : false;
}

bool UnionPlatform_FeedAdMediationisReady(void* nativeAdPtr) {
    BUBridgeNativeAdsManager *instance = (__bridge BUBridgeNativeAdsManager*)nativeAdPtr;
    if (instance.nativeAd_0 == nil) {
        return false;
    }
    if (instance.nativeAd_0.mediation) {
        return instance.nativeAd_0.mediation.isReady;
    } else {
        return false;
    }
}

const char* UnionPlatform_FeedAdMediationGetShowEcpmInfo(void* nativeAdPtr) {
    BUBridgeNativeAdsManager *instance = (__bridge BUBridgeNativeAdsManager*)nativeAdPtr;
    if (instance.nativeAd_0 == nil) {
        return NULL;
    }
    BUMRitInfo *info = instance.nativeAd_0.mediation.getShowEcpmInfo;
    if (info) {
        NSString *infoJson = [BUMRitInfo toJson:info];
        return AutonomousStringCopy_FeedAd([infoJson UTF8String]);
    } else {
        return NULL;
    }
}

const char* UnionPlatform_FeedAdMediationGetCurrentBestEcpmInfo(void* nativeAdPtr) {
    BUBridgeNativeAdsManager *instance = (__bridge BUBridgeNativeAdsManager*)nativeAdPtr;
    if (instance.nativeAd_0 == nil) {
        return NULL;
    }
    BUMRitInfo *info = instance.nativeAd_0.mediation.getCurrentBestEcpmInfo;
    if (info) {
        NSString *infoJson = [BUMRitInfo toJson:info];
        return AutonomousStringCopy_FeedAd([infoJson UTF8String]);
    } else {
        return NULL;
    }
}

const char* UnionPlatform_FeedAdMediationMultiBiddingEcpmInfos(void* nativeAdPtr) {
    BUBridgeNativeAdsManager *instance = (__bridge BUBridgeNativeAdsManager*)nativeAdPtr;
    if (instance.nativeAd_0 == nil) {
        return NULL;
    }
    NSArray<BUMRitInfo *> * infos = instance.nativeAd_0.mediation.multiBiddingEcpmInfos;
    if (infos && infos.count > 0) {
        NSMutableArray *muArr = [[NSMutableArray alloc]initWithCapacity:infos.count];
        for (BUMRitInfo *info in infos) {
            NSString *infoJson = [BUMRitInfo toJson:info];
            [muArr addObject:infoJson];
        }
        NSData *jsonData = [NSJSONSerialization dataWithJSONObject:muArr options:NSJSONWritingPrettyPrinted error:nil];
        NSString *strJson = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        return AutonomousStringCopy_FeedAd([strJson UTF8String]);
    } else {
        return NULL;
    }
}

const char* UnionPlatform_FeedAdMediationCacheRitList(void* nativeAdPtr) {
    BUBridgeNativeAdsManager *instance = (__bridge BUBridgeNativeAdsManager*)nativeAdPtr;
    if (instance.nativeAd_0 == nil) {
        return NULL;
    }
    NSArray<BUMRitInfo *> * infos = instance.nativeAd_0.mediation.cacheRitList;
    if (infos && infos.count > 0) {
        NSMutableArray *muArr = [[NSMutableArray alloc]initWithCapacity:infos.count];
        for (BUMRitInfo *info in infos) {
            NSString *infoJson = [BUMRitInfo toJson:info];
            [muArr addObject:infoJson];
        }
        NSData *jsonData = [NSJSONSerialization dataWithJSONObject:muArr options:NSJSONWritingPrettyPrinted error:nil];
        NSString *strJson = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        return AutonomousStringCopy_FeedAd([strJson UTF8String]);
    } else {
        return NULL;
    }
}

const char* UnionPlatform_FeedAdMediationGetAdLoadInfoList(void* nativeAdPtr) {
    BUBridgeNativeAdsManager *instance = (__bridge BUBridgeNativeAdsManager*)nativeAdPtr;
    if (instance.nativeAdsManager.mediation == nil) {
        return NULL;
    }
    NSArray<BUMAdLoadInfo *> * infos = instance.nativeAdsManager.mediation.getAdLoadInfoList;
    if (infos && infos.count > 0) {
        NSMutableArray *muArr = [[NSMutableArray alloc]initWithCapacity:infos.count];
        for (BUMAdLoadInfo *info in infos) {
            NSString *infoJson = [BUMAdLoadInfo toJson:info];
            [muArr addObject:infoJson];
        }
        NSData *jsonData = [NSJSONSerialization dataWithJSONObject:muArr options:NSJSONWritingPrettyPrinted error:nil];
        NSString *strJson = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        return AutonomousStringCopy_FeedAd([strJson UTF8String]);
    } else {
        return NULL;
    }
}


#if defined (__cplusplus)
}
#endif
