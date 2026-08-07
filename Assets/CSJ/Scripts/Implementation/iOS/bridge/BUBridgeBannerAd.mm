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

static const char* AutonomousStringCopy_Express(const char* string);

const char* AutonomousStringCopy_Express(const char* string)
{
    if (string == NULL) {
        return NULL;
    }
    
    char* res = (char*)malloc(strlen(string) + 1);
    strcpy(res, string);
    return res;
}


typedef void(*ExpressAd_OnLoad)(void* expressAd, int context);
typedef void(*ExpressAd_OnLoadError)(int errCode,const char* errMsg,int context);
typedef void(*ExpressAd_WillShow)(int context);
typedef void(*ExpressAd_DidClick)(int context);
typedef void(*ExpressAd_DidClose)(int context);
typedef void(*ExpressAd_DidRemove)(int context);
typedef void(*ExpressAd_RenderFailed)(int errCode,const char* errMsg,int context);
typedef void(*ExpressAd_RenderSuccess)(void* expressAd,int context);

@interface BUBridgeBannerAd : NSObject<BUMNativeExpressBannerViewDelegate,BUAdObjectProtocol>

@property(nonatomic, strong) BUNativeExpressBannerView *bannerView;
@property (nonatomic, assign) float adWidth;
@property (nonatomic, assign) float adHeight;

@property (nonatomic, assign) int loadContext;
@property (nonatomic, assign) int listenContext;

@property (nonatomic, assign) ExpressAd_OnLoad onLoad;
@property (nonatomic, assign) ExpressAd_OnLoadError onLoadError;
@property (nonatomic, assign) ExpressAd_WillShow willShow;
@property (nonatomic, assign) ExpressAd_DidClick didClick;
@property (nonatomic, assign) ExpressAd_DidClose didClose;
@property (nonatomic, assign) ExpressAd_DidRemove didRemove;
@property (nonatomic, assign) ExpressAd_RenderFailed renderFailed;
@property (nonatomic, assign) ExpressAd_RenderSuccess renderSuccess;
@end


@implementation BUBridgeBannerAd
- (void)dealloc {
    
}

#pragma BUNativeExpressBannerViewDelegate
- (void)nativeExpressBannerAdViewDidLoad:(BUNativeExpressBannerView *)bannerAdView {
    if (self.onLoad) {
        self.onLoad((__bridge void*)self, self.loadContext);
    }
}

- (void)nativeExpressBannerAdView:(BUNativeExpressBannerView *)bannerAdView didLoadFailWithError:(NSError *)error {
    if (self.onLoadError) {
        self.onLoadError((int)error.code,[[error localizedDescription] UTF8String],self.loadContext);
    }
}

- (void)nativeExpressBannerAdViewRenderSuccess:(BUNativeExpressBannerView *)bannerAdView {
    if (self.renderSuccess) {
        self.renderSuccess((__bridge void*)self, self.loadContext);
    }
}

- (void)nativeExpressBannerAdViewRenderFail:(BUNativeExpressBannerView *)bannerAdView error:(NSError *)error {
    if (self.renderFailed) {
        self.renderFailed((int)error.code,[[error localizedDescription] UTF8String],self.loadContext);
    }
}

- (void)nativeExpressBannerAdViewWillBecomVisible:(BUNativeExpressBannerView *)bannerAdView {
    if (self.willShow) {
        self.willShow(self.listenContext);
    }
}

- (void)nativeExpressBannerAdViewDidClick:(BUNativeExpressBannerView *)bannerAdView {
    if (self.didClick) {
        self.didClick(self.listenContext);
    }
}

- (void)nativeExpressBannerAdView:(BUNativeExpressBannerView *)bannerAdView dislikeWithReason:(NSArray<BUDislikeWords *> *)filterwords {
    [UIView animateWithDuration:0.25 animations:^{
        bannerAdView.alpha = 0;
    } completion:^(BOOL finished) {
        [bannerAdView removeFromSuperview];
        if (self.didClose) {
            self.didClose(self.listenContext);
        }
    }];
}

