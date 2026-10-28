import { authentication } from '@kit.AccountKit';
import { gamePerformance, gamePlayer } from '@kit.GameServiceKit';
import { common } from '@kit.AbilityKit';
import { hilog } from '@kit.PerformanceAnalysisKit';
import { JSON, util } from '@kit.ArkTS';
import { iap } from '@kit.IAPKit';
import { BusinessError, emitter } from '@kit.BasicServicesKit';
import { JWTUtil } from '../utils/JWTUtil';
import { advertising} from '@kit.AdsKit';
import { pushService } from '@kit.PushKit';
import { notificationManager } from '@kit.NotificationKit';
import {AdStatusHandler} from "./AdStatusHandler";
//import { SignalHandler } from './SignalHandler';  // 路径按项目实际调整
import { AdType } from './SDKManager';
import { Tuanjie } from './TuanjieNative';


let Instance: SDKHandler;

export class SDKHandler {


  // ============================================================
  // ★ 通知设置面板状态
  // ============================================================
  /** 是否正在等待通知设置面板关闭 */
  private _isWaitingForSettingsClosed: boolean = false;
  /** 当前通知是否已开启（缓存） */
  private _lastNotificationEnabled: boolean = false;
  /** 打开设置面板的时间戳，用于超时保护 */
  private _settingsOpenedTimestamp: number = 0;
  /** 超时时间（毫秒） */
  private static readonly SETTINGS_TIMEOUT_MS: number = 60000;

  /**
   * 初始化IAP
   */
  public async handleInitIAP(context:common.Context): Promise<string> {
    let ret: string = "{}";
    return new Promise((resolve) => {
      try{
        iap.queryEnvironmentStatus(context as common.UIAbilityContext).then(() => {
          hilog.info(0x0001, "SDKKit", `Iap queryEnvironmentStatus: Success`);
          ret="{\"successMessage\":\"verify Success\"}";
          resolve(ret);
        }).catch((error: BusinessError) => {
          ret = JSON.stringify(error) ;
          resolve(ret);
        });
      }
      catch (e) {
        hilog.error(0x0001, "SDKKit", 'initIap failed: %{public}s', JSON.stringify(e));
        ret = JSON.stringify(e);
        resolve(ret);
      }
    });
  }

  /**
   * 查询商品列表（查询的商品storeIdList是在AppGallery Connect网站配置的商品）
   * productType: iap.ProductType.CONSUMABLE：消耗型商品; iap.ProductType.NONCONSUMABLE：非消耗型商品;
   */
  public async handleQueryIAPList(storeType: string, storeIdList: string[], context : common.Context): Promise<string> {
    const param : iap.QueryProductsParameter = {
      productIds: storeIdList,
      productType: strToProductType(storeType),
    }

    let ret: string = "{}";
    let paramStr=JSON.stringify(param);
    return new Promise((resolve) => {
      iap.queryProducts(context as common.UIAbilityContext, param).then((products) => {
        // 将 products 数组包装成一个对象
        let productsObj:Products = { products: products };
        let productsStr=JSON.stringify(productsObj)
        ret = this.concatJson(paramStr,productsStr);
        resolve(ret);
      }).catch((error: BusinessError) => {
        let errorStr = JSON.stringify(error) ;
        ret = this.concatJson(paramStr,errorStr);
        resolve(ret);
      });
    });
  }

  /**
   * 发起购买
   */
  public async handleStartPurchase(param: iap.PurchaseParameter, context : common.Context): Promise<string> {

    return new Promise((resolve) => {
      let ret: string = "{}";
      iap.createPurchase(context as common.UIAbilityContext, param).then((res) => {
        hilog.info(0x0001, "SDKKit", 'purchase result is' + JSON.stringify(res))
        let jwsPurchaseOrder: string | undefined = (JSON.parse(res.purchaseData) as PurchaseData).jwsPurchaseOrder;
        if(jwsPurchaseOrder)
        {
          let purchaseStr = JWTUtil.decodeJwtObj(jwsPurchaseOrder);
          purchaseStr = purchaseStr.split('\\"').join('"');
          let paramStr=JSON.stringify(param);
          let purchaseOrderPayloadStr="{\"purchaseOrderPayload\": " + purchaseStr + "}";
          ret = this.concatJson(paramStr,purchaseOrderPayloadStr);
          resolve(ret);
        }
        let paramStr=JSON.stringify(param);
        let errStr="{\"code\":\"" + "1001860003" +"\",\"message\":\""+ "Invalid product information." +"\"}";
        ret = this.concatJson(paramStr,errStr);
        resolve(ret);
      }).catch((err: BusinessError) => {
        let errStr=JSON.stringify(err)
        hilog.error(0x0001, "SDKKit", 'purchase err:' +errStr)
        let paramStr=JSON.stringify(param)
        ret = this.concatJson(paramStr,errStr);
        resolve(ret);
      });
    });
  }


  /**
   * 核销购买(purchaseStr为StartPurchase回参内容 或 CheckOwnedPurchases接口查询出来)
   * 对应finish purchase按钮
   */
  public async handleConsumePurchase(finishPurchaseParam: iap.FinishPurchaseParameter, context:common.Context): Promise<string> {
    let ret: string = "{}";
    return new Promise((resolve) => {
      iap.finishPurchase(context as common.UIAbilityContext, finishPurchaseParam, (err) => {
        if (err) {
          hilog.error(0x0001, "SDKKit", `Iap consumePurchase: Error: ${err.code}`);
          ret = JSON.stringify(err) ;
          resolve(ret);
        }
        // 处理成功逻辑
        let paramStr=JSON.stringify(finishPurchaseParam)
        hilog.info(0x0001, "SDKKit", `Iap consumePurchase: data:` + paramStr);
        ret = paramStr;
        resolve(ret);
      });
    });
  }

