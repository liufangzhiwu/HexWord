import { AutoAdComponent, AdComponent, advertising } from '@kit.AdsKit';
import { hilog } from '@kit.PerformanceAnalysisKit';

import { BusinessError, emitter } from '@kit.BasicServicesKit';
import { ComponentContent, window } from '@kit.ArkUI';
import { UIContext } from '@ohos.arkui.UIContext';
import { AdType } from './SDKManager';

enum AdStatus {
  AD_LOAD = 'onAdLoad',
  AD_FAIL = 'onAdFail',
  AD_OPEN = 'onAdOpen',
  AD_CLICKED = 'onAdClick',
  AD_CLOSED = 'onAdClose',
  AD_REWARDED = 'onAdReward',
  AD_VIDEO_START = 'onVideoPlayBegin',
  AD_COMPLETED = 'onVideoPlayEnd',
  MEDIA_PROGRESS = 'onMediaProgress',
  MEDIA_START = 'onMediaStart',
  MEDIA_PAUSE = 'onMediaPause',
  MEDIA_STOP = 'onMediaStop',
  MEDIA_COMPLETE = 'onMediaComplete',
  MEDIA_ERROR = 'onMediaError',
  PORTRAIT = 'onPortrait',
  LANDSCAPE = 'onLandscape',
  MEDIA_COUNTDOWN = 'onMediaCountdown',
  BACK_CLICKED = 'onBackClicked'
}

@Component
export struct SplashAds {
  @State showSplashAd: Visibility = Visibility.None;
  // 请求到的广告内容
  @StorageLink('SplashRequestAds') @Watch('updateSplashAd')
  private adInstance: advertising.Advertisement =
    {
      adType: 1,
      uniqueId: '',
      isFullScreen: false,
      rewarded: false,
      shown: false,
      clicked: false,
      rewardVerifyConfig: new Map
    }
  // 广告展示参数
  @StorageLink("SplashAdDisplayOptions")
  private displayOptions: advertising.AdDisplayOptions = { mute: false };
  @StorageProp("SplashText") text: string = "";
  @StorageProp("SplashSubText") subText: string = "";

  updateSplashAd(propName: string): void {
    hilog.info(0x0000, 'SDKKit', '%{public}s', 'adupdate success');
    hilog.info(0x0000, 'SDKKit', '%{public}s', JSON.stringify(this.adInstance));
    this.showSplashAd = Visibility.Visible;
  }

  build() {
    if (this.showSplashAd == Visibility.Visible) {
      Column() {
        // AdComponent组件用于展示非全屏广告
        AdComponent({
          ads: [this.adInstance], displayOptions: this.displayOptions,
          interactionListener: {
            // 广告状态变化回调
            onStatusChanged: (status: string, ad: advertising.Advertisement, adData: string) => {
              switch (status) {
                case 'onAdOpen':
                  hilog.info(0x0000, 'SDKKit', '%{public}s', 'onAdOpen');
                  break;
                case 'onAdClick':
                  hilog.info(0x0000, 'SDKKit', '%{public}s', 'onAdClick');
                  break;
                case 'onAdClose':
                  hilog.info(0x0000, 'SDKKit', '%{public}s', 'onAdClose');
                  this.showSplashAd = Visibility.None;
                  AdPlayerPromptActionClass.closeDialog()
                  break;
              }
              // 定义一个eventId为1的事件，事件优先级为Low
              let event: emitter.InnerEvent = {
                eventId: 1,
                priority: emitter.EventPriority.LOW
              };

              let data = new Map<string, Object>();
              data.set("AdType", AdType.Splash);
              data.set("AdStatus", status);

              let eventData: emitter.EventData = { data };
              // 发送eventId为1的事件，事件内容为eventData
              emitter.emit(event, eventData);
            }
          }
        }).width('100%').height(this.adInstance.isFullScreen ? '100%' : '87%')
        if (!this.adInstance.isFullScreen) {
          // 展示媒体自定义icon、应用名称、版权信息
          Column({}) {
            Row() {
              Image($r('app.media.ad_splash')).width(24).height(24).margin({ top: 5, right: 8 })
              Text(this.text)
                .fontColor('#1A1A1A')
                .fontSize(16)
                .width(64)
                .height(21)
                .margin({ top: 5 })
            }.margin({ bottom: 8 })

            Column() {
              Text(this.subText).fontColor('#1A1A1A').fontSize(9).height(15)
            }
          }.width('100%').height('13%')
        }

      }.width('100%').height('100%')
    }
  }
}