- (void)nativeExpressBannerAdViewDidCloseOtherController:(BUNativeExpressBannerView *)bannerAdView interactionType:(BUInteractionType)interactionType {
    
}

/**
 This method is called when the Ad view container is forced to be removed.
 @param bannerAdView : Express Banner Ad view container
 */
- (void)nativeExpressBannerAdViewDidRemoved:(BUNativeExpressBannerView *)bannerAdView {
    if (self.didRemove) {
        self.didRemove(self.listenContext);
    }
}

/// 广告展示回调
- (void)nativeExpressBannerAdViewDidBecomeVisible:(BUNativeExpressBannerView *)bannerAdView {
    // 备注：M展示走这个didbecome，因unity那端回调名叫OnAdShow，对应这个self.willShow，故可如下回调
    if (self.willShow) {
        self.willShow(self.listenContext);
    }
}

/// 广告加载成功后为「混用的信息流自渲染广告」时会触发该回调，提供给开发者自渲染的时机
/// @param bannerAd 广告操作对象
/// @param canvasView 携带物料的画布，需要对其内部提供的物料及控件做布局及设置UI
/// @warning 轮播开启时，每次轮播到自渲染广告均会触发该回调，并且canvasView为其他回调中bannerView的子控件
- (void)nativeExpressBannerAdNeedLayoutUI:(BUNativeExpressBannerView *)bannerAd canvasView:(BUMCanvasView *)canvasView {
    [self exampleLayoutWith:bannerAd canvasView:canvasView];
}

- (id<BUAdClientBiddingProtocol>)adObject {
    return self.bannerView;
}

