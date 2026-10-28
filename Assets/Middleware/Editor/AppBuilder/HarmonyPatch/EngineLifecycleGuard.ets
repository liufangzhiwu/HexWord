import { TuanjieLog } from './TuanjieLog';

/**
 * 引擎生命周期闸门。
 *
 * 【背景】HarmonyOS 上应用切到后台后，系统会回收该进程占用的 OHAudio 音频流。
 * 若此时 Unity/Tuanjie 引擎主循环仍在运行，miniaudio 的 MiniAudio 线程会继续被调度，
 * 并回调到已经失效的音频对象 —— 团结引擎在 libtuanjie.so 内通过一张 13 项函数指针表
 * (ma_backend_callbacks) 分发设备读写回调，表项为 NULL 时执行 `blr x8` 直接跳到 0x0，
 * 表现为 CPP_CRASH / SIGSEGV(SEGV_MAPERR)，崩溃线程名 MiniAudio。
 *
 * 实测崩溃样本：
 *   - 华为后台：1.3.2 / KLE-AL00 / 前后台状态=后台 / pc=0x0 / libtuanjie.so
 *   - 本地 cppcrash.log：1.2.9 / Mate 80 Pro / 崩溃线程 TuanjieMain / libtuanjie.so
 *
 * 【职责】
 * 1. 记录前后台切换的确切时刻，便于与崩溃日志对时定位。
 * 2. 进入后台后拒绝一切"恢复引擎"指令，防止后台被误唤醒。
 * 3. 保证进入后台时一定会下发 nativeOnPause()（原实现在视频播放态下会跳过）。
 */
export class EngineLifecycleGuard {
  private static inBackground: boolean = false;
  private static backgroundEnterTime: number = 0;
  private static backgroundCount: number = 0;

  /** 当前是否处于后台状态 */
  public static isInBackground(): boolean {
    return EngineLifecycleGuard.inBackground;
  }

  /** 进入后台：置闸门并记录时刻 */
  public static enterBackground(): void {
    if (EngineLifecycleGuard.inBackground) {
      return;
    }
    EngineLifecycleGuard.inBackground = true;
    EngineLifecycleGuard.backgroundEnterTime = Date.now();
    EngineLifecycleGuard.backgroundCount += 1;
    TuanjieLog.warn('EngineLifecycleGuard: enter background, seq=%{public}d, at=%{public}s',
      EngineLifecycleGuard.backgroundCount, new Date().toISOString());
  }

  /** 离开后台：解除闸门并输出本次后台时长 */
  public static leaveBackground(): void {
    if (!EngineLifecycleGuard.inBackground) {
      return;
    }
    const stayMs: number = Date.now() - EngineLifecycleGuard.backgroundEnterTime;
    EngineLifecycleGuard.inBackground = false;
    TuanjieLog.info('EngineLifecycleGuard: leave background, stayed=%{public}d ms, at=%{public}s',
      stayMs, new Date().toISOString());
  }

  /**
   * 是否允许向引擎下发"恢复"指令。
   * 后台状态下返回 false，避免引擎在后台被唤醒、音频线程重新被调度。
   */
  public static canResumeEngine(): boolean {
    if (EngineLifecycleGuard.inBackground) {
      TuanjieLog.warn('EngineLifecycleGuard: resume REJECTED, app is in background');
      return false;
    }
    return true;
  }
}