@Component
export struct BannerAd {
  private ratio: number = 1;
  private adWidth: number = -1;
  private adHeight: number = -1;
  @StorageLink('BannerAdRequestParams') @Watch('updateBanner')
  private adParam: advertising.AdRequestParams = {
    // 广告类型：横幅广告
    adType: 8,
    // App正式发布时需要改为正式的广告位ID
    adId: '',
    // 广告位宽
    adWidth: 360,
    // 广告位高
    adHeight: 57,
    // 开放匿名设备标识符
    oaid: ''
  }
  // 广告展示参数
  @StorageLink('BannerDisplayOptions')
  private adDisplayOptions: advertising.AdDisplayOptions = {
    // 广告轮播的时间间隔，单位ms，取值范围[30000, 120000]
    refreshTime: 30000
  }
  @StorageLink('BannerAdOptions')
  private adOptions: advertising.AdOptions = {
    // 是否允许流量下载0：不允许，1：允许，不设置以广告主设置为准
    allowMobileTraffic: 0,
    // 是否希望根据 COPPA 的规定将您的内容视为面向儿童的内容: -1默认值，不确定 0不希望 1希望
    tagForChildProtection: -1,
    // 是否希望按适合未达到法定承诺年龄的欧洲经济区 (EEA) 用户的方式处理该广告请求： -1默认值，不确定 0不希望 1希望
    tagForUnderAgeOfPromise: -1,
    // 设置广告内容分级上限: W: 3+,所有受众 PI: 7+,家长指导 J:12+,青少年 A: 16+/18+，成人受众
    adContentClassification: 'A'
  };
  @State showBannerAd: Visibility = Visibility.None;

  updateBanner(propName: string): void {
    hilog.info(0x0000, 'SDKKit', '%{public}s', 'adupdate success');
    hilog.info(0x0000, 'SDKKit', '%{public}s', JSON.stringify(this.adParam));
    if (this.adParam?.adWidth && typeof (this.adParam?.adWidth) === 'number' && this.adParam?.adWidth > 0) {
      this.adWidth = this.adParam?.adWidth;
    }
    if (this.adParam?.adHeight && typeof (this.adParam?.adHeight) === 'number' && this.adParam?.adHeight > 0) {
      this.adHeight = this.adParam?.adHeight;
    }
    if (this.adWidth > 0 && this.adHeight > 0) {
      this.ratio = this.adWidth / this.adHeight;
    }
    this.showBannerAd = Visibility.Visible;
  }

  build() {
    Column() {
      if (this.showBannerAd == Visibility.Visible) {
        // AutoAdComponent组件用于展示轮播非全屏广告
        AutoAdComponent({
          adParam: this.adParam,
          adOptions: this.adOptions,
          displayOptions: this.adDisplayOptions,
          interactionListener: {
            // 广告状态变化回调
            onStatusChanged: (status: string, ad: advertising.Advertisement, adData: string) => {
              switch (status) {
                case AdStatus.AD_OPEN:
                  hilog.info(0x0000, 'SDKKit', '%{public}s', 'Status is onAdOpen');
                  break;
                case AdStatus.AD_CLICKED:
                  hilog.info(0x0000, 'SDKKit', '%{public}s', 'Status is onAdClick');
                  break;
                case AdStatus.AD_CLOSED:
                  hilog.info(0x0000, 'SDKKit', '%{public}s', 'Status is onAdClose');
                  AdPlayerPromptActionClass.closeDialog()
                  break;
                case AdStatus.AD_LOAD:
                  hilog.info(0x0000, 'SDKKit', '%{public}s', 'Status is onAdLoad');
                  break;
                case AdStatus.AD_FAIL:
                  hilog.error(0x0000, 'SDKKit', '%{public}s', 'Status is onAdFail');
                  AdPlayerPromptActionClass.closeDialog()
                  break;
              }
              // 定义一个eventId为1的事件，事件优先级为Low
              let event: emitter.InnerEvent = {
                eventId: 1,
                priority: emitter.EventPriority.LOW
              };

              let data = new Map<string, Object>();
              data.set("AdType", AdType.Banner);
              data.set("AdStatus", status);

              let eventData: emitter.EventData = { data };
              // 发送eventId为1的事件，事件内容为eventData
              emitter.emit(event, eventData);
            }
          }
        }).width('100%').aspectRatio(this.ratio)
      }
    }.visibility(this.showBannerAd)
  }
}

@Component
export struct NativeAds {
  private ratio: number = 1;
  @State showNativeAd: Visibility = Visibility.None;
  // 请求到的广告内容
  @StorageLink('NativeRequestAds') @Watch('updateNativeAd')
  private ads: Array<advertising.Advertisement> = [];
  // 广告展示参数
  @StorageLink("NativeAdDisplayOptions")
  private adDisplayOptions: advertising.AdDisplayOptions = {};

