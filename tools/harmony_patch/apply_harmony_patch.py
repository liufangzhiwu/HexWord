#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
apply_harmony_patch.py
=======================
把「MiniAudio 后台崩溃」的 ArkTS 侧加固补丁，重放到任意一次 Unity 导出的鸿蒙工程上。

背景
----
Unity 每次导出 OpenHarmony 工程都会重建 `tuanjieLib/`，手改的 .ets 会被重置。
本脚本让改动可重放、可审计、幂等（重复执行结果一致）。

用法
----
    python apply_harmony_patch.py <导出的鸿蒙工程根目录>
    例：python apply_harmony_patch.py D:\\GitFork\\HexWord\\output\\harmoney_1.3.3

补丁内容
--------
1. tuanjieLib/src/main/ets/common/EngineLifecycleGuard.ets   【新增】前后台生命周期闸门 + 时刻打点
2. tuanjieLib/src/main/ets/ability/TuanjiePlayerAbilityBase.ets
     - onBackground(): 无条件下发 nativeOnPause()（原实现视频播放态会跳过）
     - onForeground()/RESUMED: 先解除后台闸门，避免恢复指令被误挡
     - onResume(): 后台状态下拒绝唤醒引擎
3. tuanjieLib/exported.ets                                   导出 EngineLifecycleGuard / TuanjieLog
4. entry/src/main/ets/ability/TuanjiePlayerAbility.ets       前后台打印 ISO 时间戳，供与崩溃日志对时
"""

import os
import shutil
import sys

BUNDLE_DIR = os.path.dirname(os.path.abspath(__file__))

# 两种导出布局：
#   A) 单模块（团结 2022.3.55t4 直接 exportAsOpenHarmonyProject 的产物）：
#      entry/src/main/ets/... 下有全部 ArkTS 源码与 lib
#   B) 双模块（团队历史导出）：entry 只依赖 tuanjieLib HAR 模块，引擎 ArkTS 在 tuanjieLib/src/main/ets
LAYOUTS = [
    {
        "name": "A-single-module",
        "base": os.path.join("entry", "src", "main", "ets", "ability", "TuanjiePlayerAbilityBase.ets"),
        "guard": os.path.join("entry", "src", "main", "ets", "common", "EngineLifecycleGuard.ets"),
        "entry": os.path.join("entry", "src", "main", "ets", "ability", "TuanjiePlayerAbility.ets"),
        "exported": None,
    },
    {
        "name": "B-dual-module",
        "base": os.path.join("tuanjieLib", "src", "main", "ets", "ability", "TuanjiePlayerAbilityBase.ets"),
        "guard": os.path.join("tuanjieLib", "src", "main", "ets", "common", "EngineLifecycleGuard.ets"),
        "entry": os.path.join("entry", "src", "main", "ets", "ability", "TuanjiePlayerAbility.ets"),
        "exported": os.path.join("tuanjieLib", "exported.ets"),
    },
]

results = []


def read(path):
    # newline="" 保留原始行尾。Unity 导出的 .ets 行尾不统一：
    # TuanjiePlayerAbility.ets 是 CRLF，TuanjiePlayerAbilityBase.ets 是 LF。
    # 若用默认 universal newlines，\r\n 会被读成 \n，写回时整篇文件的行尾就被改掉了。
    with open(path, "r", encoding="utf-8", newline="") as f:
        return f.read()


def write(path, content):
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(content)


def patch(path, pairs, required=True):
    """pairs: [(原文, 替换文[, 幂等标记]), ...]，命中即替换；原文不存在且 required=True 则报错。

    幂等标记用于「补丁已打过」的判定，必须是补丁独有、原文绝不会出现的字符串。
    匹配时会按文件实际行尾（LF / CRLF）把替换串做对应转换，避免多行替换全部落空。
    """
    if not os.path.exists(path):
        results.append(("SKIP", path, "文件不存在"))
        return
    text = read(path)
    crlf = "\r\n" in text
    changed = False
    for pair in pairs:
        old, new = pair[0], pair[1]
        marker = pair[2] if len(pair) > 2 else new
        if crlf:
            old = old.replace("\n", "\r\n")
            new = new.replace("\n", "\r\n")
            marker = marker.replace("\n", "\r\n")
        if marker in text:
            continue  # 已打过补丁
        if old in text:
            text = text.replace(old, new, 1)
            changed = True
        elif required:
            results.append(("FAIL", path, "未找到待替换内容: " + old.strip().splitlines()[0][:70]))
            return
    if changed:
        write(path, text)
    results.append(("PATCHED" if changed else "ALREADY", path, ""))


def verify(root, lay):
    """完整性校验：任何一处 marker 没落地都算失败，杜绝静默跳过。"""
    base = os.path.join(root, lay["base"])
    entry = os.path.join(root, lay["entry"])
    guard = os.path.join(root, lay["guard"])
    btext = read(base) if os.path.exists(base) else ""
    etext = read(entry) if os.path.exists(entry) else ""
    checks = [
        ("EngineLifecycleGuard.ets 已生成", os.path.exists(guard)),
        ("base 缺少 EngineLifecycleGuard import",
         "import { EngineLifecycleGuard } from '../common/EngineLifecycleGuard'" in btext),
        ("base 缺少 leaveBackground", "EngineLifecycleGuard.leaveBackground();" in btext),
        ("base 缺少 enterBackground", "EngineLifecycleGuard.enterBackground();" in btext),
        ("base 缺少 canResumeEngine 闸门",
         "if (!EngineLifecycleGuard.canResumeEngine()) {" in btext),
        ("base 缺少无条件 nativeOnPause", "Tuanjie.nativeOnPause();" in btext),
        ("entry 缺少 onForeground 时间戳", "[TuanjiePlayerAbility] onForeground at " in etext),
        ("entry 缺少 onBackground 时间戳", "[TuanjiePlayerAbility] onBackground at " in etext),
    ]
    missing = [name for name, ok in checks if not ok]
    for name, ok in checks:
        results.append(("OK" if ok else "FAIL", name, ""))
    return missing


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    root = os.path.abspath(sys.argv[1])
    if not os.path.isdir(root):
        print("[ERROR] 目标目录不存在: " + root)
        return 2
    print("[TARGET] " + root)

    # 选定布局：优先 base 存在且内容像引擎模板的那个
    lay = None
    for cand in LAYOUTS:
        p = os.path.join(root, cand["base"])
        if os.path.exists(p) and "TuanjiePlayerAbilityBase" in read(p):
            lay = cand
            break
    if lay is None:
        print("[ERROR] 未识别的导出布局（找不到 TuanjiePlayerAbilityBase.ets）")
        return 2
    print("[LAYOUT] " + lay["name"])

    # 1) 新增 EngineLifecycleGuard.ets
    guard_dst = os.path.join(root, lay["guard"])
    guard_src = os.path.join(BUNDLE_DIR, "EngineLifecycleGuard.ets")
    if os.path.exists(guard_dst):
        results.append(("ALREADY", lay["guard"], "已存在"))
    else:
        os.makedirs(os.path.dirname(guard_dst), exist_ok=True)
        shutil.copyfile(guard_src, guard_dst)
        results.append(("ADDED", lay["guard"], ""))

    # 2) TuanjiePlayerAbilityBase.ets
    base = os.path.join(root, lay["base"])
    patch(base, [
        # 2.1 import
        (
            "import { VideoPlayerProxy } from '../utils/VideoPlayerProxy'",
            "import { VideoPlayerProxy } from '../utils/VideoPlayerProxy'\n"
            "import { EngineLifecycleGuard } from '../common/EngineLifecycleGuard'",
            "import { EngineLifecycleGuard } from '../common/EngineLifecycleGuard'",
        ),
        # 2.2 onForeground 先解除闸门
        (
            "    TuanjieLog.info('%{public}s', 'TuanjiePlayerAbility onForeground');\n    this.onResume();",
            "    TuanjieLog.info('%{public}s', 'TuanjiePlayerAbility onForeground');\n"
            "    // 回到前台：先解除后台闸门，再恢复引擎\n"
            "    EngineLifecycleGuard.leaveBackground();\n"
            "    this.onResume();",
            "EngineLifecycleGuard.leaveBackground();\n    this.onResume();",
        ),
        # 2.3 onBackground 无条件 nativeOnPause
        (
            "    TuanjieLog.info('%{public}s', 'TuanjiePlayerAbility onBackground');\n    this.onPause();",
            "    TuanjieLog.info('%{public}s', 'TuanjiePlayerAbility onBackground');\n"
            "    EngineLifecycleGuard.enterBackground();\n"
            "    this.onPause();\n"
            "    // [崩溃加固] 无条件暂停引擎主循环。原实现只在非视频播放态才调用 nativeOnPause(),\n"
            "    // 视频播放态下引擎继续运行, MiniAudio 线程在 OHAudio 流被系统回收后仍被调度,\n"
            "    // 回调到失效对象上的 NULL 函数指针 -> libtuanjie.so 内 CPP_CRASH / SIGSEGV @ pc=0x0。\n"
            "    // nativeOnPause 为幂等暂停语义, 重复下发安全。\n"
            "    Tuanjie.nativeOnPause();",
            "EngineLifecycleGuard.enterBackground();",
        ),
        # 2.4 windowStage RESUMED 也要解除闸门
        (
            "          TuanjieLog.info('windowStage resumed.');\n          this.onResume();",
            "          TuanjieLog.info('windowStage resumed.');\n"
            "          // RESUMED 同样是『回到前台』的信号, 必须先解除闸门,\n"
            "          // 否则某些机型上 RESUMED 先于 onForeground 触发时会误挡引擎恢复。\n"
            "          EngineLifecycleGuard.leaveBackground();\n"
            "          this.onResume();",
            "EngineLifecycleGuard.leaveBackground();\n          this.onResume();",
        ),
        # 2.5 onResume 增加闸门校验
        (
            "  private onResume(): void {\n    if (VideoPlayerProxy.getInstance().IsPlaying()) {",
            "  private onResume(): void {\n"
            "    // [崩溃加固] 后台状态下拒绝恢复引擎, 防止后台被误唤醒导致音频线程重新被调度。\n"
            "    if (!EngineLifecycleGuard.canResumeEngine()) {\n"
            "      return;\n"
            "    }\n"
            "    if (VideoPlayerProxy.getInstance().IsPlaying()) {",
            "if (!EngineLifecycleGuard.canResumeEngine()) {",
        ),
    ])

    # 3) exported.ets（仅双模块布局需要）
    if lay["exported"]:
        exported = os.path.join(root, lay["exported"])
        patch(exported, [
            (
                "export { TuanjiePlayerView } from './src/main/ets/pages/components/TuanjiePlayerView'",
                "export { TuanjiePlayerView } from './src/main/ets/pages/components/TuanjiePlayerView'\n"
                "export { EngineLifecycleGuard } from './src/main/ets/common/EngineLifecycleGuard'\n"
                "export { TuanjieLog } from './src/main/ets/common/TuanjieLog'",
                "export { EngineLifecycleGuard } from './src/main/ets/common/EngineLifecycleGuard'",
            ),
        ])
    else:
        results.append(("SKIP", "(无 tuanjieLib 模块，无需 exported.ets)", ""))

    # 4) entry 侧时间戳日志
    entry = os.path.join(root, lay["entry"])
    if os.path.exists(entry):
        etext = read(entry)
        # 4.1 补 TuanjieLog 的 import（两种布局导入方式不同）
        if "TuanjieLog" not in etext.split("export default class")[0]:
            if "from 'tuanjieLib'" in etext:
                patch(entry, [
                    (
                        "import { TuanjiePlayerAbilityBase } from 'tuanjieLib';",
                        "import { TuanjiePlayerAbilityBase, TuanjieLog } from 'tuanjieLib';",
                        "import { TuanjiePlayerAbilityBase, TuanjieLog } from 'tuanjieLib';",
                    ),
                ])
            else:
                patch(entry, [
                    (
                        "import { TuanjiePlayerAbilityBase } from './TuanjiePlayerAbilityBase';",
                        "import { TuanjiePlayerAbilityBase } from './TuanjiePlayerAbilityBase';\n"
                        "import { TuanjieLog } from '../common/TuanjieLog';",
                        "import { TuanjieLog } from '../common/TuanjieLog';",
                    ),
                ])
        # 4.2 前后台 ISO 时间戳（**替换**已有的空实现，不能插入 —— 否则重复方法定义）
        patch(entry, [
            (
                "  onForeground(): void {\n    super.onForeground();\n  }",
                "  onForeground(): void {\n"
                "    TuanjieLog.info('%{public}s', '[TuanjiePlayerAbility] onForeground at ' + new Date().toISOString());\n"
                "    super.onForeground();\n"
                "  }",
                "[TuanjiePlayerAbility] onForeground at ",
            ),
            (
                "  onBackground(): void {\n    super.onBackground();\n  }",
                "  onBackground(): void {\n"
                "    TuanjieLog.info('%{public}s', '[TuanjiePlayerAbility] onBackground at ' + new Date().toISOString());\n"
                "    super.onBackground();\n"
                "  }",
                "[TuanjiePlayerAbility] onBackground at ",
            ),
        ], required=False)
    else:
        results.append(("SKIP", lay["entry"], "文件不存在"))

    missing = verify(root, lay)

    print("")
    print("%-9s %s" % ("STATUS", "FILE"))
    print("-" * 100)
    failed = 0
    for status, path, note in results:
        print("%-9s %s %s" % (status, path, note))
        if status == "FAIL":
            failed += 1
    print("-" * 100)

    if missing:
        print("补丁未完整落地:")
        for name in missing:
            print("  - " + name)

    print("补丁完成，失败项: %d" % failed)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
