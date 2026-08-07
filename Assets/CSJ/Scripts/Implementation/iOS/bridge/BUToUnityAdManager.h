//
//  BUToUnityAdManager.h
//  Unity-iPhone
//
//  Created by CHAORS on 2020/7/28.
//

#import <Foundation/Foundation.h>
#import <BUAdSDK/BUAdSDK.h>

NS_ASSUME_NONNULL_BEGIN

@interface BUToUnityAdManager : NSObject

+ (BUToUnityAdManager *)sharedInstance;

- (void)addAdManager:(id)adManger;
- (void)deleteAdManager:(id)adManger;

@end

@interface BUMRitInfo (Json)

+ ( NSString * _Nullable )toJson:(BUMRitInfo *)ritInfo;

@end

@interface BUMAdLoadInfo (Json)

+ ( NSString * _Nullable )toJson:(BUMAdLoadInfo *)info;

@end

NS_ASSUME_NONNULL_END