  build() {
    Column() {
      List() {
        ForEach(this.ads, (item: advertising.Advertisement) => {
          ListItem() {
            AdComponent({
              ads: [item],
              displayOptions: this.adDisplayOptions,
              interactionListener: {
                onStatusChanged: (status: string, ad: advertising.Advertisement, adData: string) => {
                  switch (status) {
                    case AdStatus.AD_OPEN:
                      hilog.info(0x0000, 'SDKKit', '%{public}s', 'Status is onAdOpen');
                      break;
                    case AdStatus.AD_CLICKED:
                      hilog.info(0x0000, 'SDKKit', '%{public}s', 'Status is onAdClick');
                      break;
                    case AdStatus.AD_CLOSED:
                      hilog.info(0x0000, 'SDKKit', '%{public}s', 'Status is onAdClose');
                      this.showNativeAd = Visibility.None;
                      AdPlayerPromptActionClass.closeDialog()
                      break;
                  }
                  let event: emitter.InnerEvent = {
                    eventId: 1,
                    priority: emitter.EventPriority.LOW
                  };

                  let data = new Map<string, Object>();
                  data.set("AdType", AdType.Native);
                  data.set("AdStatus", status);

                  let eventData: emitter.EventData = { data };
                  emitter.emit(event, eventData);
                }
              }
            })
          }
        })
      }
    }
    .width('100%')
    .height('auto')
    .visibility(this.showNativeAd)
  }

  updateNativeAd(changedPropertyName: string) {
    hilog.info(0x0000, 'SDKKit', '%{public}s', 'adupdate success');
    hilog.info(0x0000, 'SDKKit', '%{public}s', JSON.stringify(this.ads));
    this.showNativeAd = Visibility.Visible;
  }
}

@Component
export struct RollAd {
  // 倒计时文案
  @StorageLink('CountdownText')
  countDownText: string = '';
  @State finalCountDownText : string = "";
  // 贴片广告播放状态
  @State rollPlayState: number = 1;
  // 是否播放正片
  @State isPlayVideo: boolean = false;
  // 视频宽高比
  @State ratio: number = 16 / 9;
  // 请求到的广告内容
  @StorageLink('RollRequestAd')
  private ad: advertising.Advertisement = {
    adType: 60,
    uniqueId: '',//'993c656b-368d-408e-8cca-d038e058a823',
    isFullScreen: false,
    rewarded: false,
    shown: false,
    clicked: false,
    rewardVerifyConfig: new Map
  }
  private ads: Array<advertising.Advertisement> = [this.ad]
  // 广告展示参数
  @StorageLink('RollAdDisplayOptions')
  private adDisplayOptions: advertising.AdDisplayOptions = {
    // 是否静音，默认不静音
    mute: false
  };
  // 已经播放的贴片广告数量
  private playedAdSize: number = 0;

  @StorageLink('ShowRollAd')
  private showRollAd : Visibility = Visibility.None;

  @StorageLink('VideoName')
  private videoName : string = "";

  @StorageLink('ImageName')
  private imageName : string = "";



