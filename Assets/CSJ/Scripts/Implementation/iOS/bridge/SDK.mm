#import <BUAdSDK/BUAdSDK.h>
#if defined (__cplusplus)
extern "C" {
#endif

extern const char* AutonomousStringCopy_SDK(const char* string);
const char* AutonomousStringCopy_SDK(const char* string)
{
    if (string == NULL) {
        return NULL;
    }
    
    char* res = (char*)malloc(strlen(string) + 1);
    strcpy(res, string);
    return res;
}

const char* UnionPlatform_PangleGetSDKVersion(){
     NSString *sdkVersion = [BUAdSDKManager SDKVersion];
     return AutonomousStringCopy_SDK([sdkVersion UTF8String]);
}

#if defined (__cplusplus)
}
#endif
