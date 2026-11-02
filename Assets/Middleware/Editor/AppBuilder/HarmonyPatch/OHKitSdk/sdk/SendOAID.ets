import { SDKHandler } from './SDKHandler'
import { BusinessError, emitter } from '@kit.BasicServicesKit';
import {  identifier } from '@kit.AdsKit';
import fs from '@ohos.file.fs';
import { JSON } from '@kit.ArkTS';
import { abilityAccessCtrl, common, Permissions, PermissionRequestResult } from '@kit.AbilityKit';
import { dataShare } from '@kit.ArkData';
import { Tuanjie } from './TuanjieNative';
import { hilog } from '@kit.PerformanceAnalysisKit';

export class SendOAID {



  static oaid: string = '正在获取OAID...';
  static permissionGranted: boolean = false;
  static showPermissionDialog: boolean = false;

  static  permissionStatus: string = '等待权限请求';
  static  privacyConsentGranted: boolean = false;


  static readonly PERMISSION_TRACKING: Permissions = 'ohos.permission.APP_TRACKING_CONSENT';

  // ---------- 授权结果回传统一入口 ----------
  // 允许/拒绝/异常都从这里回 '1'/'0'：埋点（Pop_accept/Pop_refuse）与关引导页都靠这次回包。
  private static reportResult(granted: boolean, status: string): void {
    SendOAID.permissionGranted = granted;
    SendOAID.permissionStatus = status;
    if (!granted) {
      SendOAID.oaid = '00000000-0000-0000-0000-000000000000';
    }
    hilog.info(0x0000, 'OAIDPage', `权限结果回传: granted=${granted}, status=${status}`);
    Tuanjie.TuanjieSendMessage(
      'PushMessageReceiver',
      'OnOAIDClosedSignal',
      granted ? '1' : '0'
    );
  }

  // 授权通过后的收尾：先回包（埋点/关页以权限结果为准），再后台取 OAID 与归因。
  private static onGranted(ctx: common.Context): void {
    SendOAID.reportResult(true, '权限已授予');
    // 注意：授权结果已按权限结果回传（OnOAIDClosedSignal），
    // OAID 只影响广告出价，不代表授权意向，不再重复回传。
    SendOAID.getOAID();
    SendOAID.fetchAndSaveAttribution(ctx);
  }

  static async RequestOAID() {

    hilog.info(0x0000, 'OAIDPage', `正在请求权限...`);

    const atManager: abilityAccessCtrl.AtManager = abilityAccessCtrl.createAtManager();
    // SDKManager 的方法由消息机制派发，未必在 UI 线程；globalThis.AbilityContext 优先，getContext() 兜底
    const ctx = (globalThis.AbilityContext ?? getContext()) as common.Context;
    const permissions: Array<Permissions> = [SendOAID.PERMISSION_TRACKING];

    // ① 首选：标准系统授权弹窗（首次申请时由系统弹出）
    let result: PermissionRequestResult | null = null;
    try {
      result = await atManager.requestPermissionsFromUser(ctx, permissions);
    } catch (err) {
      hilog.info(0x0000, 'OAIDPage',
        `requestPermissionsFromUser error: ${(err as BusinessError).code}, ${(err as BusinessError).message}`);
    }

    if (result != null) {
      if (result.authResults[0] === 0) {
        SendOAID.onGranted(ctx);
        return;
      }

      // dialogShownResults（API 12+）：本次申请系统是否有弹窗。
      // true  = 弹窗已弹出、被用户拒绝 → 尊重本次拒绝，不再打扰（引导页随弹窗关闭）
      // false = 系统根本没弹窗 = 此前已拒绝过/权限已固定（requestPermissionsFromUser
      //         被拒过一次后不再弹框，静默返回 -1）→ 走 requestPermissionOnSetting 兜底
      const dialogShown: boolean =
        result.dialogShownResults != null &&
        result.dialogShownResults.length > 0 &&
        result.dialogShownResults[0] === true;

      if (dialogShown) {
        // ★ 拒绝：同样要回 '0'（Pop_refuse 埋点 + 关引导页都靠这次回包，
        //   不回包 Unity 侧只能等超时兜底，漏斗会缺"拒绝"出口）
        SendOAID.reportResult(false, '权限被拒绝');
        return;
      }

      hilog.info(0x0000, 'OAIDPage',
        `系统未弹出标准授权弹窗（此前已拒绝或请求无效），改拉权限设置弹窗`);
    }

    // ② 兜底：应用内"权限设置弹窗"（带开关，API 12+；被拒过也能拉起，官方推荐的二次授权路径）
    try {
      const settingResults: Array<abilityAccessCtrl.GrantStatus> =
        await atManager.requestPermissionOnSetting(ctx, permissions);
      if (settingResults != null && settingResults.length > 0 &&
          settingResults[0] === abilityAccessCtrl.GrantStatus.PERMISSION_GRANTED) {
        SendOAID.onGranted(ctx);
        return;
      }
      SendOAID.reportResult(false, '权限被拒绝');
    } catch (err) {
      // ★ 请求异常（系统取消 / 未完成交互 / 兜底弹窗拉起失败等）也要回 '0'，保证引导页必被关闭
      hilog.info(0x0000, 'OAIDPage',
        `requestPermissionOnSetting error: ${(err as BusinessError).code}, ${(err as BusinessError).message}`);
      SendOAID.reportResult(false, `权限请求失败: ${(err as BusinessError).code}`);
    }

  }