  build() {
    Stack({ alignContent: Alignment.TopEnd }) {
      if (!this.isPlayVideo) {
        AdComponent({
          ads: this.ads,
          rollPlayState: this.rollPlayState,
          displayOptions: this.adDisplayOptions,
          interactionListener: {
            // 广告状态变化回调
            onStatusChanged: (status: string, ad: advertising.Advertisement, datas: string) => {
              switch (status) {
                case AdStatus.PORTRAIT:
                  hilog.info(0x0000, 'SDKKit', 'Status is onPortrait');
                  break;
                case AdStatus.LANDSCAPE:
                  hilog.info(0x0000, 'SDKKit', 'Status is onLandscape');
                  break;
                case AdStatus.MEDIA_PROGRESS:
                  hilog.info(0x0000, 'SDKKit', 'Status is onMediaProgress');
                  break;
                case AdStatus.MEDIA_START:
                  hilog.info(0x0000, 'SDKKit', 'Status is onMediaStart');
                  break;
                case AdStatus.MEDIA_PAUSE:
                  hilog.info(0x0000, 'SDKKit', 'Status is onMediaPause');
                  break;
                case AdStatus.MEDIA_STOP:
                  hilog.info(0x0000, 'SDKKit', 'Status is onMediaStop');
                  break;
                case AdStatus.MEDIA_COMPLETE:
                  hilog.info(0x0000, 'SDKKit', 'Status is onMediaComplete');
                  // 所有广告都播放完毕后，开始播放正片
                  this.playedAdSize++;
                  if (this.playedAdSize === this.ads.length) {
                    this.isPlayVideo = true;
                  }
                  break;
                case AdStatus.MEDIA_ERROR:
                  hilog.error(0x0000, 'SDKKit', 'Status is onMediaError');
                  break;
                case AdStatus.MEDIA_COUNTDOWN:
                  try {
                    hilog.info(0x0000, 'SDKKit', 'Status is onMediaCountdown');
                    const parseData: Record<string, number> = JSON.parse(JSON.stringify(datas));
                    this.finalCountDownText = this.countDownText.replace('%d', String(parseData.countdownTime));
                  } catch (e) {
                    hilog.error(0x0000, 'SDKKit', `Failed to parse data. Code is ${e.code}, message is ${e.message}`);
                  }
                  break;
                case AdStatus.BACK_CLICKED:
                  hilog.info(0x0000, 'SDKKit', 'Status is onBackClicked');
                  break;
              }
              let event: emitter.InnerEvent = {
                eventId: 1,
                priority: emitter.EventPriority.LOW
              };

              let data = new Map<string, Object>();
              data.set("AdType", AdType.cinema);
              data.set("AdStatus", status);

              let eventData: emitter.EventData = { data };
              emitter.emit(event, eventData);

            }
          }
        })
          .visibility(!this.isPlayVideo ? Visibility.Visible : Visibility.None)
          .width('100%')
          .height('100%')


        Text(this.finalCountDownText)
          .fontSize(12)
          .lineHeight(12)
          .maxLines(1)
          .textAlign(TextAlign.Center)
          .fontColor(Color.White)
          .textOverflow({ overflow: TextOverflow.Ellipsis })
          .backgroundColor('#66000000')
          .border({ radius: 25 })
          .padding(8)
          .margin(16)
          .height(24)
          .onClick(() => {
            hilog.info(0x0000, 'SDKKit', 'CountdownText Clicked, do something...');
            this.isPlayVideo = true;
          })
          .visibility(this.countDownText ? Visibility.Visible : Visibility.None)
      }


      Video({
        src: $rawfile("Data/StreamingAssets/OHSDKKitAdsResource/" + this.videoName),
        previewUri: $rawfile('Data/StreamingAssets/OHSDKKitAdsResource/' + this.imageName),
        controller: new VideoController()
      })
        .visibility(this.isPlayVideo ? Visibility.Visible : Visibility.None)
        .autoPlay(this.isPlayVideo)
        .controls(false)
        .width('100%')
        .height('100%')
    }
    .width('100%')
    .height('100%')
    .aspectRatio(this.ratio)
    .visibility(this.showRollAd)
  }
}


export class AdPlayerPromptActionClass {
  static ctx: UIContext;
  static contentNode: ComponentContent<Object>;
  static options: Object;

  static setContext(context: UIContext) {
    AdPlayerPromptActionClass.ctx = context;
  }

  static setContentNode(contentNode: ComponentContent<Object>) {
    AdPlayerPromptActionClass.contentNode = contentNode;
  }

  static setOptions(options: Object) {
    AdPlayerPromptActionClass.options = options;
  }

  static openDialog() {
    if (AdPlayerPromptActionClass.contentNode !== null) {
      AdPlayerPromptActionClass.ctx.getPromptAction()
        .openCustomDialog(AdPlayerPromptActionClass.contentNode, AdPlayerPromptActionClass.options)
        .then(() => {
          console.info('OpenCustomDialog complete.')
        })
        .catch((error: BusinessError) => {
          let message = (error as BusinessError).message;
          let code = (error as BusinessError).code;
          console.error(`OpenCustomDialog args error code is ${code}, message is ${message}`);
        })
    }
  }

  static closeDialog() {
    if (AdPlayerPromptActionClass.contentNode !== null) {
      AdPlayerPromptActionClass.ctx.getPromptAction()
        .closeCustomDialog(AdPlayerPromptActionClass.contentNode)
        .then(() => {
          console.info('CloseCustomDialog complete.')
        })
        .catch((error: BusinessError) => {
          let message = (error as BusinessError).message;
          let code = (error as BusinessError).code;
          console.error(`CloseCustomDialog args error code is ${code}, message is ${message}`);
        })
    }
  }

  static updateDialog(options: Object) {
    if (AdPlayerPromptActionClass.contentNode !== null) {
      AdPlayerPromptActionClass.ctx.getPromptAction()
        .updateCustomDialog(AdPlayerPromptActionClass.contentNode, options)
        .then(() => {
          console.info('UpdateCustomDialog complete.')
        })
        .catch((error: BusinessError) => {
          let message = (error as BusinessError).message;
          let code = (error as BusinessError).code;
          console.error(`UpdateCustomDialog args error code is ${code}, message is ${message}`);
        })
    }
  }
}