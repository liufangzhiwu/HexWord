#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <AdSupport/AdSupport.h>

#if __IPHONE_OS_VERSION_MAX_ALLOWED >= 140000
#import <AppTrackingTransparency/AppTrackingTransparency.h>
#endif

// Unity 侧的发消息接口（避免依赖 UnityInterface.h，插件编译更稳）
extern void UnitySendMessage(const char *obj, const char *method, const char *msg);

static const char *kReceiver = "AdTrackingReceiver";
static const char *kCallback = "OnTrackingResult";

// 广告追踪（关联应用）权限桥接
// 语义与 AdTrackingStatus 对齐：0 未决定 / 1 受限 / 2 拒绝 / 3 同意
extern "C" {

    int _AdTracking_GetStatus(void)
    {
        if (@available(iOS 14, *)) {
#if __IPHONE_OS_VERSION_MAX_ALLOWED >= 140000
            return (int)ATTrackingManager.trackingAuthorizationStatus;
#else
            return 3;
#endif
        }
        // iOS 14 以下没有 ATT，IDFA 默认可用
        return 3;
    }

    void _AdTracking_Request(void)
    {
        if (@available(iOS 14, *)) {
#if __IPHONE_OS_VERSION_MAX_ALLOWED >= 140000
            [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
                NSString *value = [NSString stringWithFormat:@"%d", (int)status];
                dispatch_async(dispatch_get_main_queue(), ^{
                    UnitySendMessage(kReceiver, kCallback, [value UTF8String]);
                });
            }];
#endif
        } else {
            UnitySendMessage(kReceiver, kCallback, "3");
        }
    }

    void _AdTracking_OpenSettings(void)
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            NSURL *url = [NSURL URLWithString:UIApplicationOpenSettingsURLString];
            if (url == nil) return;
            UIApplication *app = [UIApplication sharedApplication];
            if ([app respondsToSelector:@selector(openURL:options:completionHandler:)]) {
                [app openURL:url options:@{} completionHandler:nil];
            }
        });
    }
}
