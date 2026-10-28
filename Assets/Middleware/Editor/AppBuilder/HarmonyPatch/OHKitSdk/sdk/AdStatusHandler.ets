import { commonEventManager, BusinessError } from '@kit.BasicServicesKit';
import { hilog } from '@kit.PerformanceAnalysisKit';
import { AdType } from './SDKManager';
import emitter from "@ohos.events.emitter";

const KEY_REWARD_DATA = "reward_ad_data";
const KEY_REWARD_STATUS = "reward_ad_status";
const KEY_INTERSTITIAL_STATUS = 'interstitial_ad_status';
let Instance: AdStatusHandler;

export class AdStatusHandler {

  public static getInstance() {
    if (Instance == null) {
      Instance = new AdStatusHandler();
    }
    return Instance;
  }

  private subscriber: commonEventManager.CommonEventSubscriber | null = null;
  private adType: AdType = AdType.Reward;

  // ========== 修复：返回 Promise，确保订阅完成 ==========
  public registerPPSReceiver(type: AdType): Promise<void> {
    if (this.subscriber) {
      this.unRegisterPPSReceiver();
    }
    this.adType = type;
    if (this.adType == AdType.Reward) {
      return this.subscribeRewardAd();
    } else if (this.adType == AdType.Interstitial) {
      return this.subscribeInterstitial();
    } else {
      return Promise.resolve();
    }
  }

  private subscribeRewardAd(): Promise<void> {
    return new Promise((resolve) => {
      const subscribeInfo: commonEventManager.CommonEventSubscribeInfo = {
        events: ['com.huawei.hms.pps.action.PPS_REWARD_STATUS_CHANGED'],
        publisherBundleName: 'com.huawei.hms.adsservice'
      };
      commonEventManager.createSubscriber(subscribeInfo, (err: BusinessError, commonEventSubscriber: commonEventManager.CommonEventSubscriber) => {
        if (err) {
          hilog.error(0x0000, 'SDKKit', `createSubscriber error, code: ${err.code}, message: ${err.message}`);
          resolve();
          return;
        }
        hilog.info(0x0000, 'SDKKit', 'Succeeded in creating subscriber');
        this.subscriber = commonEventSubscriber;
        if (!this.subscriber) {
          hilog.warn(0x0000, 'SDKKit', 'Need create subscriber');
          resolve();
          return;
        }
        commonEventManager.subscribe(this.subscriber, (err: BusinessError, commonEventSubscriber: commonEventManager.CommonEventData) => {
          if (err) {
            hilog.error(0x0000, 'SDKKit', `Subscribe error, code: ${err.code}, message: ${err.message}`);
          } else {
            hilog.info(0x0000, 'SDKKit', 'Subscribe data');
            const status: string = commonEventSubscriber?.parameters?.[KEY_REWARD_STATUS];
            switch (status) {
              case AdStatus.AD_OPEN:
                hilog.info(0x0000, 'SDKKit', 'onAdOpen');
                break;
              case AdStatus.AD_CLICKED:
                hilog.info(0x0000, 'SDKKit', 'onAdClick');
                break;
              case AdStatus.AD_CLOSED:
                hilog.info(0x0000, 'SDKKit', 'onAdClose');
                // 延迟退订，避免丢失后续事件
                setTimeout(() => {
                  this.unRegisterPPSReceiver();
                }, 100);
                break;
              case AdStatus.AD_REWARDED:
                const rewardData: Record<string, string | number> = commonEventSubscriber?.parameters?.[KEY_REWARD_DATA];
                const rewardType: string = rewardData?.rewardType as string;
                const rewardAmount: number = rewardData?.rewardAmount as number;
                hilog.info(0x0000, 'SDKKit', `onAdReward, rewardType: ${rewardType}, rewardAmount: ${rewardAmount}`);
                break;
              case AdStatus.AD_VIDEO_START:
                hilog.info(0x0000, 'SDKKit', 'onAdVideoStart');
                break;
              case AdStatus.AD_COMPLETED:
                hilog.info(0x0000, 'SDKKit', 'onAdCompleted');
                break;
              default:
                break;
            }
            let event: emitter.InnerEvent = {
              eventId: 1,
              priority: emitter.EventPriority.LOW
            };
            let data = new Map<string, Object>();
            data.set("AdType", AdType.Reward);
            data.set("AdStatus", status);
            let eventData: emitter.EventData = { data };
            emitter.emit(event, eventData);
          }
        });
        resolve();
      });
    });
  }