static CGFloat const margin = 15;
static CGSize const logoSize = { 15, 15 };
static UIEdgeInsets const padding = { 10, 15, 10, 15 };
// 这是一个支持banner混出自渲染信息流的样例，如果需要该功能请按照自己需求布局
- (void)exampleLayoutWith:(BUNativeExpressBannerView *)bannerAd canvasView:(BUMCanvasView *)canvasView {
    //    NSInteger index = [self.selfs indexOfObject:self];
    UIImageView *imageView1 = [[UIImageView alloc] init];
    UIImageView *imageView2 = [[UIImageView alloc] init];
    
    CGRect frame = canvasView.frame;
    CGFloat width = CGRectGetWidth(canvasView.bounds);
    CGFloat contentWidth = (width - 2 * margin);
    CGFloat y = padding.top;
    
    // 确定是自渲染才会调用该方法
    //    self.descLabel = [[UILabel alloc] initWithFrame:CGRectMake(0, y, contentWidth, 40)];
    canvasView.descLabel.text = canvasView.data.AdTitle;
    canvasView.descLabel.backgroundColor = [UIColor grayColor];
    y += 40;
    y += 5;
    
    CGFloat leftMargin = frame.size.width/20;
    if (canvasView.data.icon.imageURL) {
        CGFloat cusIconWidth = 30;
        CGFloat cusIconHeight = 30;
        
        canvasView.iconImageView = [[UIImageView alloc] initWithFrame:CGRectMake(0, y, cusIconWidth, cusIconHeight)];
        UIImage *imagePic = [UIImage imageWithData:[NSData dataWithContentsOfURL:[NSURL URLWithString:canvasView.data.icon.imageURL]]];
        [canvasView.iconImageView setImage:imagePic];
        
        canvasView.descLabel.frame = CGRectMake(CGRectGetMaxX(canvasView.iconImageView.frame), CGRectGetMinY(canvasView.iconImageView.frame), frame.size.width - leftMargin - CGRectGetMaxX(canvasView.iconImageView.frame), CGRectGetHeight(canvasView.iconImageView.frame));
    }
    
    CGFloat dislikeX = width - 24;
    // 物料信息可能不包含关闭按钮需要自己实现
    if (!canvasView.dislikeBtn) {
        canvasView.dislikeBtn = [[UIButton alloc] init];
        canvasView.dislikeBtn.backgroundColor = [UIColor cyanColor];
        canvasView.dislikeBtn.userInteractionEnabled = YES;
    }
    canvasView.dislikeBtn.frame = CGRectMake(dislikeX-24, 0, 24, 24);
    
    CGFloat originInfoX = padding.left;
    if (canvasView.adLogoView) {
        canvasView.adLogoView.frame = CGRectMake(originInfoX, y + 3, 26, 14);
        originInfoX += 24;
        originInfoX += 10;
    }
    
    canvasView.titleLabel.text = canvasView.data.AdDescription;
    canvasView.titleLabel.frame = CGRectMake(0, CGRectGetMaxY(canvasView.descLabel.frame), contentWidth, 40);
    canvasView.titleLabel.backgroundColor = [UIColor grayColor];
    BUMaterialMeta *adMeta = canvasView.data;
    
    if (canvasView.hasSupportActionBtn) {
        CGFloat customBtnWidth = 100;
        canvasView.callToActionBtn.frame = CGRectMake(dislikeX - customBtnWidth - 5, CGRectGetMaxY(canvasView.titleLabel.frame), customBtnWidth, 20);
        NSString *btnTxt = @"Click";
        if (canvasView.data.buttonText.length > 0) {
            btnTxt = canvasView.data.buttonText;
        }
        [canvasView.callToActionBtn setTitle:btnTxt forState:UIControlStateNormal];
        canvasView.callToActionBtn.backgroundColor = [UIColor redColor];
    }
    
    // imageMode decides whether to show video or not
    if (adMeta.imageMode == BUFeedVideoAdModeImage) {
        canvasView.imageView.hidden = YES;
        if (canvasView.mediaView) {
            BUImage *image = canvasView.data.imageAry.firstObject;
            const CGFloat imageHeight = contentWidth * (image.height / image.width);
            canvasView.mediaView.frame = CGRectMake(0, CGRectGetMaxY(canvasView.titleLabel.frame), contentWidth, imageHeight);
        }
    } else if (adMeta.imageMode == BUFeedADModeLargeImage) {
        canvasView.imageView.hidden = NO;
        if (adMeta.imageAry.count > 0) {
            BUImage *image = canvasView.data.imageAry.firstObject;
            const CGFloat imageHeight = contentWidth * (image.height / image.width);
            if (image.imageURL.length > 0) {
                
                canvasView.imageView.frame = CGRectMake(5, CGRectGetMaxY(canvasView.titleLabel.frame), contentWidth, imageHeight);
                UIImage *imagePic = [UIImage imageWithData:[NSData dataWithContentsOfURL:[NSURL URLWithString:image.imageURL]]];
                [canvasView.imageView setImage:imagePic];
            }
        }
    } else if (adMeta.imageMode == BUFeedADModeGroupImage) {
        canvasView.imageView.hidden = NO;
        CGFloat y = CGRectGetMaxY(canvasView.titleLabel.frame);
        if (canvasView.callToActionBtn.frame.origin.y != 0) {
            y = CGRectGetMaxY(canvasView.callToActionBtn.frame);
        }
        if (adMeta.imageAry.count > 1) {
            CGFloat imageWidth = (contentWidth - 5 * 2) / 3;
            BUImage *image = adMeta.imageAry[1];
            const CGFloat imageHeight = imageWidth * (image.height / image.width);
            if (image.imageURL.length > 0) {
                canvasView.imageView.frame = CGRectMake(5, y + 5, imageWidth, imageHeight);
                UIImage *imagePic = [UIImage imageWithData:[NSData dataWithContentsOfURL:[NSURL URLWithString:image.imageURL]]];
                [canvasView.imageView setImage:imagePic];
            }
        }
        if (adMeta.imageAry.count > 2) {
            CGFloat imageWidth = (contentWidth - 5 * 2) / 3;
            BUImage *image = adMeta.imageAry[2];
            const CGFloat imageHeight = imageWidth * (image.height / image.width);
            if (image.imageURL.length > 0) {
                
                imageView1.frame = CGRectMake(5+imageWidth+10, y + 5, imageWidth, imageHeight);
                UIImage *imagePic = [UIImage imageWithData:[NSData dataWithContentsOfURL:[NSURL URLWithString:image.imageURL]]];
                [imageView1 setImage:imagePic];
                [canvasView addSubview:imageView1];
            }
        }
        if (adMeta.imageAry.count > 3) {
            CGFloat imageWidth = (contentWidth - 5 * 2) / 3;
            BUImage *image = adMeta.imageAry[3];
            const CGFloat imageHeight = imageWidth * (image.height / image.width);
            if (image.imageURL.length > 0) {
                imageView2.frame = CGRectMake(5+imageWidth*2+10+10, y + 5, imageWidth, imageHeight);
                UIImage *imagePic = [UIImage imageWithData:[NSData dataWithContentsOfURL:[NSURL URLWithString:image.imageURL]]];
                [imageView2 setImage:imagePic];
                [canvasView addSubview:imageView2];
            }
        }
    }
    
    [canvasView bringSubviewToFront:canvasView.dislikeBtn];
    canvasView.frame = CGRectMake(frame.origin.x, frame.origin.y, frame.size.width, CGRectGetHeight(canvasView.titleLabel.frame)+CGRectGetHeight(canvasView.descLabel.frame)+CGRectGetHeight(canvasView.descLabel.frame)+CGRectGetHeight(canvasView.mediaView.frame)+CGRectGetHeight(canvasView.descLabel.frame)+CGRectGetHeight(canvasView.imageView.frame)+CGRectGetHeight(canvasView.descLabel.frame)+CGRectGetHeight(canvasView.callToActionBtn.frame)+CGRectGetHeight(canvasView.callToActionBtn.frame)+CGRectGetHeight(canvasView.dislikeBtn.frame));
    
    // Register UIView with the native ad; the whole UIView will be clickable.
    [canvasView registerClickableViews:@[canvasView.callToActionBtn,
                                         canvasView.titleLabel,
                                         canvasView.descLabel,
                                         canvasView.imageView,
                                         imageView1,
                                         imageView2]];
    
}

