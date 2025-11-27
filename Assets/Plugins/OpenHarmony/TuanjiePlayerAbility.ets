import window from '@ohos.window';
import { AbilityConstant, Want } from '@kit.AbilityKit';
import { SetToGlobalThis } from '../common/GlobalThisUtil';
import { TuanjiePlayerAbilityBase } from './TuanjiePlayerAbilityBase';

export default class TuanjiePlayerAbility extends TuanjiePlayerAbilityBase {
  onCreate(want: Want, launchParam: AbilityConstant.LaunchParam): void {
    this.setConfig();
    super.onCreate(want, launchParam);
  }

  onDestroy(): void {
    super.onDestroy();
  }

  onWindowStageCreate(windowStage: window.WindowStage): void {
    super.setPageUri('pages/Index');
    super.onWindowStageCreate(windowStage);
  }

  onWindowStageDestroy(): void {
    super.onWindowStageDestroy();
  }

  onForeground(): void {
    super.onForeground();
  }

  onBackground(): void {
    super.onBackground();
  }

  setConfig(): void {
    SetToGlobalThis("staticSplashScreenFit", $r('app.integer.StaticSplashScreenFit'));
    SetToGlobalThis("appSplash", $r('app.media.app_splash'));
    SetToGlobalThis("showStaticSplash",$r('app.integer.ShowStaticSplashScreen'));
  }
}