  /**
   * 查询已购买商品
   */
  public async handleCheckOwnedPurchases(param: iap.QueryPurchasesParameter, context:common.Context): Promise<string> {
    let ret: string = "{}";
    let paramStr=JSON.stringify(param)
    return new Promise((resolve) => {
      iap.queryPurchases(context as common.UIAbilityContext, param).then((res: iap.QueryPurchaseResult) => {
        let purchaseDataList: string[] = res.purchaseDataList;
        if (purchaseDataList === undefined || purchaseDataList.length <= 0) {
          hilog.error(0x0001, "SDKKit", "check owned PurchaseDataList null");
          resolve(ret);
        }
        let purchaseDataArray: string[] = [];
        for (const purchaseData of purchaseDataList) {
          let jwsPurchaseOrder: string | undefined = (JSON.parse(purchaseData) as PurchaseData).jwsPurchaseOrder;
          if(jwsPurchaseOrder)
          {
            let purchaseStr = JWTUtil.decodeJwtObj(jwsPurchaseOrder);
            purchaseStr = purchaseStr.split('\\"').join('"');
            purchaseDataArray.push(purchaseStr);
          }
        }
        // 将purchaseDataArray数组包装成一个对象
        let purchaseList : string = purchaseDataArray.join(",");
        let purchaseListJson : string = "{\"purchaseDataArray\":[" + purchaseList + "]}";
        ret = this.concatJson(paramStr,purchaseListJson);
        resolve(ret);
      }).catch((error: BusinessError) => {
        let errorStr=JSON.stringify(error)
        ret = this.concatJson(paramStr,errorStr);
        hilog.error(0x0000, 'SDKKit', `check purchas info fail: ${ret}`);
        resolve(ret);
      })
    });
  }

  /**
   * 初始化game player service
   */
  public handleGamePlayerInit(context:common.Context): Promise<string> {
    let ret: string = "{}";
    return new Promise((resolve) => {
      gamePlayer.init(context as common.UIAbilityContext).then(() => {
        hilog.info(0x0001, "SDKKit", `Game Service Init Success`);
        ret="{\"successMessage\":\"init Success\"}";
        resolve(ret);
      }).catch((error: BusinessError) => {
        hilog.info(0x0001, "SDKKit", `Game Service init Failed`);
        ret =JSON.stringify(error);
        resolve(ret);
      });
    });
  }

  public async handleLogin(_accountInfo: string, _showLoginDialog: boolean,_loginPanelType:number, _forceAuthorization:boolean,_forceLogin:boolean, context:common.Context): Promise<string> {

    let thirdAccountInfos : Array<gamePlayer.ThirdAccountInfo> = [];

    interface UserAccountInfo {
      accountIcon: string;
      accountName: string;
    }

    if(_accountInfo !== "")
    {
      const param :UserAccountInfo = JSON.parse(_accountInfo) as UserAccountInfo;

      if(param.accountName !== "")
      {
        if(param.accountIcon === "")
          param.accountIcon = 'app.media.icon';

        let thirdAccountInfo1: gamePlayer.ThirdAccountInfo = {
          'accountName': param.accountName, // 游戏官方账号名
          'accountIcon': $r(param.accountIcon)  // 游戏官方账号图标资源信息
        };

        thirdAccountInfos = [thirdAccountInfo1];
      }
    }

    //传入2个时报错
    let request: gamePlayer.UnionLoginParam = {
      showLoginDialog: _showLoginDialog,
      thirdAccountInfos: thirdAccountInfos,
      loginPanelType:_loginPanelType
    };
    return new Promise((resolve) => {
      try {
        gamePlayer.unionLogin(context as common.UIAbilityContext, request).then((result) => {
          let resultStr=JSON.stringify(result);
          hilog.info(0x0000, 'SDKKit', `unionLogin succeeded: ${resultStr}`);
          let accountName = result.accountName;
          let needBinding = result.needBinding;
          if (accountName == "hw_account") {
            if (needBinding) {
              resolve(this.handleLoginByHWNeedBind(_forceAuthorization,context,resultStr));
            } else {
              resolve(this.handleLoginByHW(_forceLogin,context,resultStr));
            }
          } else {
            hilog.info(0x0000, 'SDKKit', `需要开发商自行处理`);
            let ret: string = JSON.stringify(result);
            resolve(ret);
          }
        }).catch((error: BusinessError) => {
          let errorStr=JSON.stringify(error);
          let paramStr=JSON.stringify(request)
          let ret: string = this.concatJson(paramStr,errorStr);
          hilog.error(0x0000, 'SDKKit', `unionLogin failed: ${ret}`);
          resolve(ret);
        });
      } catch (error) {
        hilog.error(0x0000, 'SDKKit', `unionLogin exception: ${JSON.stringify(error)}`);
        let ret: string = JSON.stringify(error);
        resolve(ret);
      }
    });
  }

