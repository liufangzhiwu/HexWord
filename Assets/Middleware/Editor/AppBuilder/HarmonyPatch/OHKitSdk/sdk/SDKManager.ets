import { SDKHandler } from './SDKHandler'
import { BusinessError, emitter } from '@kit.BasicServicesKit';
import { AdPlayerPromptActionClass, NativeAds, SplashAds, RollAd, BannerAd } from './AdPlayer'
import { hilog } from '@kit.PerformanceAnalysisKit';
import { ComponentContent } from '@kit.ArkUI';
import { advertising, identifier } from '@kit.AdsKit';
import json from '@ohos.util.json';
import { gamePlayer } from '@kit.GameServiceKit';
import { JSON } from '@kit.ArkTS';
import { iap } from '@kit.IAPKit';
import { notificationManager } from '@kit.NotificationKit';
import { AbilityLifecycleCallback, common } from '@kit.AbilityKit';
import { Tuanjie } from 'OHKitSdk/src/main/ets/components/sdk/TuanjieNative';
import { SendOAID } from 'OHKitSdk/src/main/ets/components/sdk/SendOAID';

export enum AdType {
  Splash = 1,
  Native = 3,
  Reward = 7,
  Banner = 8,
  Interstitial = 12,
  cinema = 60,
}

export class SDKManager {

  // ============================================================
  // ★ 新增：UIAbility 生命周期注册标记
  // ============================================================
  private static _lifecycleRegistered: boolean = false;

