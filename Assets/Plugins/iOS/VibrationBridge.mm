#import <Foundation/Foundation.h>
#import <AudioToolbox/AudioToolbox.h>
#import <UIKit/UIKit.h> // 包含 UIKit 以使用 UIImpactFeedbackGenerator

extern "C" {
    void TriggerVibrationWithStyle(int style) {
        if (@available(iOS 10.0, *)) {
            UIImpactFeedbackStyle feedbackStyle;
            switch (style) {
                case 0: feedbackStyle = UIImpactFeedbackStyleLight; break;
                case 1: feedbackStyle = UIImpactFeedbackStyleMedium; break;
                case 2: feedbackStyle = UIImpactFeedbackStyleHeavy; break;
                default: feedbackStyle = UIImpactFeedbackStyleMedium; break;
            }
            UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:feedbackStyle];
            [generator prepare];
            [generator impactOccurred];
        } else {
            AudioServicesPlaySystemSound(kSystemSoundID_Vibrate);
        }
    }

float _GetNativeScaleFactor() {
        // 返回设备的缩放因子（2.0 或 3.0）
        return [UIScreen mainScreen].scale;
    }
}