  /**
   * 登录 且不需绑定
   */
  public async handleLoginByHW(_forceLogin: boolean,context:common.Context,resultStr:string): Promise<string> {
    let ret: string = "{}";
    // 创建授权请求，并设置参数
    let loginRequest = new authentication.HuaweiIDProvider().createLoginWithHuaweiIDRequest();
    // 当用户未登录华为帐号时，是否强制拉起华为帐号登录界面
    loginRequest.forceLogin = _forceLogin;
    loginRequest.state = util.generateRandomUUID();
    //loginRequest.idTokenSignAlgorithm = authentication.IdTokenSignAlgorithm.PS256;
    // 创建登录回调，并处理登录结果
    return new Promise((resolve) => {
      // 执行登录请求
      try {
        let controller = new authentication.AuthenticationController(context as common.UIAbilityContext);
        controller.executeRequest(loginRequest, (err, data) => {
          if (err) {
            hilog.error(0x0001, "SDKKit", 'login fail, error: %{public}s', JSON.stringify(err));
            ret =this.concatJson(resultStr,JSON.stringify(err));
            resolve(ret);
            return;
          }
          let loginWithHuaweiIDResponse = data as authentication.LoginWithHuaweiIDResponse;
          let state = loginWithHuaweiIDResponse.state;
          if (state != undefined && loginRequest.state != state) {
            hilog.error(0x0001, "SDKKit", 'login fail,The state is different: %{public}s', JSON.stringify(loginWithHuaweiIDResponse));
            ret = this.concatJson(resultStr,JSON.stringify(loginWithHuaweiIDResponse));
            resolve(ret);
            return;
          }
          hilog.info(0x0001, "SDKKit", 'login success! ' + JSON.stringify(loginWithHuaweiIDResponse.data));
          ret = this.concatJson(resultStr,JSON.stringify(loginWithHuaweiIDResponse.data));
          ret = "{\"needBinding\" : \"false\"," + ret.substring(1);
          resolve(ret);
        });
      } catch (e) {
        hilog.error(0x0001, "SDKKit", 'login failed: %{public}s', JSON.stringify(e));
        ret = JSON.stringify(e);
        resolve(ret);
      }
    });
  }

  /**
   * 登录 且需绑定
   */
  public async handleLoginByHWNeedBind(_forceAuthorization: boolean,context:common.Context,resultStr:string): Promise<string> {
    let ret: string = "{}";
    // 创建授权请求，并设置参数
    let authRequest = new authentication.HuaweiIDProvider().createAuthorizationWithHuaweiIDRequest();
    // 获取头像昵称需要传如下 scope
    authRequest.scopes = ['profile'];
    // 若开发者需要进行服务端开发，则需传如下 permission 获取 authorizationCode
    authRequest.permissions = ['serviceauthcode'];
    // 用户是否需要登录授权，该值为 true 且用户未登录或未授权时，会拉起用户登录或授权页面
    authRequest.forceAuthorization = _forceAuthorization;
    authRequest.state = util.generateRandomUUID();
    // 创建登录回调，并处理登录结果
    return new Promise((resolve) => {
      // 执行登录请求
      try {
        let controller = new authentication.AuthenticationController(context as common.UIAbilityContext);
        controller.executeRequest(authRequest, (err, data) => {
          if (err) {
            let errStr=JSON.stringify(err);
            hilog.error(0x0001, "SDKKit", 'login fail, error: %{public}s',errStr);
            ret = this.concatJson(resultStr,errStr);
            return;
          }
          let authorizationWithHuaweiIDResponse = data as authentication.AuthorizationWithHuaweiIDResponse;
          let state = authorizationWithHuaweiIDResponse.state;
          if (state != undefined && authRequest.state != state) {
            hilog.error(0x0000, 'LoginByHWNeedBing', `auth failed, state is different:${authorizationWithHuaweiIDResponse}`);
            return;
          }
          hilog.debug(0x0000, 'LoginByHWNeedBing', `auth succeeded:${authorizationWithHuaweiIDResponse}`);
          if (state != undefined && authRequest.state != state) {
            hilog.error(0x0001, "SDKKit", 'login fail,The state is different: %{public}s', JSON.stringify(authorizationWithHuaweiIDResponse));
            ret = this.concatJson(resultStr,JSON.stringify(authorizationWithHuaweiIDResponse.data));
            resolve(ret);
            return;
          }
          hilog.info(0x0001, "SDKKit", 'login with need bind success!');
          ret = this.concatJson(resultStr,JSON.stringify(authorizationWithHuaweiIDResponse.data));
          ret = "{\"needBinding\" : \"true\"," + ret.substring(1);
          resolve(ret);
        });
      } catch (e) {
        hilog.error(0x0001, "SDKKit", 'login failed: %{public}s', JSON.stringify(e));
        ret = JSON.stringify(e);
        resolve(ret);
      }
    });
  }

  public async handleVerifyCheck(request: gamePlayer.ThirdUserInfo, context:common.Context): Promise<string> {
    let ret: string = "{}";
    return new Promise((resolve) => {
      try {
        gamePlayer.verifyLocalPlayer(context as common.UIAbilityContext, request).then(() => {
          hilog.info(0x0000, 'SDKKit', `verifyLocalPlayer succeeded`);
          ret = JSON.stringify(request);
          resolve(ret);
        }).catch((error: BusinessError) => {
          hilog.error(0x0000, 'SDKKit', `verifyLocalPlayer failed: ${JSON.stringify(error)}`);
          ret = JSON.stringify(error);
          resolve(ret);
        });
      } catch (error) {
        hilog.error(0x0000, 'SDKKit', `verifyLocalPlayer exception: ${JSON.stringify(error)}`);
        ret = JSON.stringify(error);
        resolve(ret);
      }
    });
  }

