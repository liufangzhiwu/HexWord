import { SDKHandler } from './SDKHandler'
import { BusinessError, emitter } from '@kit.BasicServicesKit';
import {  identifier } from '@kit.AdsKit';
import fs from '@ohos.file.fs';
import { JSON } from '@kit.ArkTS';
import { abilityAccessCtrl, common } from '@kit.AbilityKit';
import { dataShare } from '@kit.ArkData';
import { Tuanjie } from './TuanjieNative';
import { hilog } from '@kit.PerformanceAnalysisKit';

export class SendOAID {



  static oaid: string = '正在获取OAID...';
  static permissionGranted: boolean = false;
  static showPermissionDialog: boolean = false;

  static  permissionStatus: string = '等待权限请求';
  static  privacyConsentGranted: boolean = false;


  static async RequestOAID() {

    hilog.info(0x0000, 'OAIDPage', `正在请求权限...`);

    const atManager: abilityAccessCtrl.AtManager = abilityAccessCtrl.createAtManager();
    try {
      const result = await atManager.requestPermissionsFromUser(
        getContext(),
        ["ohos.permission.APP_TRACKING_CONSENT"]
      );
      if (result.authResults[0] === 0) {
        SendOAID.permissionGranted = true;
        hilog.info(0x0000, 'OAIDPage', `权限已授予`);
        SendOAID.permissionStatus = '权限已授予';
        SendOAID.getOAID();

        SendOAID.fetchAndSaveAttribution(getContext());

      } else {
        SendOAID.permissionStatus = '权限被拒绝';
        SendOAID.permissionGranted = false;
        SendOAID.oaid = '00000000-0000-0000-0000-000000000000';
      }
    } catch (err) {
      SendOAID.permissionStatus = `权限请求失败: ${(err as BusinessError).code}`;
      SendOAID.permissionGranted = false;
      SendOAID.oaid = '00000000-0000-0000-0000-000000000000';
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
        Tuanjie.TuanjieSendMessage(
          'PushMessageReceiver',
          'OnOAIDClosedSignal',
          '1'
        );
      } else {
        SendOAID.oaid = '无法获取有效的OAID';
        hilog.info(0x0000, 'OAIDPage', `Failed to get OAID: 无法获取有效的OAID`);
        Tuanjie.TuanjieSendMessage(
          'PushMessageReceiver',
          'OnOAIDClosedSignal',
          '0'
        );
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