  private subscribeInterstitial(): Promise<void> {
    return new Promise((resolve) => {
      const subscribeInfo: commonEventManager.CommonEventSubscribeInfo = {
        events: ['com.huawei.hms.pps.action.PPS_INTERSTITIAL_STATUS_CHANGED'],
        publisherBundleName: 'com.huawei.hms.adsservice'
      };
      commonEventManager.createSubscriber(subscribeInfo,
        (err: BusinessError, commonEventSubscriber: commonEventManager.CommonEventSubscriber) => {
          if (err) {
            hilog.error(0x0000, 'SDKKit', `CreateSubscriber error, ${err.code}, message: ${err.message}`);
            resolve();
            return;
          }
          hilog.info(0x0000, 'SDKKit', 'Succeeded in creating subscriber');
          this.subscriber = commonEventSubscriber;
          if (!this.subscriber) {
            hilog.warn(0x0000, 'SDKKit', 'Need to create subscriber');
            resolve();
            return;
          }
          commonEventManager.subscribe(this.subscriber,
            (err: BusinessError, commonEventData: commonEventManager.CommonEventData) => {
              if (err) {
                hilog.error(0x0000, 'SDKKit', `Subscribe error, ${err.code}, message: ${err.message}`);
              } else {
                hilog.info(0x0000, 'SDKKit', 'Succeeded subscribing data');
                const status: string = commonEventData?.parameters?.[KEY_INTERSTITIAL_STATUS];
                switch (status) {
                  case AdStatus.AD_OPEN:
                    hilog.info(0x0000, 'testTag', 'onAdOpen');
                    break;
                  case AdStatus.AD_CLICKED:
                    hilog.info(0x0000, 'testTag', 'onAdClick');
                    break;
                  case AdStatus.AD_CLOSED:
                    hilog.info(0x0000, 'testTag', 'Status is onAdClose');
                    setTimeout(() => {
                      this.unRegisterPPSReceiver();
                    }, 100);
                    break;
                  case AdStatus.AD_VIDEO_START:
                    hilog.info(0x0000, 'testTag', 'onAdVideoStart');
                    break;
                  case AdStatus.AD_COMPLETED:
                    hilog.info(0x0000, 'testTag', 'onAdCompleted');
                    break;
                  default:
                    break;
                }
                let event: emitter.InnerEvent = {
                  eventId: 1,
                  priority: emitter.EventPriority.LOW
                };
                let data = new Map<string, Object>();
                data.set("AdType", AdType.Interstitial);
                data.set("AdStatus", status);
                let eventData: emitter.EventData = { data };
                emitter.emit(event, eventData);
              }
            });
          resolve();
        });
    });
  }

  public unRegisterPPSReceiver(): void {
    if (this.subscriber === null || this.subscriber === undefined) {
      hilog.warn(0x0000, 'SDKKit', 'Subscriber is null, skip unsubscribe.');
      return;
    }
    try {
      commonEventManager.unsubscribe(this.subscriber, (err: BusinessError) => {
        if (err) {
          hilog.error(0x0000, 'SDKKit', `Failed to unsubscribe. Code is ${err.code}, message is ${err.message}`);
        } else {
          hilog.info(0x0000, 'SDKKit', 'Succeeded in unsubscribing');
          this.subscriber = null;
        }
      });
    } catch (error) {
      const e: BusinessError = error as BusinessError;
      hilog.error(0x0000, 'SDKKit', `unsubscribe exception. Code is ${e.code}, message is ${e.message}`);
      this.subscriber = null;
    }
  }
}

enum AdStatus {
  AD_LOAD = 'onAdLoad',
  AD_FAIL = 'onAdFail',
  AD_OPEN = 'onAdOpen',
  AD_CLICKED = 'onAdClick',
  AD_CLOSED = 'onAdClose',
  AD_REWARDED = 'onAdReward',
  AD_VIDEO_START = 'onVideoPlayBegin',
  AD_COMPLETED = 'onVideoPlayEnd'
}