  public async handleBindPlayer(_thirdOpenId: string, _teamPlayerId: string, context:common.Context): Promise<string> {
    let ret: string = "{}";
    return new Promise((resolve) => {
      let thirdOpenId = _thirdOpenId; // thirdOpenId 表示游戏官方账号 ID
      let teamPlayerId = _teamPlayerId; // teamPlayerId 表示玩家华为账号对应的 teamPlayerId
      const param :Record<string,string> = {
        "thirdOpenId": thirdOpenId,
        "teamPlayerId" : teamPlayerId
      }
      let paramStr=JSON.stringify(param)
      try {
        gamePlayer.bindPlayer(context as common.UIAbilityContext, thirdOpenId, teamPlayerId).then(() => {
          hilog.info(0x0000, 'SDKKit', `bindPlayer succeeded:`);
          ret = paramStr;
          resolve(ret);
        }).catch((error: BusinessError) => {
          let errorStr=JSON.stringify(error)
          ret = this.concatJson(paramStr,errorStr);
          hilog.error(0x0000, 'SDKKit', `bindPlayer failed: ${ret}`);
          resolve(ret);
        });
      } catch (error) {
        hilog.error(0x0000, 'SDKKit', `bindPlayer failed: ${JSON.stringify(error)}`);
        resolve(ret);
      }
    });
  }

  public async handleUnBindPlayer(_thirdOpenId: string, _teamPlayerId: string,context:common.Context): Promise<string> {
    let ret: string = "{}";
    return new Promise((resolve) => {
      let thirdOpenId =_thirdOpenId ; // thirdOpenId 表示游戏官方账号 ID
      let teamPlayerId = _teamPlayerId; // teamPlayerId 表示玩家华为账号对应的 teamPlayerId
      const param : Record<string,string> = {
        "thirdOpenId": thirdOpenId,
        "teamPlayerId":teamPlayerId
      }
      let paramStr=JSON.stringify(param)
      try {
        gamePlayer.unbindPlayer(context as common.UIAbilityContext, thirdOpenId, teamPlayerId).then(() => {
          hilog.info(0x0000, 'SDKKit', `unbindPlayer succeeded:`);
          ret = paramStr;
          resolve(ret);
        }).catch((error: BusinessError) => {
          let errorStr=JSON.stringify(error)
          ret = this.concatJson(paramStr,errorStr);
          hilog.error(0x0000, 'SDKKit', `unbindPlayer failed: ${ret}`);
          resolve(ret);
        });
      } catch (error) {
        hilog.error(0x0000, 'SDKKit', `unbindPlayer failed: ${JSON.stringify(error)}`);
        ret = JSON.stringify(error).substring(1);
        resolve(ret);
      }
    });
  }

  /**
   * 登出
   */
  public async handleLogout(context:common.Context): Promise<string> {
    let ret: string = "{}";
    // 创建取消授权请求，并设置参数
    let cancelRequest = new authentication.HuaweiIDProvider().createCancelAuthorizationRequest();
    cancelRequest.state = util.generateRandomUUID();
    return new Promise((resolve) => {
      // 执行取消授权请求，并处理结果
      try {
        let controller = new authentication.AuthenticationController(context as common.UIAbilityContext);
        controller.executeRequest(cancelRequest, (err, data) => {
          if (err) {
            hilog.error(0x0000, 'SDKKit', 'cancel fail,error: %{public}s', JSON.stringify(err));
            ret = JSON.stringify(err);
            resolve(ret);
            return;
          }
          let cancelAuthorizationResponse = data as authentication.CancelAuthorizationResponse;
          let state = cancelAuthorizationResponse.state;
          if (state != undefined && cancelRequest.state != state) {
            hilog.error(0x0000, 'SDKKit', 'cancel fail,The state is different: %{public}s',
              JSON.stringify(cancelAuthorizationResponse));
            ret = JSON.stringify(cancelAuthorizationResponse);
            resolve(ret);
            return;
          }
          hilog.debug(0x0000, 'SDKKit', 'cancel success: %{public}s', JSON.stringify(cancelAuthorizationResponse));
          ret = JSON.stringify(cancelAuthorizationResponse);
          resolve(ret);
        });
      } catch (error) {
        hilog.error(0x0000, 'SDKKit', 'cancel failed: %{public}s', JSON.stringify(error));
        ret = JSON.stringify(error);
        resolve(ret);
      }
    });
  }

  public async handleGamePlayerSave(gSKPlayerRole:gamePlayer.GSKPlayerRole , context:common.Context): Promise<string> {
    let ret: string = "{}";
    let requestStr=JSON.stringify(gSKPlayerRole);
    hilog.info(0x0001, "SDKKit", `Trigger GamePlayerSave Success ` + requestStr);
    return new Promise((resolve) => {
      gamePlayer.savePlayerRole(context as common.UIAbilityContext, gSKPlayerRole).then(() => {
        hilog.info(0x0001, "Game Service Demo", `Game Service Save Success`);
        ret = requestStr;
        resolve(ret);
      }).catch((error: BusinessError) => {
        hilog.info(0x0001, "SDKKit", `Game Service Failed`);
        let errorStr=JSON.stringify(error)
        ret =this.concatJson(requestStr,errorStr);
        resolve(ret);
      });
    });
  }