  static async getOAID() {
    try {

      hilog.info(0x0000, 'OAIDPage', `获取中...`);
      const oaid = await identifier.getOAID();
      if (oaid && oaid !== '00000000-0000-0000-0000-000000000000') {
        SendOAID.oaid = oaid;
        SendOAID.sendToFile(oaid,getContext());

        hilog.info(0x0000, 'OAIDPage', `Get OAID: ${oaid}`);
        // 注意：授权结果已在 RequestOAID 里按权限结果回传（OnOAIDClosedSignal），
        // 这里只负责拿 OAID 落盘，不再重复回传，避免一次授权回两次包。
      } else {
        SendOAID.oaid = '无法获取有效的OAID';
        hilog.info(0x0000, 'OAIDPage', `Failed to get OAID: 无法获取有效的OAID`);
      }
    } catch (error) {
      const err = error as BusinessError;
      switch (err.code) {
        case 201: SendOAID.oaid = '权限未授权'; break;
        case 202: SendOAID.oaid = '系统服务未启用'; break;
        case 203: SendOAID.oaid = '获取OAID超时'; break;
        default: SendOAID.oaid = `获取失败: ${err.code}`;

          //hilog.info(0x0000, 'OAIDPage', `Failed to get OAID: 无法获取有效的OAID`);
      }
    }
  }

  static sendToFile(oaid: string, context: common.Context) {
    try {
      const filesDir = context.filesDir;
      const unityDir = `${filesDir}/files`;
      if (!fs.accessSync(unityDir)) {
        fs.mkdirSync(unityDir);
      }
      const filePath = `${unityDir}/oaid.txt`;
      const file = fs.openSync(filePath, fs.OpenMode.CREATE | fs.OpenMode.READ_WRITE);
      fs.writeSync(file.fd, oaid);
      fs.closeSync(file);
      console.info(`OAID已保存到Unity路径: ${filePath}`);
    } catch (e) {
      console.error(`保存OAID时发生异常: ${JSON.stringify(e)}`);
    }
  }

  // ---------- 新增归因相关方法 ----------
  static async fetchAndSaveAttribution(context: common.Context) {

    //Logger.info(TAG, `[HuaweiAttr] Enter Attribution`);

    try {
      const uri = 'datashareproxy://com.huawei.appmarket.commondata/item/5'; // 注意：URI 格式可能需要调整
      const packageName = context.applicationInfo.name; // 使用 bundleName 而非 name

      // 1. 创建 DataProxyHandle 实例
      const dataProxyHandle: dataShare.DataProxyHandle = await dataShare.createDataProxyHandle();

      // 2. 构造配置对象
      const config: dataShare.DataProxyConfig = {
        type: dataShare.DataProxyType.SHARED_CONFIG, // 根据实际数据类型选择
      };

      // 3. 调用 get 方法获取数据
      const results: dataShare.DataProxyGetResult[] = await dataProxyHandle.get([uri], config);

      if (!results || results.length === 0) {
        //Logger.warn(TAG, '[HuaweiAttr] No attribution data found.');
        SendOAID.saveAttributionToFile(JSON.stringify({}), context);
        return;
      }


      // 尝试将 results 转为 JSON 字符串
      let jsonString: string;
      try {
        // 如果 results 是对象数组，直接序列化整个数组
        jsonString = JSON.stringify(results);
        // 如果每个元素有 value 字段，可以提取 value 并重新序列化
        const values = results.map(r => r.value);
        jsonString = JSON.stringify(values);
      } catch (e) {
        jsonString = '{}';
      }

      SendOAID.saveAttributionToFile(jsonString, context);
      hilog.info(0x0000, 'OAIDPage', `[HuaweiAttr] Attribution raw data saved: ${jsonString}`);


    } catch (error) {
      const err = error as BusinessError;
      hilog.info(0x0000,'OAIDPage', `[HuaweiAttr] fetchAndSaveAttribution error: ${err.code}, ${err.message}`);
      SendOAID.saveAttributionToFile(JSON.stringify({}), context);
    }
  }

  static saveAttributionToFile(content: string, context: common.Context) {
    try {
      const filesDir = context.filesDir;
      const unityDir = `${filesDir}/files`;
      if (!fs.accessSync(unityDir)) {
        fs.mkdirSync(unityDir);
      }
      const filePath = `${unityDir}/attribution.txt`;
      const file = fs.openSync(filePath, fs.OpenMode.CREATE | fs.OpenMode.READ_WRITE | fs.OpenMode.TRUNC);
      fs.writeSync(file.fd, content);
      fs.closeSync(file);
      //Logger.info(TAG, `Attribution file saved: ${filePath}`);
    } catch (e) {
      //Logger.error(TAG, `saveAttributionToFile error: ${JSON.stringify(e)}`);
    }
  }

}

