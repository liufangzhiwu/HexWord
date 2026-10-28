import { SDKManager, TuanjiePlayerView } from 'tuanjieLib';
import { promptAction, PromptAction, window } from '@kit.ArkUI';
import { BusinessError } from '@ohos.base';
import identifier from '@ohos.identifier.oaid';
import common from '@ohos.app.ability.common';
import abilityAccessCtrl from '@ohos.abilityAccessCtrl';
import Want from '@ohos.app.ability.Want';
import Logger from '../log/Logger';
import prompt from '@ohos.prompt';
import fs from '@ohos.file.fs';
import { privacyManager } from '@kit.AppGalleryKit';
import { hilog } from '@kit.PerformanceAnalysisKit';

const TAG = 'OAIDPage';

@Entry
@Component
struct TuanjiePlayer {

  @State oaid: string = '正在获取OAID...';
  @State permissionGranted: boolean = false;
  @State showPermissionDialog: boolean = false;

  private context: common.UIAbilityContext = getContext(this) as common.UIAbilityContext;

  @State permissionStatus: string = '等待权限请求';
  @State privacyConsentGranted: boolean = false;
  @State privacyStatus: string = '等待隐私协议查询';

  build() {
    Row() {
      Column() {
        Stack() {
          TuanjiePlayerView();
        }
        // ★ 挂到 Stack 上
        .onBlur(() => {
          hilog.info(0x0000, 'SDKKit', '[UI] Stack onBlur');
        })
        .onFocus(() => {
          hilog.info(0x0000, 'SDKKit', '[UI] Stack onFocus');
          SDKManager.OnNotificationSettingsClosed();
        })
      }
      .height('100%')
      .backgroundColor('#ff000000')
    }
    .width('100%')
    .backgroundColor('#ff000000')
  }

  onBackPress() {
    try{
      promptAction.showDialog({
        title:"退出游戏",
        message:"\n                 你确定要退出游戏吗？     \n",
        alignment:DialogAlignment.Center,
        buttons:[
          {
            text:"取消",
            color:'#000000'
          },
          {
            text:"确定",
            color:'#000000'
          }
        ],
      }).then(data=>{
        if(data.index==0){
          return;
        }else{
          this.exitGame();
        }
      })
    }catch (error){
      console.error("showDialog args error code is "+error.code)
    };
    return true;
  }

  exitGame(){
    // 检查是否有全局上下文
    if (globalThis.abilityContext) {
      Logger.info(TAG, '通过全局上下文退出应用');
      globalThis.abilityContext.terminateSelf().then(() => {
        Logger.info(TAG, '应用退出成功');
      }).catch((error: BusinessError) => {
        Logger.error(TAG, `terminateSelf失败: ${JSON.stringify(error)}`);
      });
    }
    // 如果没有全局上下文，尝试使用当前上下文
    else if (this.context) {
      Logger.info(TAG, '通过当前上下文退出应用');
      this.context.terminateSelf().then(() => {
        Logger.info(TAG, '应用退出成功');
      }).catch((error: BusinessError) => {
        Logger.error(TAG, `terminateSelf失败: ${JSON.stringify(error)}`);
      });
    }
  }

}