  public async handleGamePerformanceInit(_bundleName:string, _appVersion:string,_messageType:number):Promise<string>{
    let ret: string = "{}";
    let request:gamePerformance.GamePackageInfo={
      bundleName:_bundleName,
      appVersion:_appVersion,
      messageType:_messageType
    }
    let requestStr=JSON.stringify(request);
    hilog.info(0x0001, "SDKKit", `Trigger performance init Success`);
    return new Promise((resolve) => {
      try {
        gamePerformance.init(request).then(() => {
          hilog.info(0x0001, "SDKKit", `Game performance init Success`);
          ret = requestStr;
          resolve(ret);
        }).catch((error: BusinessError) => {
          hilog.info(0x0001, "SDKKit", `Game performance init Failed`);
          let errorStr=JSON.stringify(error)
          ret =this.concatJson(requestStr,errorStr);
          resolve(ret);
        });
      } catch (err) {
        hilog.error(0x0000, 'SDKKit', '%{public}s', ` catch error: ${err.code} ${err.message}`);
        ret=this.concatJson(requestStr,"{\"code\":\"" + err.code +"\",\"message\":\""+err.message+"\"}");
        resolve(ret)
      }

    });
  }

  public async handleGamePerformanceUpdate(_messageType:number,_extra:string):Promise<string>{
    let ret: string = "{}";
    let request:gamePerformance.BaseGameInfo={
      messageType:_messageType,
      extra:""
    }
    switch(_messageType){
      case 0:
        request = JSON.parse(_extra) as gamePerformance.GamePackageInfo;
        break;
      case 1:
        request = JSON.parse(_extra) as gamePerformance.GameConfigInfo;
        (request as gamePerformance.GameConfigInfo).currentPictureQualityLevel=1;//in arkTs,"currentPictureQualityLevel"missing last letter "l"
        break;
      case 2:
        request = JSON.parse(_extra) as gamePerformance.GameSceneInfo;
        break;
      case 3:
        request = JSON.parse(_extra) as gamePerformance.GameNetInfo;
        break;
      case 4:
        request = JSON.parse(_extra) as gamePerformance.GamePlayerInfo;
        break;
    }
    let requestStr=JSON.stringify(request);
    hilog.debug(0x0000,"SDKKit",requestStr);
    return new Promise((resolve) => {
      try {
        gamePerformance.updateGameInfo(request).then(() => {
          hilog.info(0x0001, "SDKKit", `Game performance update Success`);
          ret = requestStr;
          resolve(ret);
        }).catch((error: BusinessError) => {
          hilog.info(0x0001, "SDKKit", `Game performance update Failed`);
          let errorStr=JSON.stringify(error)
          ret =this.concatJson(requestStr,errorStr);
          resolve(ret);
        });
      } catch (err) {
        hilog.error(0x0000, 'SDKKit', '%{public}s', ` catch error: ${err.code} ${err.message}`);
        ret=this.concatJson(requestStr,"{\"code\":\"" + err.code +"\",\"message\":\""+err.message+"\"}");
        resolve(ret)
      }
    });
  }

  public async handleAdsLoad(adParam:advertising.AdRequestParams,adOptions:advertising.AdOptions,context:common.Context):Promise<string>{
    let returnAds:string;
    return new Promise((resolve) => {
      // 广告请求回调监听
      const adLoaderListener: advertising.AdLoadListener = {
        // 广告请求失败回调
        onAdLoadFailure: (errorCode: number, errorMsg: string) => {
          hilog.error(0x0000, 'SDKKit', '%{public}s',
            `Request single ad errorCode is: ${errorCode}, errorMsg is: ${errorMsg}`);
          returnAds = "{\"code\":\"" + errorCode + "\",\"message\":\"" + errorMsg + "\"}";
          resolve(returnAds);
        },
        // 广告请求成功回调
        onAdLoadSuccess: (ads: Array<advertising.Advertisement>) => {
          hilog.info(0x0000, 'SDKKit', '%{public}s', 'Request single ad succeed! ' + JSON.stringify(ads));
          // 将 ads 数组包装成一个对象
          let adsObj:adsArray = {ads: ads};
          returnAds=JSON.stringify(adsObj)
          resolve(returnAds);
        }
      };
      const load: advertising.AdLoader = new advertising.AdLoader(context);
      load.loadAd(adParam, adOptions, adLoaderListener);
    });
  }

  public async handleBannerLoad(adParam:advertising.AdRequestParams,adOptions:advertising.AdOptions,displayOptions:advertising.AdDisplayOptions,context:common.Context):Promise<string>{
    //把广告参数传到组件
    AppStorage.setOrCreate('BannerDisplayOptions',displayOptions);
    AppStorage.setOrCreate('BannerAdOptions',adOptions);
    AppStorage.setOrCreate('BannerAdRequestParams',adParam);
    let returnAds:string
    returnAds = "{\"code\":\"" + 0 + "\",\"message\":\"" + "load Banner advertising success"  + "\"}";
    return returnAds;

  }

