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

  static async LoadBanner(adParamJson: string, adOptionsJson: string, displayOptionsJson: string): Promise<Array<string>> {
    let adParam: advertising.AdRequestParams = JSON.parse(adParamJson) as advertising.AdRequestParams;
    let adOptions: advertising.AdOptions = json.parse(adOptionsJson) as advertising.AdOptions;
    let displayOptions: advertising.AdDisplayOptions = json.parse(displayOptionsJson) as advertising.AdDisplayOptions;

    let Msg: string;
    try {
      await SendOAID.RequestOAID();
    } catch (error) {
      let e = error as BusinessError;
      hilog.error(0x0000, 'SDKKit', `Failed to get adsIdentifierInfo: ${e.code}, ${e.message}`);
      Msg=`Failed to get adsIdentifierInfo: ${e.code}, ${e.message}`;
    }


    return new Promise((resolve) => {
      let result: Array<string> = ["LoadOAID", "{\"code\":\"" + 0 + "\",\"message\":\"" + "Load OAID advertising success" + "\"}"];
      resolve(result);
    });

   // SDKManager.ShowAdsWindow();

   /* return new Promise((resolve) => {
      SDKHandler.getInstance().handleBannerLoad(adParam, adOptions, displayOptions, globalThis.AbilityContext).then((data: string) => {
        let result: Array<string> = ["AdsLoad", data];
        resolve(result);
      }).catch((error: BusinessError) => {
        let errorMsg: string = JSON.stringify(error);
        let result: Array<string> = [errorMsg];
        resolve(result);
      });
    });*/
  }

  static async HideBanner(): Promise<Array<string>> {
    AdPlayerPromptActionClass.closeDialog();
    return new Promise((resolve) => {
      let result: Array<string> = ["AdsHide", "{\"code\":\"" + 0 + "\",\"message\":\"" + "hide Banner advertising success" + "\"}"];
      resolve(result);
    });
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
    BannerAd()
    NativeAds()
  }.backgroundColor(Color.Transparent).position({ bottom: 0 })
  .onDisAppear(() => {
    AppStorage.setOrCreate('ShowRollAd', Visibility.None);
  });
}