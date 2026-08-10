//
//  BUToUnityAdManager.m
//  Unity-iPhone
//
//  Created by CHAORS on 2020/7/28.
//

#import "BUToUnityAdManager.h"

#define kBUUnityMaxManagerCount 50


@interface BUToUnityAdManager ()

@property (nonatomic, strong) NSMutableDictionary<NSString *, id> *adManagerMap;

@end


@implementation BUToUnityAdManager

- (void)dealloc {
    
    if (self.adManagerMap.count) {
        [self.adManagerMap removeAllObjects];
        self.adManagerMap = nil;
    }
}

+ (BUToUnityAdManager *)sharedInstance {
    
    static BUToUnityAdManager *sharedInstance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        sharedInstance = [[self alloc] init];
        sharedInstance.adManagerMap = [[NSMutableDictionary alloc] init];
    });
    return sharedInstance;
}

- (void)addAdManager:(id)adManger {
    
    if (!adManger) {
        return;
    }
    
    if (self.adManagerMap.count > kBUUnityMaxManagerCount) {
        [self.adManagerMap removeAllObjects];
    }
    
    [self.adManagerMap setValue:adManger forKey:[NSString stringWithFormat:@"%p", adManger]];
}

- (void)deleteAdManager:(id)adManger {
    
    if (!adManger) {
        return;
    }
    
    [self.adManagerMap removeObjectForKey:[NSString stringWithFormat:@"%p", adManger]];
}



@end




@implementation BUMRitInfo (Json)

+ ( NSString * _Nullable )toJson:(BUMRitInfo *)ritInfo {
    if (ritInfo == nil){
        return nil;
    }
    NSMutableDictionary *dict = [[NSMutableDictionary alloc]init];
    [dict setValue:ritInfo.adnName forKey:@"sdkName"];
    [dict setValue:ritInfo.customAdnName forKey:@"customSdkName"];
    [dict setValue:ritInfo.slotID forKey:@"slotId"];
    [dict setValue:ritInfo.levelTag forKey:@"levelTag"];
    [dict setValue:ritInfo.ecpm forKey:@"ecpm"];
    [dict setValue:[NSNumber numberWithInteger:ritInfo.biddingType] forKey:@"reqBiddingType"];
    [dict setValue:ritInfo.errorMsg forKey:@"errorMsg"];
    [dict setValue:ritInfo.requestID forKey:@"requestId"];
    [dict setValue:ritInfo.adRitType forKey:@"ritType"];
    [dict setValue:ritInfo.segmentId forKey:@"segmentId"];
    [dict setValue:ritInfo.abtestId forKey:@"abTestId"];
    [dict setValue:ritInfo.channel forKey:@"channel"];
    [dict setValue:ritInfo.sub_channel forKey:@"subChannel"];
    [dict setValue:ritInfo.scenarioId forKey:@"scenarioId"];
    NSData *jsonData = [NSJSONSerialization dataWithJSONObject:dict options:NSJSONWritingPrettyPrinted error:nil];
    NSString *strJson = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
    return strJson;
}

@end


@implementation BUMAdLoadInfo (Json)

+ ( NSString * _Nullable )toJson:(BUMAdLoadInfo *)info {
    NSMutableDictionary *dict = [[NSMutableDictionary alloc]init];
    [dict setValue:info.mediationRit forKey:@"mediationRit"];
    [dict setValue:info.adnName forKey:@"adnName"];
    [dict setValue:info.customAdnName forKey:@"customAdnName"];
    [dict setValue:[NSNumber numberWithInteger:info.errCode] forKey:@"errCode"];
    [dict setValue:info.errMsg forKey:@"errMsg"];
    if (info.errUserInfo) {
        NSData *tempJsonData = [NSJSONSerialization dataWithJSONObject:info.errUserInfo options:NSJSONWritingPrettyPrinted error:nil];
        NSString *tempStrJson = [[NSString alloc] initWithData:tempJsonData encoding:NSUTF8StringEncoding];
        if (tempStrJson) {
            [dict setValue:tempStrJson forKey:@"errUserInfo"];
        }
    }
    NSData *jsonData = [NSJSONSerialization dataWithJSONObject:dict options:NSJSONWritingPrettyPrinted error:nil];
    NSString *strJson = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
    return strJson;
}

@end
