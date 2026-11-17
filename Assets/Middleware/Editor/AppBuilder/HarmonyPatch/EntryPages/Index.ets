import { SDKManager, TuanjiePlayerView } from 'tuanjieLib';
import { BannerAd } from 'tuanjieLib';
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
        Stack({ alignContent: Alignment.Bottom }) {
          TuanjiePlayerView();
          // ★ 横幅广告：页面内组件（不放弹窗，AutoAdComponent 在 CustomDialog 里请求会失败）。
          //   显隐由 SDKManager.LoadBanner / HideBanner 通过 AppStorage 'BannerShowing' 控制。
          BannerAd()
        }
        // ★ 挂到 Stack 上
        .onBlur(() => {
          hilog.info(0x0000, 'SDKKit', '[UI] Stack onBlur');
        })
        .onFocus(() => {
          hilog.info(0x0000, 'SDKKit', '[UI] Stack onFocus');
          // ★ 收银台/系统弹窗关闭后页面重新获得焦点：通知游戏侧收口（详见 SDKManager.OnWindowFocusGained）
          SDKManager.OnWindowFocusGained();
          SDKManager.OnNotificationSettingsClosed();
        })
      }
      .height('100%')
      .backgroundColor('#ff000000')
    }
    .width('100%')
    .backgroundColor('#ff000000')
  }

  // ============================================================
  // ★ 系统返回键 / 全面屏侧滑返回
  //
  // 返回 true 吃掉系统默认的「退出应用」，改为转发给 Unity，
  // 由 BackGestureRouter 按当前界面分流（与安卓 KeyCode.Escape 同一套规则）：
  //   退出确认面板 → 忽略 / 暂停界面 → 继续 / 弹窗 → 关闭 /
  //   拼字关卡 → 暂停 / 六边形关卡 → 回主页 / Start、大厅 → 退出确认面板
  // ============================================================
  onBackPress(): boolean {
    hilog.info(0x0000, 'SDKKit', '[UI] onBackPress（侧滑返回）');
    // 桥接不可用时返回 false，把这次返回交回系统（例如引擎还没起来时允许用户退出）
    return SDKManager.NotifyBackGesture();
  }

}