  public async handleMultiSlotsAdLoad(adParamArray: advertising.AdRequestParams[], adOptions: advertising.AdOptions,
    context: common.UIAbilityContext): Promise<string> {
    let result: string;
    return new Promise((resolve) => {
      const multiSlotsAdLoaderListener: advertising.MultiSlotsAdLoadListener = {
        // 广告请求失败回调
        onAdLoadFailure: (errorCode: number, errorMsg: string) => {
          hilog.error(0x0000, 'testTag', `Failed to load multiSlots ad. Code is ${errorCode}, message is ${errorMsg}`);
          result = "{\"code\":\"" + errorCode + "\",\"message\":\"" + errorMsg + "\"}";
          resolve(result);
        },
        // 广告请求成功回调
        onAdLoadSuccess: (ads: Map<string, Array<advertising.Advertisement>>) => {
          hilog.info(0x0000, 'testTag', 'Succeeded in loading multiSlots ad');
          const returnAds: Array<advertising.Advertisement> = [];
          ads.forEach((adsArray) => returnAds.push(...adsArray));
          let adsObj: adsArray = { ads: returnAds };
          let result = JSON.stringify(adsObj)
          resolve(result);
        }
      }
      // 创建AdLoader广告对象
      const adLoader: advertising.AdLoader = new advertising.AdLoader(context);
      // 调用广告请求接口
      adLoader.loadAdWithMultiSlots(adParamArray, adOptions, multiSlotsAdLoaderListener);
    });
  }

  // ============================================================
  // ★ handleAdsShow：await 订阅完成后再 showAd
  // ============================================================
  public async handleAdsShow(ad: advertising.Advertisement, displayOptions: advertising.AdDisplayOptions, context: common.UIAbilityContext) {
    const startTime = Date.now();
    const adsStr = JSON.stringify(ad);

    hilog.info(0x0000, 'SDKKit',`========== handleAdsShow 开始 ==========`);
    hilog.info(0x0000, 'SDKKit',`handleAdsShow ts=${startTime}`);
    hilog.info(0x0000, 'SDKKit',`handleAdsShow ad=${adsStr}`);
    hilog.info(0x0000, 'SDKKit',`handleAdsShow displayOptions=${JSON.stringify(displayOptions)}`);
    hilog.info(0x0000, 'SDKKit',`handleAdsShow ad字段: adType=${ad?.adType}, uniqueId=${ad?.uniqueId}, ` +
      `isFullScreen=${ad?.isFullScreen}, rewarded=${ad?.rewarded}, ` +
      `shown=${ad?.shown}, clicked=${ad?.clicked}`);

    let ret: string = "{}";

    return new Promise<string>(async (resolve) => {
      try {
        hilog.info(0x0000, 'SDKKit', 'Request single ad is' + adsStr);

        // 等待订阅完成
        await AdStatusHandler.getInstance().registerPPSReceiver(ad.adType);
        hilog.info(0x0000, 'SDKKit',`handleAdsShow registerPPSReceiver 完成, adType=${ad.adType}, 耗时=${Date.now() - startTime}ms`);

        advertising.showAd(ad, displayOptions, context);
        hilog.info(0x0000, 'SDKKit',`handleAdsShow showAd 调用成功, 耗时=${Date.now() - startTime}ms`);
        hilog.info(0x0000, 'SDKKit',`handleAdsShow ⚠ 注意：showAd 调用成功仅表示已提交展示请求，不代表广告已出画面。` +
          `后续请关注 AdComponent onStatusChanged: onAdOpen / onMediaStart / onMediaError / onAdFail`);

        ret = adsStr;
        resolve(ret);
      } catch (err) {
        hilog.error(0x0000, 'SDKKit', `Show ad catch error: ${err.code} ${err.message}`);
        ret = this.concatJson(adsStr, "{\"code\":\"" + err.code + "\",\"message\":\"" + err.message + "\"}");
        resolve(ret);
      }
      hilog.info(0x0000, 'SDKKit',`========== handleAdsShow 结束 ==========`);
    });
  }

  public async handleShowSplashAds(ad:advertising.Advertisement,displayOptions:advertising.AdDisplayOptions,text:string,subText:string){
    //把广告参数传到组件
    AppStorage.setOrCreate('SplashText',text);
    AppStorage.setOrCreate('SplashSubText',subText);
    AppStorage.setOrCreate('SplashAdDisplayOptions',displayOptions);
    AppStorage.setOrCreate('SplashRequestAds',ad);

    let returnAds:string
    returnAds = "{\"code\":\"" + 0 + "\",\"message\":\"" + "Show Splash Ads success"  + "\"}";
    return returnAds;
  }

  public async handleShowNativeAds(ads: advertising.Advertisement[], displayOptions: advertising.AdDisplayOptions) {
    //把广告参数传到组件
    AppStorage.setOrCreate('NativeRequestAds', ads);
    AppStorage.setOrCreate('NativeAdDisplayOptions', displayOptions);
    let returnAds: string
    returnAds = "{\"code\":\"" + 0 + "\",\"message\":\"" + "Show Native Ads success" + "\"}";
    return returnAds;
  }

  public async handleShowRollAds(ad: advertising.Advertisement, displayOptions: advertising.AdDisplayOptions,
    countdownText: string, videoName: string, imageName: string) {
    //把广告参数传到组件
    AppStorage.setOrCreate('RollRequestAd', ad);
    AppStorage.setOrCreate('CountdownText', countdownText);
    AppStorage.setOrCreate('RollAdDisplayOptions', displayOptions);
    AppStorage.setOrCreate('VideoName', videoName);
    AppStorage.setOrCreate('ImageName', imageName);
    let returnAds: string
    returnAds = "{\"code\":\"" + 0 + "\",\"message\":\"" + "Show Roll Ads success" + "\"}";
    return returnAds;
  }


