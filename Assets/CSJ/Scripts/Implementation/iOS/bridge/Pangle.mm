#import <BUAdSDK/BUAdSDK.h>
#if defined (__cplusplus)
extern "C" {
#endif
typedef void(*PangleInitializeCallBack)(bool success, const char* message); 

void UnionPlatform_PangleInitializeSDK(PangleInitializeCallBack callback){
    
    NSString *userExtData = [BUAdSDKConfiguration configuration].userExtData;
    NSMutableString *string = [[NSMutableString alloc]initWithString:([userExtData isKindOfClass:[NSString class]] && userExtData.length>2)?userExtData:@"[]"];
    if (string.length > 2) {
        [string insertString:@"," atIndex:string.length-1];
    }
    [string insertString:@"{\"name\":\"unity_version\",\"value\":\"7.6.0.0\"}" atIndex:string.length-1];
    [BUAdSDKConfiguration configuration].userExtData = string.copy;
    NSLog(@"CSJM_Unity  %@",@"触发初始化SDK");
    [BUAdSDKManager startWithSyncCompletionHandler:^(BOOL success, NSError *error) {
        NSLog(@"CSJM_Unity  %@ %d",@"初始化结果",success);
        if (callback) {
            callback(success,[error.userInfo[@"message"]?:@"" cStringUsingEncoding:NSUTF8StringEncoding]);
        }
    }];

}

bool UnionPlatform_PanglIsSdkReady() {
    return [BUAdSDKManager state] == BUAdSDKStateStart;
}

#if defined (__cplusplus)
}
#endif