  static async Login_SDK(_accountInfo: string, _showLoginDialog: boolean, _loginPanelType: number, _forceAuthorization: boolean, _forceLogin: boolean): Promise<Array<string>> {
    return new Promise((resolve) => {
      SDKHandler.getInstance().handleLogin(_accountInfo, _showLoginDialog, _loginPanelType, _forceAuthorization, _forceLogin, globalThis.AbilityContext).then((data: string) => {
        let result: Array<string> = ["Login", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async Logout_SDK(): Promise<Array<string>> {
    return new Promise((resolve) => {
      SDKHandler.getInstance().handleLogout(globalThis.AbilityContext).then((data: string) => {
        let result: Array<string> = ["Logout", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async Login_Verify(thirdUserInfoJson: string): Promise<Array<string>> {
    let request: gamePlayer.ThirdUserInfo = JSON.parse(thirdUserInfoJson) as gamePlayer.ThirdUserInfo
    return new Promise((resolve) => {
      SDKHandler.getInstance().handleVerifyCheck(request, globalThis.AbilityContext).then((data: string) => {
        let result: Array<string> = ["Login_Verify", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async Login_Bind(_thirdOpenId: string, _teamPlayerId: string): Promise<Array<string>> {
    return new Promise((resolve) => {
      SDKHandler.getInstance().handleBindPlayer(_thirdOpenId, _teamPlayerId, globalThis.AbilityContext).then((data: string) => {
        let result: Array<string> = ["Login_Bind", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async Login_UnBind(_thirdOpenId: string, _teamPlayerId: string): Promise<Array<string>> {
    return new Promise((resolve) => {
      SDKHandler.getInstance().handleUnBindPlayer(_thirdOpenId, _teamPlayerId, globalThis.AbilityContext).then((data: string) => {
        let result: Array<string> = ["Login_UnBind", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async GamePlayer_SavePlayerRole(gSKPlayerRoleJson: string): Promise<Array<string>> {
    let gSKPlayerRole: gamePlayer.GSKPlayerRole = JSON.parse(gSKPlayerRoleJson) as gamePlayer.GSKPlayerRole;
    return new Promise((resolve) => {
      SDKHandler.getInstance().handleGamePlayerSave(gSKPlayerRole, globalThis.AbilityContext).then((data: string) => {
        let result: Array<string> = ["SavePlayerRole", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static onPlayerChangedEventCallback(result: gamePlayer.PlayerChangedResult) {
    hilog.info(0x0001, "SDKKit", `onPlayerChangedEventCallback`);
    if (result.event === gamePlayer.PlayerChangedEvent.SWITCH_GAME_ACCOUNT) {
      let event: emitter.InnerEvent = {
        eventId: 2,
        priority: emitter.EventPriority.LOW
      };
      let data = new Map<string, Object>();
      data.set("changedEvent", 0);
      data.set("resultInfo", result.resultInfo);
      let eventData: emitter.EventData = { data };
      emitter.emit(event, eventData);
    }
  }

  static EnablePlayerChangedEvent(): Array<string> {
    try {
      gamePlayer.on('playerChanged', SDKManager.onPlayerChangedEventCallback);
      let resultData = "{\"ReceivedPlayerChangeEvent\":\"1\"}";
      let result: Array<string> = ["PlayerOnOff", resultData];
      return result;
    } catch (error) {
      let err = error as BusinessError;
      hilog.error(0x0000, 'SDKKit', `Failed to EnablePlayerChangedEvent. Code: ${err.code}, message: ${err.message}`);
      let errorMsg: string = JSON.stringify(err);
      let result: Array<string> = [errorMsg];
      return result;
    }
  }

  static DisablePlayerChangedEvent(): Array<string> {
    try {
      gamePlayer.off('playerChanged');
      let resultData = "{\"ReceivedPlayerChangeEvent\":\"0\"}";
      let result: Array<string> = ["PlayerOnOff", resultData];
      return result;
    } catch (error) {
      let err = error as BusinessError;
      hilog.error(0x0000, 'SDKKit', `Failed to DisablePlayerChangedEvent. Code: ${err.code}, message: ${err.message}`);
      let errorMsg: string = JSON.stringify(err);
      let result: Array<string> = [errorMsg];
      return result;
    }
  }

  static async GamePlayer_Init(): Promise<Array<string>> {
    return new Promise((resolve) => {
      SDKHandler.getInstance().handleGamePlayerInit(globalThis.AbilityContext).then((data: string) => {
        let result: Array<string> = ["GamePlayerInit", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async GamePerformance_Init(_bundleName: string, _appVersion: string, _messageType: number): Promise<Array<string>> {
    return new Promise((resolve) => {
      SDKHandler.getInstance().handleGamePerformanceInit(_bundleName, _appVersion, _messageType).then((data: string) => {
        let result: Array<string> = ["GamePerformance_Init", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async GamePerformance_Update(_messageType: number, _extra: string): Promise<Array<string>> {
    return new Promise((resolve) => {
      SDKHandler.getInstance().handleGamePerformanceUpdate(_messageType, _extra).then((data: string) => {
        let result: Array<string> = ["GamePerformance_Update", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async IAP_Init(): Promise<Array<string>> {
    return new Promise((resolve) => {
      SDKHandler.getInstance().handleInitIAP(globalThis.AbilityContext).then((data: string) => {
        let result: Array<string> = ["IAP_Init", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async IAP_QueryProducts(storeType: string, storeIdList: string[]): Promise<Array<string>> {
    return new Promise((resolve) => {
      SDKHandler.getInstance().handleQueryIAPList(storeType, storeIdList, globalThis.AbilityContext).then((data: string) => {
        let result: Array<string> = ["IAP_QueryProducts", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async IAP_Purchase(purchaseParameterJson: string): Promise<Array<string>> {
    let purchaseInfo: iap.PurchaseParameter = JSON.parse(purchaseParameterJson) as iap.PurchaseParameter;
    return new Promise((resolve) => {
      SDKHandler.getInstance().handleStartPurchase(purchaseInfo, globalThis.AbilityContext).then((data: string) => {
        let result: Array<string> = ["IAP_Purchase", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async IAP_ConsumePurchase(finishPurchaseParameterJson: string): Promise<Array<string>> {
    let finishPurchaseInfo: iap.FinishPurchaseParameter = json.parse(finishPurchaseParameterJson) as iap.FinishPurchaseParameter;
    return new Promise((resolve) => {
      SDKHandler.getInstance().handleConsumePurchase(finishPurchaseInfo, globalThis.AbilityContext).then((data: string) => {
        let result: Array<string> = ["IAP_ConsumePurchase", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async IAP_QueryOwnedPurchases(queryPurchasesParameterJson: string): Promise<Array<string>> {
    let purchaseInfo: iap.QueryPurchasesParameter = json.parse(queryPurchasesParameterJson) as iap.QueryPurchasesParameter;
    return new Promise((resolve) => {
      SDKHandler.getInstance().handleCheckOwnedPurchases(purchaseInfo, globalThis.AbilityContext).then((data: string) => {
        let result: Array<string> = ["IAP_QueryOwnedPurchases", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async AdsLoad(adParamJson: string, adOptionsJson: string): Promise<Array<string>> {
    let adParam: advertising.AdRequestParams = JSON.parse(adParamJson) as advertising.AdRequestParams;
    let adOptions: advertising.AdOptions = json.parse(adOptionsJson) as advertising.AdOptions;

   /* try {
      const oaid = await identifier.getOAID();
      if (oaid && oaid !== '00000000-0000-0000-0000-000000000000') {
        adParam.oaid = oaid;
        hilog.info(0x0000, 'SDKKit', `Succeeded in getting adsIdentifierInfo by promise: ${oaid}`);
      } else {
        hilog.warn(0x0000, 'SDKKit', 'OAID is empty or all zero, ad may fail to load');
      }
    } catch (error) {
      let e = error as BusinessError;
      hilog.error(0x0000, 'SDKKit', `Failed to get adsIdentifierInfo: ${e.code}, ${e.message}`);
    }*/

    return new Promise((resolve) => {
      SDKHandler.getInstance().handleAdsLoad(adParam, adOptions, globalThis.AbilityContext).then((data: string) => {
        let result: Array<string> = ["AdsLoad", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  // ============================================================
  // ★ 横幅开关状态（用 AppStorage 共享，BannerAd 拉取失败自动关闭时由组件侧复位）
  // ============================================================
  private static readonly BANNER_SHOWING_KEY: string = 'BannerShowing';

  /**
   * 横幅：取 OAID → 同步参数给页面里的 BannerAd 组件 → 打开 BannerShowing 开关。
   * 也就是说"加载"即"展示"（Unity 侧的 ShowBanner 就调这里），没有单独的 ShowBanner 了。
   */
  static async LoadBanner(adParamJson: string, adOptionsJson: string, displayOptionsJson: string): Promise<Array<string>> {
    // parse Json
    let adParam: advertising.AdRequestParams = JSON.parse(adParamJson) as advertising.AdRequestParams;
    let adOptions: advertising.AdOptions = json.parse(adOptionsJson) as advertising.AdOptions;
    let displayOptions: advertising.AdDisplayOptions = json.parse(displayOptionsJson) as advertising.AdDisplayOptions;

    // ★ 用 await 等 OAID 回来再请求：如果写成 getOAID().then(...)，赋值可能晚于
    //   BannerAd 组件发起广告请求，广告位拿到的还是空 oaid。
    try {
      const data: string = await identifier.getOAID();
      adParam.oaid = data;
      hilog.info(0x0000, 'SDKKit', '%{public}s', `Succeeded in getting adsIdentifierInfo by promise`);
    } catch (error) {
      let e = error as BusinessError;
      hilog.error(0x0000, 'SDKKit', '%{public}s',
        `Failed to get adsIdentifierInfo, code: ${e.code}, message: ${e.message}`);
    }

    // ★ 横幅是页面内组件（entry/pages/Index.ets 的根 Stack 里挂了 BannerAd），
    //   这里只负责同步参数 + 打开开关，不再 ShowAdsWindow：
    //   AutoAdComponent 放进 CustomDialog(ComponentContent) 真机会请求失败。
    return new Promise((resolve) => {
      SDKHandler.getInstance().handleBannerLoad(adParam, adOptions, displayOptions,
        globalThis.AbilityContext as common.UIAbilityContext).then((data: string) => {
        let result: Array<string> = ["AdsLoad", data];
        // 参数先落到 AppStorage，再打开开关，避免组件用上一次的旧 adId 发请求
        AppStorage.setOrCreate(SDKManager.BANNER_SHOWING_KEY, true);
        hilog.info(0x0000, 'SDKKit', `LoadBanner 参数已同步: adId=${adParam?.adId}, adType=${adParam?.adType}`);
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  /**
   * 关闭横幅：只把 BannerShowing 置 false，页面里的 BannerAd 组件会自己隐藏。
   * （横幅不再是对话框，不存在关闭失败的问题）
   */
  static async HideBanner(): Promise<Array<string>> {
    if (AppStorage.get<boolean>(SDKManager.BANNER_SHOWING_KEY) === true) {
      AppStorage.setOrCreate(SDKManager.BANNER_SHOWING_KEY, false);
      hilog.info(0x0000, 'SDKKit', 'HideBanner: 横幅已关闭');
    } else {
      hilog.info(0x0000, 'SDKKit', 'HideBanner: 横幅未在展示，跳过');
    }
    return new Promise((resolve) => {
      let result: Array<string> = ["AdsHide", "{\"code\":\"" + 0 + "\",\"message\":\"" + "hide Banner advertising success" + "\"}"];
      resolve(result);
    });
  }

  /**
   * 请求 APP_TRACKING_CONSENT（广告追踪 / OAID）系统授权框。
   * 弹出时机由 Unity 侧 AdTrackingManager 控制（引导页点"继续"后才调用）。
   * 允许/拒绝都会由 SendOAID 通过 TuanjieSendMessage('PushMessageReceiver',
   * 'OnOAIDClosedSignal', '1'/'0') 回传给 Unity（埋点与关引导页在 Unity 侧统一处理）。
   */
  static async RequestTrackingConsent(): Promise<Array<string>> {
    hilog.info(0x0000, 'SDKKit', 'RequestTrackingConsent: 请求广告追踪授权');

    // 不 await：权限框是模态交互，等玩家点了才 resolve；
    // 结果统一由 SendOAID 走 TuanjieSendMessage('PushMessageReceiver','OnOAIDClosedSignal') 回传。
    // 这里要像其它桥接方法一样返回 Array<string>，否则宿主端会报
    // "Error occurred while calling callback=SDKManager.RequestTrackingConsent"。
    SendOAID.RequestOAID();

    return ["AdsTrackingConsent", "requested"];
  }

  /**
   * 静默查询 APP_TRACKING_CONSENT 当前是否已授予（bug34）。
   * 只读核对：不弹任何系统框（已同意/已拒绝时 requestPermissionsFromUser 都直接返回），
   * 也绝不走 requestPermissionOnSetting 那个带开关的二次授权弹窗。
   * 结果由 SendOAID 经 TuanjieSendMessage('PushMessageReceiver', 'OnOAIDQueryResult', '1'/'0') 回传。
   */
  static async QueryTrackingConsent(): Promise<Array<string>> {
    hilog.info(0x0000, 'SDKKit', 'QueryTrackingConsent: 静默查询广告追踪授权状态');
    return SendOAID.QueryTrackingConsentAndReport();
  }

  static async LoadAdWithMultiSlots(adParamsJson: string, adOptionsJson: string): Promise<Array<string>> {
    let adParams: advertising.AdRequestParams[] = JSON.parse(adParamsJson) as advertising.AdRequestParams[];
    let adOptions: advertising.AdOptions = json.parse(adOptionsJson) as advertising.AdOptions;

   /* try {
      const oaid = await identifier.getOAID();
      if (oaid && oaid !== '00000000-0000-0000-0000-000000000000') {
        for (let i = 0; i < adParams.length; i++) {
          adParams[i].oaid = oaid;
        }
        hilog.info(0x0000, 'SDKKit', `Succeeded in getting adsIdentifierInfo by promise: ${oaid}`);
      } else {
        hilog.warn(0x0000, 'SDKKit', 'OAID is empty or all zero, multi-slots ad may fail to load');
      }
    } catch (error) {
      let e = error as BusinessError;
      hilog.error(0x0000, 'SDKKit', `Failed to get adsIdentifierInfo: ${e.code}, ${e.message}`);
    }*/

    return new Promise((resolve) => {
      SDKHandler.getInstance().handleMultiSlotsAdLoad(adParams, adOptions, globalThis.AbilityContext as common.UIAbilityContext).then((data: string) => {
        let result: Array<string> = ["AdsLoad", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async AdsShow(advertisementJson: string, adDisplayOptionsJson: string): Promise<Array<string>> {
    let ad: advertising.Advertisement = JSON.parse(advertisementJson) as advertising.Advertisement;
    let displayOptions: advertising.AdDisplayOptions = json.parse(adDisplayOptionsJson) as advertising.AdDisplayOptions;

   /* try {
      const oaid = await identifier.getOAID();
      if (oaid && oaid !== '00000000-0000-0000-0000-000000000000') {
        ad.oaid = oaid;
        hilog.info(0x0000, 'SDKKit', `Succeeded in getting adsIdentifierInfo by promise: ${oaid}`);
      } else {
        hilog.warn(0x0000, 'SDKKit', 'OAID is empty or all zero, ad may fail to show');
      }
    } catch (error) {
      let e = error as BusinessError;
      hilog.error(0x0000, 'SDKKit', `Failed to get adsIdentifierInfo: ${e.code}, ${e.message}`);
    }*/

    return new Promise((resolve) => {
      SDKHandler.getInstance().handleAdsShow(ad, displayOptions, globalThis.AbilityContext as common.UIAbilityContext).then((data: string) => {
        let result: Array<string> = ["AdsShow", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async ShowSplashAd(advertisementJson: string, displayOptionsJson: string, text: string, subText: string): Promise<Array<string>> {
    let ad: advertising.Advertisement = JSON.parse(advertisementJson) as advertising.Advertisement;
    let displayOptions: advertising.AdDisplayOptions = json.parse(displayOptionsJson) as advertising.AdDisplayOptions;

    SDKManager.ShowAdsWindow();

    return new Promise((resolve) => {
      SDKHandler.getInstance().handleShowSplashAds(ad, displayOptions, text, subText).then((data: string) => {
        let result: Array<string> = ["AdsShow", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async ShowNativeAds(advertisementsJson: string, displayOptionsJson: string): Promise<Array<string>> {
    let ads: advertising.Advertisement[] = JSON.parse(advertisementsJson) as advertising.Advertisement[];
    let displayOptions: advertising.AdDisplayOptions = json.parse(displayOptionsJson) as advertising.AdDisplayOptions;

    SDKManager.ShowAdsWindow();

    return new Promise((resolve) => {
      SDKHandler.getInstance().handleShowNativeAds(ads, displayOptions).then((data: string) => {
        let result: Array<string> = ["AdsShow", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async ShowRollAd(advertisementsJson: string, displayOptionsJson: string, countdownText: string,
    videoName: string, imageName: string): Promise<Array<string>> {
    let ad: advertising.Advertisement = JSON.parse(advertisementsJson) as advertising.Advertisement;
    let displayOptions: advertising.AdDisplayOptions = json.parse(displayOptionsJson) as advertising.AdDisplayOptions;

    return new Promise((resolve) => {
      SDKHandler.getInstance()
        .handleShowRollAds(ad, displayOptions, countdownText, videoName, imageName)
        .then((data: string) => {
          let result: Array<string> = ["AdsShow", data];
          resolve(result);
          SDKManager.ShowAdsWindow();
          AppStorage.setOrCreate('ShowRollAd', Visibility.Visible);
        })
        .catch((error: BusinessError) => {
          let errorMsg: string = JSON.stringify(error);
          let result: Array<string> = [errorMsg];
          resolve(result);
        });
    });
  }

  static async Push_GetToken(): Promise<Array<string>> {
    return new Promise((resolve) => {
      SDKHandler.getInstance().handlePushGetToken().then((data: string) => {
        let result: Array<string> = ["Push_GetToken", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async RequestEnableNotification(): Promise<Array<string>> {
    return new Promise((resolve) => {
      SDKHandler.getInstance().handleRequestEnableNotification(globalThis.AbilityContext).then((data: string) => {
        let result: Array<string> = ["RequestEnableNotification", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }

  static async PublishNotification(currId: number, title: string, text: string, additionalText: string): Promise<Array<string>> {
    return new Promise((resolve) => {
      SDKHandler.getInstance().handlePublishNotification(currId, title, text, additionalText).then((data: string) => {
        let result: Array<string> = ["PublishNotification", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });
  }


  static async CancelNotification(selectedId: number, currId: number): Promise<Array<string>> {
    return new Promise((resolve) => {
      SDKManager.OpenNotificationSettings();
      /* SDKHandler.getInstance().handleCancelNotification(selectedId, currId).then((data: string) => {
         let result: Array<string> = ["CancelNotification", data];
         resolve(result);
       }).catch((error: BusinessError) => {
         let errorMsg: string = JSON.stringify(error);
         let result: Array<string> = [errorMsg];
         resolve(result);
       });*/
    });
  }

  // ============================================================
  // ★ 打开通知设置面板（走 SDKHandler，会置 _isWaitingForSettingsClosed = true）
  // ============================================================
  static async OpenNotificationSettings(): Promise<Array<string>> {
    return new Promise((resolve) => {
      try {
        const context = getContext() as common.UIAbilityContext;
        if (!context) {
          hilog.error(0x0000, 'SDKKit', 'OpenNotificationSettings: context is null');
          let result: Array<string> = ["OpenNotificationSettings", JSON.stringify({ success: false, errorMsg: 'context null' })];
          resolve(result);
          return;
        }

        hilog.info(0x0000, 'SDKKit', 'OpenNotificationSettings: 通过 SDKHandler 打开');

        // ★ 走 SDKHandler，它会设置 _isWaitingForSettingsClosed = true
        SDKHandler.getInstance().handleOpenNotificationSettings(context).then((data: string) => {
          hilog.info(0x0000, 'SDKKit', 'OpenNotificationSettings result: ' + data);
          let result: Array<string> = ["OpenNotificationSettings", data];
          resolve(result);
        }).catch((error: BusinessError) => {
          hilog.error(0x0000, 'SDKKit',
            `OpenNotificationSettings failed: code=${error.code}, msg=${error.message}`);
          let result: Array<string> = [JSON.stringify(error)];
          resolve(result);
        });
      } catch (error) {
        let err = error as BusinessError;
        hilog.error(0x0000, 'SDKKit',
          `OpenNotificationSettings catch: code=${err.code}, msg=${err.message}`);
        let result: Array<string> = [JSON.stringify(err)];
        resolve(result);
      }
    });
  }


  // ============================================================
  // ★ 通知设置面板关闭（供 UI 组件的 onFocus 调用）
  // ============================================================
  static OnNotificationSettingsClosed(): void {
    hilog.info(0x0000, 'SDKKit', 'OnNotificationSettingsClosed 被 UI 调用');

    SDKHandler.getInstance().handleOnSettingsClosed().then((data: string) => {
      hilog.info(0x0000, 'SDKKit', 'OnNotificationSettingsClosed result: ' + data);
    }).catch((error: BusinessError) => {
      hilog.error(0x0000, 'SDKKit',
        `OnNotificationSettingsClosed failed: code=${error.code}, msg=${error.message}`);
    });
  }

/*  // 在 SDKManager 类中新增
  static async OpenNotificationSettings(): Promise<Array<string>> {
    return new Promise((resolve) => {
      try {
        const context = getContext() as common.UIAbilityContext;
        if (!context) {
          hilog.error(0x0000, 'SDKKit', 'OpenNotificationSettings: context is null');
          let result: Array<string> = ["OpenNotificationSettings", JSON.stringify({ success: false, errorMsg: 'context null' })];
          resolve(result);
          return;
        }

        // ★ 打开设置前先注册生命周期回调
        //SDKManager.RegisterLifecycleOnce(context);

        hilog.info(0x0000, 'SDKKit', 'OpenNotificationSettings: opening...');
        notificationManager.openNotificationSettings(context).then(() =>
        {
          hilog.info(0x0000, 'SDKKit', 'OpenNotificationSettings success');
          let result: Array<string> = ["OpenNotificationSettings", JSON.stringify({ success: true })];
          resolve(result);
        }).catch((error: BusinessError) => {
          hilog.error(0x0000, 'SDKKit', `OpenNotificationSettings failed: code=${error.code}, msg=${error.message}`);
          let errorMsg: string = JSON.stringify(error);
          let result: Array<string> = [errorMsg];
          resolve(result);
        });
      } catch (error) {
        let err = error as BusinessError;
        hilog.error(0x0000, 'SDKKit', `OpenNotificationSettings catch: code=${err.code}, msg=${err.message}`);
        let errorMsg: string = JSON.stringify(err);
        let result: Array<string> = [errorMsg];
        resolve(result);
      }
    });
  }*/


  static ShowAdsWindow() {
    hilog.info(0x0001, "SDKHandler", "Trigger Create Custom Dialog");
    AdPlayerPromptActionClass.setContext(globalThis.UIContext);
    let contentNode: ComponentContent<Object> =
      new ComponentContent(globalThis.UIContext, wrapBuilder(buildAdDialog));
    AdPlayerPromptActionClass.setContentNode(contentNode);
    AdPlayerPromptActionClass.setOptions(Object({
      alignment: DialogAlignment.Center,
    }));
    AdPlayerPromptActionClass.openDialog()
  }
}

@Builder
function buildAdDialog() {
  Column() {
    RollAd()
    SplashAds()
    NativeAds()
  }.backgroundColor(Color.Transparent).position({ bottom: 0 })
  .onDisAppear(() => {
    AppStorage.setOrCreate('ShowRollAd', Visibility.None);
  });
}