  public async handleRequestEnableNotification(context:common.UIAbilityContext) :Promise<string> {
    let ret: string = "{}";

    this._isWaitingForSettingsClosed = true;
    this._settingsOpenedTimestamp = Date.now();

    return new Promise((resolve) =>
    {
      try {
        notificationManager.requestEnableNotification(context).then(() => {

          hilog.info(0x0000, 'SDKKit', 'handleRequestEnableNotification');
          resolve(ret);
        }).catch((err: BusinessError) => {
          hilog.error(0x0000, 'SDKKit', 'Failed to handleRequestEnableNotification: %{public}d %{public}s', err.code, err.message);
          ret= "{\"code\":\"" + err.code +"\",\"message\":\""+err.message+"\"}";
          resolve(ret);
        });
      } catch (err) {
        let e = err as BusinessError;
        hilog.error(0x0000, 'SDKKit', 'Failed to handleRequestEnableNotification: %{public}d %{public}s', e.code, e.message);
      }
    });
  }

  public async handlePushGetToken():Promise<string> {
    let ret: string = "{}";
    return new Promise((resolve) =>
    {
      try {
        pushService.getToken().then((data: string) => {
          hilog.info(0x0000, 'SDKKit', 'Succeeded in getting push token.');
          hilog.info(0x0000, 'SDKKit', data);
          const param :Record<string,string> = {
            "pushToken": data,
          }
          ret = JSON.stringify(param);

          hilog.info(0x0000, 'SDKKit', ret);
          resolve(ret);
        }).catch((err: BusinessError) => {
          hilog.error(0x0000, 'SDKKit', 'Failed to get push token: %{public}d %{public}s', err.code, err.message);
          ret=JSON.stringify(err);
          resolve(ret);
        });
      } catch (err) {
        let e: BusinessError = err as BusinessError;
        hilog.error(0x0000, 'SDKKit', 'Failed to get push token: %{public}d %{public}s', e.code, e.message);
        ret=JSON.stringify(err);
        resolve(ret);
      }
    });
  }

  public async handlePublishNotification(id:number,title:string,text:string,additionalText:string){
    let ret: string = "{}";
    let param:notificationManager.NotificationRequest={
      id:id,
      content: {
        notificationContentType: notificationManager.ContentType.NOTIFICATION_CONTENT_BASIC_TEXT,
        normal: {
          title: title,
          text: text,
          additionalText:additionalText
        }
      }
    }
    return new Promise<string>((resolve) =>
    {
      try{
        notificationManager.publish(param).then(() => {
          hilog.info(0x0000, 'SDKKit', 'push content success');
          notificationManager.setBadgeNumber(id+1)//id start from 0
          ret="{\"successMessage\":\"Publish Success\"}";
          resolve(ret);
        }).catch((err: BusinessError) => {
          console.error(`publish fail: ${JSON.stringify(err)}`);
          ret= "{\"code\":\"" + err.code +"\",\"message\":\""+err.message+"\"}";
          resolve(ret);
        });
      } catch (err) {
        ret= "{\"code\":\"" + err.code +"\",\"message\":\""+err.message+"\"}";
        resolve(ret);
      }
    });
  }

  public async handleCancelNotification(selectedId:number,currId: number){
    let ret: string = "{}";
    hilog.info(0x0000, 'SDKKit',`${selectedId}`);
    // cancel回调
    return new Promise<string>((resolve) => {
      let cancelCallback = (err: BusinessError): void => {
        if (err) {
          hilog.info(0x0000, 'SDKKit', `cancel failed, code is ${err.code}, message is ${err.message}`);
          ret= "{\"code\":\"" + err.code +"\",\"message\":\""+err.message+"\"}";
          resolve(ret);
        } else {
          hilog.info(0x0000, 'SDKKit', 'cancel success');
          ret="{\"successMessage\":\"verify Success\"}";
          currId--;
          notificationManager.setBadgeNumber(currId)
          resolve(ret);
        }
      }
      notificationManager.cancel(selectedId , cancelCallback);
    });
  }


  // ============================================================
  // ★ 打开系统通知设置面板
  // ============================================================
  public async handleOpenNotificationSettings(context: common.UIAbilityContext): Promise<string> {
    let ret: string = "{}";
    return new Promise<string>((resolve) => {
      try {
        this._isWaitingForSettingsClosed = true;
        this._settingsOpenedTimestamp = Date.now();

        hilog.info(0x0000, 'SDKKit', '[Settings] openNotificationSettings 调用');

        notificationManager.openNotificationSettings(context).then(() => {
          hilog.info(0x0000, 'SDKKit', '[Settings] openNotificationSettings 成功');
          ret = "{\"success\":true}";
          resolve(ret);
        }).catch((err: BusinessError) => {
          hilog.error(0x0000, 'SDKKit',
            `[Settings] openNotificationSettings 失败: ${err.code}, ${err.message}`);
          this._isWaitingForSettingsClosed = false;
          ret = "{\"code\":\"" + err.code + "\",\"message\":\"" + err.message + "\"}";
          resolve(ret);
        });
      } catch (err) {
        let e = err as BusinessError;
        hilog.error(0x0000, 'SDKKit',
          `[Settings] openNotificationSettings 异常: ${e.code}, ${e.message}`);
        this._isWaitingForSettingsClosed = false;
        ret = "{\"code\":\"" + e.code + "\",\"message\":\"" + e.message + "\"}";
        resolve(ret);
      }
    });
  }