@end

#if defined (__cplusplus)
extern "C" {
#endif
void UnionPlatform_ExpressBannersAd_Load(
                                         AdSlotStruct *slot,
                                         ExpressAd_OnLoad onLoad,
                                         ExpressAd_OnLoadError onLoadError,
                                         int context) {
    BUBridgeBannerAd *instance = [[BUBridgeBannerAd alloc] init];
    
    CGFloat newWidth = slot->viewWidth;
    CGFloat newHeight = slot->viewHeight;
    
    BUAdSlot *slot1 = [[BUAdSlot alloc] init];
    slot1.ID = [[NSString alloc] initWithCString:slot->slotId encoding:NSUTF8StringEncoding];
    slot1.mediation.bidNotify = slot->m_bidNotify;
    slot1.mediation.scenarioID = [[NSString alloc] initWithCString:slot->m_cenarioId encoding:NSUTF8StringEncoding];
    slot1.mediation.mutedIfCan = slot->m_isMuted;
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
    
    if (slot->intervalTime > 0) {
        instance.bannerView = [[BUNativeExpressBannerView alloc] initWithSlot:slot1 rootViewController:GetAppController().rootViewController adSize:CGSizeMake(newWidth, newHeight) interval:slot->intervalTime];
    } else {
        instance.bannerView = [[BUNativeExpressBannerView alloc] initWithSlot:slot1 rootViewController:GetAppController().rootViewController adSize:CGSizeMake(newWidth, newHeight)];
    }
    
    if (strlen(slot->autoPlayPolicy) > 0) {
        NSString *str = [[NSString alloc] initWithCString:slot->autoPlayPolicy encoding:NSUTF8StringEncoding];
        NSNumber *num = @(str.integerValue);
        [instance.bannerView.mediation addParam:num withKey:BUMAdAddParamKeyAutoPlayPolicy];
    }
    
    instance.bannerView.frame = CGRectMake(0, CGRectGetHeight([UIScreen mainScreen].bounds)-newHeight, newWidth, newHeight);
    instance.bannerView.delegate = instance;
    instance.onLoad = onLoad;
    instance.onLoadError = onLoadError;
    instance.loadContext = context;
    [instance.bannerView loadAdData];
    
    NSLog(@"CSJM_Unity  banner 设置 adSlot，其中 %@, %@, %@, %@, %@, %@",
          [NSString stringWithFormat:@"slotId-%@ ，",slot1.ID],
          [NSString stringWithFormat:@"bidNotify-%d ，",slot1.mediation.bidNotify],
          [NSString stringWithFormat:@"scenarioID-%@ ，",slot1.mediation.scenarioID],
          [NSString stringWithFormat:@"mutedIfCan-%d ，",slot1.mediation.mutedIfCan],
          [NSString stringWithFormat:@"imgSize-%d-%d ，",slot->width, slot->height],
          [NSString stringWithFormat:@"adSize-%f-%f ，",newWidth, newHeight]
          );
          
    
    (__bridge_retained void*)instance;
}

void UnionPlatform_ExpressBannersAd_SetInteractionListener(
                                                           void* expressAdPtr,
                                                           ExpressAd_RenderSuccess renderSuccess,
                                                           ExpressAd_RenderFailed renderFailed,
                                                           ExpressAd_WillShow willShow,
                                                           ExpressAd_DidClick didClick,
                                                           ExpressAd_DidClose didClose,
                                                           ExpressAd_DidRemove didRemove,
                                                           int context) {
    
    BUBridgeBannerAd *expressBannerAd = (__bridge BUBridgeBannerAd*)expressAdPtr;
    expressBannerAd.renderFailed = renderFailed;
    expressBannerAd.renderSuccess = renderSuccess;
    expressBannerAd.willShow = willShow;
    expressBannerAd.didClick = didClick;
    expressBannerAd.didClose = didClose;
    expressBannerAd.didRemove = didRemove;
    expressBannerAd.listenContext = context;
}

void UnionPlatform_ExpressBannersAd_Show(void* expressAdPtr, float originX, float originY) {
    BUBridgeBannerAd *expressBannerAd = (__bridge BUBridgeBannerAd*)expressAdPtr;
    
    CGFloat newX = originX/[UIScreen mainScreen].scale;
    CGFloat newY = originY/[UIScreen mainScreen].scale;
    
    expressBannerAd.bannerView.frame = CGRectMake(newX, newY, expressBannerAd.bannerView.frame.size.width, expressBannerAd.bannerView.frame.size.height);
    
    [GetAppController().rootViewController.view addSubview:expressBannerAd.bannerView];
}

void UnionPlatform_ExpressBannerAd_Dispose(void* expressAdPtr) {
    if ([[NSThread currentThread] isMainThread]) {
        BUBridgeBannerAd *expressBannerAd = (__bridge BUBridgeBannerAd*)expressAdPtr;
        [expressBannerAd.bannerView removeFromSuperview];
        expressBannerAd.bannerView = nil;
        (__bridge_transfer BUBridgeBannerAd*)expressAdPtr;
    } else {
        dispatch_async(dispatch_get_main_queue(), ^{
            BUBridgeBannerAd *expressBannerAd = (__bridge BUBridgeBannerAd*)expressAdPtr;
            [expressBannerAd.bannerView removeFromSuperview];
            expressBannerAd.bannerView = nil;
            (__bridge_transfer BUBridgeBannerAd*)expressAdPtr;
        });
    }
}

bool UnionPlatform_bannerHaveMediationManager(void * expressAdPtr) {
    BUBridgeBannerAd *expressBannerAd = (__bridge BUBridgeBannerAd*)expressAdPtr;
    return expressBannerAd.bannerView.mediation ? true : false;
}

bool UnionPlatform_bannerMediationisReady(void * expressAdPtr) {
    BUBridgeBannerAd *expressBannerAd = (__bridge BUBridgeBannerAd*)expressAdPtr;
    if (expressBannerAd.bannerView.mediation) {
        return expressBannerAd.bannerView.mediation.isReady;
    } else {
        return false;
    }
}

const char* UnionPlatform_bannerMediationGetShowEcpmInfo(void * expressAdPtr) {
    BUBridgeBannerAd *expressBannerAd = (__bridge BUBridgeBannerAd*)expressAdPtr;
    BUMRitInfo *info = expressBannerAd.bannerView.mediation.getShowEcpmInfo;
    if (info) {
        NSString *infoJson = [BUMRitInfo toJson:info];
        return AutonomousStringCopy_Express([infoJson UTF8String]);
    } else {
        return NULL;
    }
}

const char* UnionPlatform_bannerMediationGetCurrentBestEcpmInfo(void * expressAdPtr) {
    BUBridgeBannerAd *expressBannerAd = (__bridge BUBridgeBannerAd*)expressAdPtr;
    return NULL;
}

const char* UnionPlatform_bannerMediationMultiBiddingEcpmInfos(void * expressAdPtr) {
    BUBridgeBannerAd *expressBannerAd = (__bridge BUBridgeBannerAd*)expressAdPtr;
    return NULL;
}

const char* UnionPlatform_bannerMediationCacheRitList(void * expressAdPtr) {
    BUBridgeBannerAd *expressBannerAd = (__bridge BUBridgeBannerAd*)expressAdPtr;
    NSArray<BUMRitInfo *> * infos = expressBannerAd.bannerView.mediation.cacheRitList;
    if (infos && infos.count > 0) {
        NSMutableArray *muArr = [[NSMutableArray alloc]initWithCapacity:infos.count];
        for (BUMRitInfo *info in infos) {
            NSString *infoJson = [BUMRitInfo toJson:info];
            [muArr addObject:infoJson];
        }
        NSData *jsonData = [NSJSONSerialization dataWithJSONObject:muArr options:NSJSONWritingPrettyPrinted error:nil];
        NSString *strJson = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        return AutonomousStringCopy_Express([strJson UTF8String]);
    } else {
        return NULL;
    }
}

const char* UnionPlatform_bannerMediationGetAdLoadInfoList(void * expressAdPtr) {
    BUBridgeBannerAd *expressBannerAd = (__bridge BUBridgeBannerAd*)expressAdPtr;
    NSArray<BUMAdLoadInfo *> * infos = expressBannerAd.bannerView.mediation.getAdLoadInfoList;
    if (infos && infos.count > 0) {
        NSMutableArray *muArr = [[NSMutableArray alloc]initWithCapacity:infos.count];
        for (BUMAdLoadInfo *info in infos) {
            NSString *infoJson = [BUMAdLoadInfo toJson:info];
            [muArr addObject:infoJson];
        }
        NSData *jsonData = [NSJSONSerialization dataWithJSONObject:muArr options:NSJSONWritingPrettyPrinted error:nil];
        NSString *strJson = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        return AutonomousStringCopy_Express([strJson UTF8String]);
    } else {
        return NULL;
    }
}

void UnionPlatform_bannerMediationDestory(void * expressAdPtr) {
    BUBridgeBannerAd *expressBannerAd = (__bridge BUBridgeBannerAd*)expressAdPtr;
    [expressBannerAd.bannerView.mediation destory];
}

#if defined (__cplusplus)
}
#endif
