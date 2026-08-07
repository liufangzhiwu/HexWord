//
//  BUDPrivacyProvider.h
//  BUDemo
//
//  Created by Willie on 2021/9/9.
//  Copyright © 2021 bytedance. All rights reserved.
//

#import <Foundation/Foundation.h>
#import <BUAdSDK/BUAdSDKConfiguration.h>

NS_ASSUME_NONNULL_BEGIN

@interface BUDPrivacyProvider : NSObject <BUAdSDKPrivacyProvider>

@property (nonatomic, assign)CLLocationDegrees longitude;
@property (nonatomic, assign)CLLocationDegrees latitude;
@property (nonatomic, assign)BOOL canUseLocation;
@property (nonatomic, strong)NSDictionary *privacyConfig;

@end

NS_ASSUME_NONNULL_END