  // ============================================================
  // ★ 查询通知是否开启
  // ============================================================
  public async handleCheckNotificationEnabled(): Promise<string> {
    let ret: string = "{}";
    return new Promise<string>((resolve) => {
      notificationManager.isNotificationEnabled().then((enabled: boolean) => {
        this._lastNotificationEnabled = enabled;
        hilog.info(0x0000, 'SDKKit', `[Settings] isNotificationEnabled = ${enabled}`);
        ret = "{\"enabled\":" + (enabled ? 1 : 0) + "}";
        resolve(ret);
      }).catch((err: BusinessError) => {
        hilog.error(0x0000, 'SDKKit',
          `[Settings] isNotificationEnabled 失败: ${err.code}, ${err.message}`);
        ret = "{\"code\":\"" + err.code + "\",\"message\":\"" + err.message + "\"}";
        resolve(ret);
      });
    });
  }

  // ============================================================
  // ★ 通知设置面板关闭回调（由 UI 的 onFocus 触发）
  // 检测到用户从设置返回后，主动推送 Signal 给 Unity
  // ============================================================
  public async handleOnSettingsClosed(): Promise<string> {
    let ret: string = "{}";

    // 不是从设置返回，忽略
    if (!this._isWaitingForSettingsClosed) {
      hilog.info(0x0000, 'SDKKit', '[Settings] 非设置返回，忽略 onFocus');
      ret = "{\"ignored\":true}";
      return ret;
    }

    // 超时保护
    const elapsed = Date.now() - this._settingsOpenedTimestamp;
    if (elapsed > SDKHandler.SETTINGS_TIMEOUT_MS) {
      hilog.warn(0x0000, 'SDKKit', `[Settings] 超过超时时间 ${elapsed}ms，仍按关闭处理`);
    }

    this._isWaitingForSettingsClosed = false;
    hilog.info(0x0000, 'SDKKit', '[Settings] 用户从设置返回，开始检查通知状态');

    return new Promise<string>((resolve) => {
      notificationManager.isNotificationEnabled().then((enabled: boolean) => {
        this._lastNotificationEnabled = enabled;
        hilog.info(0x0000, 'SDKKit', `[Settings] 设置关闭后通知状态: enabled=${enabled}`);

        // ★ 主动通知 Unity
        this.sendSettingsClosedSignal(enabled);

        ret = "{\"success\":true,\"enabled\":" + (enabled ? 1 : 0) + "}";
        resolve(ret);
      }).catch((err: BusinessError) => {
        hilog.error(0x0000, 'SDKKit',
          `[Settings] isNotificationEnabled 失败: ${err.code}, ${err.message}`);

        // 查询失败按未开启处理
        this.sendSettingsClosedSignal(false);

        ret = "{\"code\":\"" + err.code + "\",\"message\":\"" + err.message + "\"}";
        resolve(ret);
      });
    });
  }


  // ============================================================
  // ★ 发送设置关闭 Signal 给 Unity
  // ============================================================
  private sendSettingsClosedSignal(enabled: boolean): void {
    try {
      hilog.info(0x0000, 'SDKKit', `[Settings] Tuanjie 对象: ${JSON.stringify(Tuanjie)}`);
      hilog.info(0x0000, 'SDKKit', `[Settings] TuanjieSendMessage 类型: ${typeof Tuanjie.TuanjieSendMessage}`);

      Tuanjie.TuanjieSendMessage(
        'PushMessageReceiver',
        'OnSettingsClosedSignal',
        enabled ? '1' : '0'
      );
      hilog.info(0x0000, 'SDKKit', `[Settings] TuanjieSendMessage 调用完成`);
    } catch (e) {
      hilog.error(0x0000, 'SDKKit',
        `[Settings] TuanjieSendMessage 异常: ${JSON.stringify(e)}`);
    }
  }






  public concatJson(json1:string,json2:string){
    return json1.substring(0,json1.length-1)+","+json2.substring(1);
  }

  public static getInstance() {
    if (Instance == null) {
      Instance = new SDKHandler();
    }
    return Instance;
  }
}

function strToProductType(params: string): iap.ProductType {
  switch (params) {
    case "CONSUMABLE":
      return iap.ProductType.CONSUMABLE;
    case "NONCONSUMABLE":
      return iap.ProductType.NONCONSUMABLE;
    case "AUTORENEWABLE":
      return iap.ProductType.AUTORENEWABLE;
    case "0":
      return iap.ProductType.CONSUMABLE;
    case "1":
      return iap.ProductType.NONCONSUMABLE;
    case "2":
      return iap.ProductType.AUTORENEWABLE;
  }
  return iap.ProductType.CONSUMABLE;
}


function strToQueryType(params: string): iap.PurchaseQueryType {
  switch (params) {
    case "ALL":
      return iap.PurchaseQueryType.ALL;
    case "UNFINISHED":
      return iap.PurchaseQueryType.UNFINISHED;
    case "CURRENT_ENTITLEMENT":
      return iap.PurchaseQueryType.CURRENT_ENTITLEMENT;
  }
  return iap.PurchaseQueryType.ALL;
}

interface PurchaseData {
  type: number;
  jwsPurchaseOrder?: string;
  jwsSubscriptionStatus?: string;
}

interface PurchaseOrderPayload {
  applicationId: string;
  countryCode: string;
  environment: string;
  payOrderId: string;
  price: number;
  productId: string;
  productType: number;
  purchaseOrderId: string;
  purchaseTime: number;
  purchaseToken: string;
  signedTime: number;
}

interface Products {
  products: iap.Product[];
}

interface adsArray{
  ads:advertising.Advertisement[];
}
