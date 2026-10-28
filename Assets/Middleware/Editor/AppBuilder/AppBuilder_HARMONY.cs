using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Middleware
{
    public partial class AppBuilder
    {
        /// <summary>
        /// 鸿蒙包版本号（versionName）。全局唯一来源——改版本只改这一处。
        ///
        /// 菜单打包与命令行导出（HarmonyExportBuilder）都从这里取值，
        /// 避免出现「菜单是 A、命令行是 B」导致真机安装版本不一致的问题。
        /// </summary>
        public const string HarmonyVersion = "1.3.4";

        #if UNITY_2022_3_55 || UNITY_2022_3_61
        [MenuItem("Tools/切换平台/Harmony", false, 101)]
        private static void SwitchToHarmony()
        {
            SwitchPlatform(BuildTarget.OpenHarmony);
            if (AssetDatabase.IsValidFolder("Assets/GeneratedLocalRepo"))
                AssetDatabase.DeleteAsset("Assets/GeneratedLocalRepo");
        }
        #endif

        #if UNITY_2022_3_55 || UNITY_2022_3_61
        #region 鸿蒙打包菜单
        [MenuItem("Tools/自动化打包/Harmony/Debug", false, 110)]
        private static void BuildHarmonyDebug()
        {
            BuildHarmony(new BuildParam()
            {
                BuildVersion = HarmonyVersion,
                IsBuildRelease = false,
                IsBuildShowLog = true,
                Channel = Channel.None
            });
        }

        [MenuItem("Tools/自动化打包/Harmony/Release", false, 111)]
        private static void BuildHarmonyRelease()
        {
            BuildHarmony(new BuildParam()
            {
                BuildVersion = HarmonyVersion,
                IsBuildRelease = true,
                IsBuildShowLog = false,
                Channel = Channel.None
            });
        }

        [MenuItem("Tools/自动化打包/Harmony/自检补丁与签名", false, 114)]
        private static void MenuVerifyHarmonyPostProcess()
        {
            var result = VerifyHarmonyPostProcess();
            EditorUtility.DisplayDialog("鸿蒙自检（补丁 + 签名）", result, "确定");
        }

        [MenuItem("Tools/自动化打包/Harmony/回归自检补丁", false, 115)]
        private static void MenuVerifyHarmonyPatchFromTemplate()
        {
            var result = VerifyHarmonyPatchFromTemplate();
            EditorUtility.DisplayDialog("鸿蒙回归自检（补丁）", result, "确定");
        }
        #endregion
        #endif

        #region 流水线自检入口

        /// <summary>
        /// 零参自检：跑一遍**菜单路径**的完整流水线（exportDirOverride = null，
        /// 输出到 harmony_toolchain.json 指定的 output/{outputDirName}/{projectDirName}）。
        ///
        /// 与命令行路径（HarmonyExportBuilder，输出到 output/harmony_fix_x.y.z）用的是同一套
        /// RunHarmonyPipeline，只是输出目录不同。两条路径都必须能独立出包，
        /// 所以留一个可被语义桥接 / CI 调用的入口做回归。
        /// </summary>
        public static string SelfTestMenuPathBuild()
        {
            var param = new BuildParam
            {
                BuildVersion = HarmonyVersion,
                IsBuildRelease = false,
                IsBuildShowLog = true,
                Channel = Channel.None
            };
            var hap = RunHarmonyPipeline(param, null, out var error);
            return string.IsNullOrEmpty(hap)
                ? "FAILED: " + error
                : "OK: " + hap;
        }

        /// <summary>
        /// 零参自检：把导出目录指向一个**全新**的固定路径（output/_layouttest），
        /// 跑完整流水线后回报该目录的模块结构与 entry 源文件数。
        /// 用于验证「从零开始的干净导出」也能出包（排除目录残留导致的结构错乱）。
        /// </summary>
        public static string SelfTestFreshDirBuild()
        {
            var param = new BuildParam
            {
                BuildVersion = HarmonyVersion,
                IsBuildRelease = false,
                IsBuildShowLog = true,
                Channel = Channel.None
            };
            var dir = Path.GetFullPath($"{Application.dataPath}/../output/_layouttest");
            var hap = RunHarmonyPipeline(param, dir, out var error);
            if (!string.IsNullOrEmpty(hap)) return "OK: " + hap;

            var entryEts = Path.Combine(dir, "entry", "src", "main", "ets");
            var count = Directory.Exists(entryEts)
                ? Directory.GetFiles(entryEts, "*", SearchOption.AllDirectories).Length
                : -1;
            var dual = Directory.Exists(Path.Combine(dir, "tuanjieLib"));
            return "FAILED: " + error +
                   $"\n[布局诊断] entry/src/main/ets 文件数={count}, tuanjieLib存在={dual}";
        }

        [MenuItem("Tools/自动化打包/Harmony/自检·全新目录出包", false, 116)]
        private static void MenuSelfTestFreshDirBuild()
        {
            var result = SelfTestFreshDirBuild();
            EditorUtility.DisplayDialog("鸿蒙自检（全新目录出包）", result, "确定");
        }

        #endregion

        #region 鸿蒙打包流水线

        /// <summary>
        /// 鸿蒙（OpenHarmony）完整出包流水线。
        ///
        /// 五个阶段（顺序不可调换）：
        ///   ① Unity 导出 OpenHarmony 工程   —— BuildPipeline.BuildPlayer
        ///   ② 重放 ArkTS 加固补丁           —— 导出时工程被重建，手改 .ets 会被覆盖
        ///   ③ 注入签名配置                 —— 导出模板里 signingConfigs 恒为空数组
        ///   ④ ohpm install                 —— 新导出不带 oh_modules
        ///   ⑤ hvigor assembleHap           —— 产出 entry-default-signed.hap
        ///
        /// 任一阶段失败立即返回，并给出可定位的中文原因。
        /// </summary>
        private static void BuildHarmony(BuildParam buildParam)
        {
            var hapPath = RunHarmonyPipeline(buildParam, null, out var error);
            if (string.IsNullOrEmpty(hapPath))
            {
                Debug.LogError("[Harmony] 打包失败：" + error);
                EditorUtility.DisplayDialog("鸿蒙打包失败", error, "确定");
                return;
            }

            Debug.Log("[Harmony] 打包成功: " + hapPath);
            var outputDir = Path.GetDirectoryName(hapPath);
            EditorUtility.DisplayDialog("鸿蒙打包成功", hapPath, "确定");
            Application.OpenURL(@"file://" + outputDir);
        }

        /// <summary>
        /// 实际执行流水线。exportDirOverride 非空时用它作为导出工程目录（命令行/CI 场景）。
        /// 成功返回 .hap 绝对路径，失败返回 null 并通过 error 输出原因。
        /// </summary>
        internal static string RunHarmonyPipeline(BuildParam buildParam, string exportDirOverride, out string error)
        {
            error = null;

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.OpenHarmony)
            {
                error = "当前构建平台不是 OpenHarmony，请先执行 Tools/切换平台/Harmony";
                return null;
            }

            var cfg = LoadHarmonyConfig();
            if (!ValidateHarmonyToolchain(cfg, out error)) return null;

            // ---------- 宏与 PlayerSettings ----------
            SetDefineSymbols(BuildTargetGroup.OpenHarmony, buildParam);
            EditorUserBuildSettings.exportAsOpenHarmonyProject = true;
            EditorUserBuildSettings.development = false;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.bundleVersion = buildParam.BuildVersion;
            PlayerSettings.OpenHarmony.bundleVersionCode = GenHarmonyBuildNumber();
            PlayerSettings.OpenHarmony.targetArchitectures = OpenHarmonyArchitecture.ARM64;
            PlayerSettings.companyName = "HexaSpaceGames";
            PlayerSettings.productName = "成语消：禅意之境";
            PlayerSettings.applicationIdentifier = "chengyu.idiom.hexa.zen.huawei";
            PlayerSettings.OpenHarmony.useCustomKeystore = true;
            PlayerSettings.OpenHarmony.keystoreName = Path.GetFullPath(cfg.keystorePath("hexa.p12"));
            PlayerSettings.OpenHarmony.keystorePass = "hexa123456";
            PlayerSettings.OpenHarmony.keyaliasName = "hexa";
            PlayerSettings.OpenHarmony.keyaliasPass = "hexa123456";
            PlayerSettings.OpenHarmony.openHarmonyAppID = "6917590527000396765";
            PlayerSettings.OpenHarmony.openHarmonyClientID = "6917590527000396765";
            PlayerSettings.OpenHarmony.openHarmonyProfile =
                Path.GetFullPath(cfg.keystorePath(cfg.profileFileName(buildParam.IsBuildRelease)));
            PlayerSettings.OpenHarmony.openHarmonyCertificate =
                Path.GetFullPath(cfg.keystorePath(cfg.certFileName(buildParam.IsBuildRelease)));

            Debug.Log($"[Harmony] versionCode = {PlayerSettings.OpenHarmony.bundleVersionCode}, " +
                      $"version = {PlayerSettings.bundleVersion}");

            // AB 资源默认已预构建在 Assets/StreamingAssets，鸿蒙导出不需要重复构建。
            // 需要重新打 AB 时把 harmony_toolchain.json 的 buildAssetBundles 改成 true。
            if (cfg.buildAssetBundles)
            {
                Debug.Log("[Harmony] 构建 AssetBundle");
                AssetBundleBuilder.BuildAssetBundles(false);
            }
            else
            {
                Debug.Log("[Harmony] 跳过 AssetBundle 构建（harmony_toolchain.json: buildAssetBundles=false）");
            }
            ApplyHarmonySettings();

            // ---------- ① Unity 导出 ----------
            var outputDir = Path.GetFullPath($"{Application.dataPath}/../output/{cfg.outputDirName}");
            var harProject = !string.IsNullOrEmpty(exportDirOverride)
                ? Path.GetFullPath(exportDirOverride)
                : Path.Combine(outputDir, cfg.projectDirName);
            Directory.CreateDirectory(harProject);
            CleanHarmonyExportDir(harProject);

            Debug.Log("[Harmony] ① 导出 OpenHarmony 工程: " + harProject);
            var report = BuildPipeline.BuildPlayer(GetBuildScenes(), harProject, BuildTarget.OpenHarmony,
                BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded)
            {
                error = $"Unity 导出失败（{report.summary.result}，errors={report.summary.totalErrors}）";
                return null;
            }
            Debug.Log($"[Harmony] ① 完成，耗时 {report.summary.totalTime}");

            // ---------- ② ArkTS 加固补丁 ----------
            Debug.Log("[Harmony] ② 重放 ArkTS 加固补丁");
            if (!HarmonyArkTsPatcher.Apply(harProject, out var patchLog, out error)) return null;
            foreach (var line in patchLog) Debug.Log("    " + line);

            // ---------- ③ 签名配置 ----------
            Debug.Log("[Harmony] ③ 注入签名配置");
            if (!HarmonySigning.Inject(harProject, cfg, buildParam.IsBuildRelease, out error)) return null;

            // ---------- ④ ohpm install ----------
            // 必须每次都执行：Unity 重新导出会改写 oh-package.json5，
            // 而 oh_modules 目录还在旧状态，会导致 "Cannot find module '@thinkingdata/analytics'"。
            // 实测耗时约 3 秒，不值得为省这点时间而承担编译失败风险。
            var ohpm = Path.Combine(cfg.devEcoHome, "tools", "ohpm", "bin", "ohpm.bat");
            Debug.Log("[Harmony] ④ ohpm install");
            if (!RunCli(ohpm, "install", harProject, cfg.cliEnv(), out var ohpmOut, out var ohpmCode))
            {
                error = "ohpm install 启动失败：" + ohpmOut;
                return null;
            }
            if (ohpmCode != 0)
            {
                error = $"ohpm install 失败（exit={ohpmCode}）\n{Tail(ohpmOut, 12)}";
                return null;
            }

            // ---------- ⑤ hvigor assembleHap ----------
            var hvigor = Path.Combine(cfg.devEcoHome, "tools", "hvigor", "bin", "hvigorw.bat");
            var buildMode = buildParam.IsBuildRelease ? "release" : "debug";
            var hvigorArgs = $"--mode module -p product=default -p module=entry@default " +
                             $"-p buildMode={buildMode} assembleHap --no-daemon";
            Debug.Log("[Harmony] ⑤ hvigor assembleHap (" + buildMode + ")");
            var buildStartedAt = DateTime.Now;
            if (!RunCli(hvigor, hvigorArgs, harProject, cfg.cliEnv(), out var hvOut, out var hvCode))
            {
                error = "hvigor 启动失败：" + hvOut;
                return null;
            }

            var hap = FindNewestHap(harProject, buildStartedAt);
            if (hap == null)
            {
                error = BuildHarmonyErrorHint(hvOut, hvCode);
                return null;
            }

            Debug.Log($"[Harmony] ⑤ 完成: {hap}");
            return hap;
        }

        /// <summary>hvigor 失败时给出可定位的中文原因。</summary>
        private static string BuildHarmonyErrorHint(string hvOut, int exitCode)
        {
            // ArkTS 编译期「找不到模块」：绝大多数是导出目录结构错乱（源码被挪进 tuanjieLib），
            // 而不是依赖没装。要区分：报错里带具体模块路径 → 结构问题；
            // 报的是 @thinkingdata/analytics / OHKitSdk 这类依赖名 → 才可能是 ohpm 没装全。
            if (hvOut.Contains("Cannot find module"))
            {
                if (hvOut.Contains("'@thinkingdata/analytics'") || hvOut.Contains("'OHKitSdk'"))
                    return "鸿蒙依赖未安装（Cannot find module '@thinkingdata/analytics' / 'OHKitSdk'）。\n" +
                           "Unity 重新导出会改写 oh-package.json5，需要重新执行 ohpm install。\n\n" + Tail(hvOut, 10);

                return "ArkTS 找不到源码模块（Cannot find module）。\n" +
                       "最常见原因：导出目录里残留了上一轮的 tuanjieLib 模块，\n" +
                       "导致 Unity 把业务源码导出成双模块结构（源码进了 tuanjieLib/，entry/ 变薄壳），\n" +
                       "于是 entry 里的 import '../common/xxx' 解析不到。\n" +
                       "解决：删除整个导出目录后重新导出（流水线已自动清空，若仍出现请检查是否有进程占用目录）。\n\n" +
                       Tail(hvOut, 10);
            }

            if (hvOut.Contains("certificate has expired") || hvOut.Contains("11013002"))
                return "签名材料已过期。\n" +
                       "请在华为 AppGallery Connect 重新申请 profile/证书，并更新 harmony_toolchain.json " +
                       "中 signingKeystoreDir 指向的目录下的 cer / p7b 文件。\n\n" + Tail(hvOut, 10);

            if (hvOut.Contains("DEVECO_SDK_HOME"))
                return "hvigor 找不到 OpenHarmony SDK。请确认 harmony_toolchain.json 里的 devEcoHome 是否正确。\n\n" +
                       Tail(hvOut, 10);

            return $"hvigor 构建失败（exit={exitCode}）\n" + Tail(hvOut, 15);
        }

        private static string Tail(string text, int lines)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var all = text.Replace("\r\n", "\n").Split('\n');
            var take = Math.Min(lines, all.Length);
            return string.Join("\n", all, all.Length - take, take);
        }

        /// <summary>
        /// 清理上一次导出的**构建产物**（entry/build、entry/outputs）。
        ///
        /// 必须清掉整个导出目录，不能只清 entry/build：
        /// Unity 导出器会检测目标目录里是否已存在 tuanjieLib 模块。
        ///   · 全新空目录 → 单模块结构，源码全部落在 entry/src/main/ets/（正确）
        ///   · 目录已存在 → 双模块结构，源码被挪进 tuanjieLib/，entry 变成薄壳，
        ///     随后 entry/oh-package.json5 里找不到 ../common/GlobalThisUtil，
        ///     ArkTS 编译报 "Cannot find module" 而失败。
        ///
        /// 删除方式：逐文件删除而不是 Directory.Delete(path, true)。
        /// Unity 导出的 .so 是硬链接（与 tuanjieLib/libs 共享 inode），
        /// Mono 的递归删目录会抛 "参数错误 : 'arm64-v8a'"；
        /// File.Delete 对硬链接是安全的（只减少链接计数，不动原文件）。
        /// </summary>
        private static void CleanHarmonyExportDir(string dir)
        {
            var root = Path.GetFullPath($"{Application.dataPath}/../output");
            var full = Path.GetFullPath(dir);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[Harmony] 导出目录不在 output 下，跳过清理: " + full);
                return;
            }
            if (!Directory.Exists(full)) return;

            try
            {
                DeleteDirContents(full);
                Debug.Log("[Harmony] 已清空导出目录内容: " + full);
            }
            catch (Exception e)
            {
                // 清不掉不阻断导出，但必须告警：残留会让 Unity 走错模块结构分支
                Debug.LogWarning("[Harmony] 清理导出目录失败（可能残留 tuanjieLib 导致模块结构错误）: " + e.Message);
            }
        }

        /// <summary>逐文件删除目录内容，再删除空目录（规避硬链接导致的递归删失败）。</summary>
        private static void DeleteDirContents(string dir)
        {
            foreach (var file in Directory.GetFiles(dir))
            {
                try { File.Delete(file); }
                catch (Exception e) { Debug.LogWarning($"[Harmony] 删除文件失败 {file}: {e.Message}"); }
            }

            foreach (var sub in Directory.GetDirectories(dir))
            {
                try
                {
                    DeleteDirContents(sub);
                    Directory.Delete(sub, false);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Harmony] 删除目录失败 {sub}: {e.Message}");
                }
            }
        }

        /// <summary>找本次构建新生成的 .hap（按修改时间过滤，避免拿到上次的旧包）。</summary>
        private static string FindNewestHap(string exportDir, DateTime since)
        {
            var outDir = Path.Combine(exportDir, "entry", "build", "default", "outputs");
            if (!Directory.Exists(outDir)) return null;
            var haps = Directory.GetFiles(outDir, "*.hap", SearchOption.AllDirectories)
                .Where(p => File.GetLastWriteTime(p) >= since)
                .OrderByDescending(File.GetLastWriteTime)
                .ToList();
            return haps.Count > 0 ? haps[0] : null;
        }

        /// <summary>调用外部命令行工具（.bat），实时把输出刷到 Unity Console。</summary>
        private static bool RunCli(string exePath, string args, string workingDir,
            IDictionary<string, string> env, out string output, out int exitCode)
        {
            output = "";
            exitCode = -1;
            if (!File.Exists(exePath))
            {
                output = "可执行文件不存在: " + exePath;
                return false;
            }
            if (!Directory.Exists(workingDir))
            {
                output = "工作目录不存在: " + workingDir;
                return false;
            }

            // .bat 必须经 cmd.exe 执行；引号包住带空格的路径
            var psi = new ProcessStartInfo("cmd.exe", "/c \"" + exePath + "\" " + args)
            {
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (env != null)
            {
                foreach (var kv in env) psi.EnvironmentVariables[kv.Key] = kv.Value;
            }
            try
            {
                // cmd 输出是 GBK；某些环境没有 936 编码页，失败则退回默认编码
                psi.StandardOutputEncoding = Encoding.GetEncoding(936);
                psi.StandardErrorEncoding = Encoding.GetEncoding(936);
            }
            catch
            {
                // ignore
            }

            var buffer = new StringBuilder();
            using (var proc = new Process { StartInfo = psi })
            {
                proc.OutputDataReceived += (s, e) =>
                {
                    if (e.Data == null) return;
                    lock (buffer) buffer.AppendLine(e.Data);
                    Debug.Log("[Harmony] " + e.Data);
                };
                proc.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data == null) return;
                    lock (buffer) buffer.AppendLine("[ERR] " + e.Data);
                    Debug.LogWarning("[Harmony] " + e.Data);
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();
                exitCode = proc.ExitCode;
            }

            lock (buffer) output = buffer.ToString();
            return true;
        }

        /// <summary>每天预留的 versionCode 号段数量（当天可构建次数上限 + 1）。</summary>
        private const int HarmonyVersionSlotsPerDay = 2000;

        /// <summary>
        /// 鸿蒙 versionCode 编码：yyMMdd × 2000 + 当天序号。
        ///
        /// 为什么不用 yyMMdd × 10000：10000 倍会超出 int32 上限
        /// （yyMMdd 最大 991231，991231 × 10000 = 9,912,310,000 &gt; int32 的 2,147,483,647），
        /// 溢出成负数后「与上一次取较大者」分支恒不成立，日期段实际失效，
        /// versionCode 退化成「上次 + 1」，也无法保证跨天递增。
        ///
        /// × 2000 时末日最大值 = 991231 × 2000 + 1999 = 1,982,463,999，仍在 int32 内。
        /// 每天 2000 个号段足够（一天内构建 2000 次远超实际需要），
        /// 且跨天严格递增（日期前缀每天 +2000 > 单日最大序号 1999）。
        /// </summary>
        private static int GenHarmonyBuildNumber()
        {
            var now = DateTime.Now;
            var dayPrefix = int.Parse($"{now.Year - 2000}{now.Month:00}{now.Day:00}") *
                            HarmonyVersionSlotsPerDay;

            var prev = PlayerSettings.OpenHarmony.bundleVersionCode;

            // 从存量 versionCode 反推当天已用到的序号
            var seq = 1;
            if (prev >= dayPrefix && prev < dayPrefix + HarmonyVersionSlotsPerDay)
                seq = (prev - dayPrefix) + 1;
            if (seq > HarmonyVersionSlotsPerDay - 1) seq = HarmonyVersionSlotsPerDay - 1;

            var code = dayPrefix + seq;
            // 兜底：存量来自旧编码或被手工改大时，绝不产出比它小的值（否则真机装不上）
            if (prev >= code) code = prev + 1;
            return code;
        }

        #endregion

        /// <summary>
        /// 自检入口（零参）：在已有导出工程上重放 ArkTS 补丁并注入签名配置，不触发 Unity 导出。
        /// 用于改了补丁代码后快速验证，菜单：Tools/自动化打包/Harmony/自检补丁与签名
        /// </summary>
        public static string VerifyHarmonyPostProcess()
        {
            var cfg = LoadHarmonyConfig();
            var projectDir = Path.GetFullPath($"{Application.dataPath}/../output/{cfg.outputDirName}/{cfg.projectDirName}");
            if (!File.Exists(Path.Combine(projectDir, "build-profile.json5")))
            {
                // 回退到最近一次导出的工程目录
                var outputRoot = Path.GetFullPath($"{Application.dataPath}/../output");
                var candidates = Directory.Exists(outputRoot)
                    ? Directory.GetDirectories(outputRoot)
                    : new string[0];
                projectDir = candidates
                    .Select(d => Path.Combine(d, "build-profile.json5"))
                    .Where(File.Exists)
                    .Select(Path.GetDirectoryName)
                    .OrderByDescending(d => Directory.GetLastWriteTime(d))
                    .FirstOrDefault() ?? projectDir;
            }

            if (!File.Exists(Path.Combine(projectDir, "build-profile.json5")))
                return "自检失败：没有找到可用的导出工程，请先执行一次 Unity 导出。";

            var lines = new List<string> { "导出工程: " + projectDir };

            if (!HarmonyArkTsPatcher.Apply(projectDir, out var patchLog, out var patchError))
                return "自检失败（ArkTS 补丁）：" + patchError;
            lines.AddRange(patchLog);

            if (!HarmonySigning.Inject(projectDir, cfg, false, out var signError))
                return "自检失败（签名注入）：" + signError;
            lines.Add("签名配置已注入");

            var result = string.Join("\n", lines);
            Debug.Log("[Harmony] 自检通过\n" + result);
            return result;
        }

        /// <summary>
        /// 回归自检（零参）：从 Unity 原始 ArkTS 模板出发，在临时目录里完整跑一遍补丁，
        /// 校验所有 marker 都正确落地，并验证重复执行是幂等的。
        /// 用于改了补丁代码后快速回归，菜单：Tools/自动化打包/Harmony/回归自检补丁
        /// </summary>
        public static string VerifyHarmonyPatchFromTemplate()
        {
            var unityEditorDir = Path.GetDirectoryName(EditorApplication.applicationPath);
            var templateDir = Path.Combine(unityEditorDir ?? "", "Data", "PlaybackEngines",
                "OpenHarmonyPlayer", "OpenHarmonyAppTemplate");
            var templateEts = Path.Combine(templateDir, "entry", "src", "main", "ets", "ability");
            if (!Directory.Exists(templateEts))
                return "回归自检失败：找不到 Unity ArkTS 模板目录 " + templateEts;

            var workDir = Path.GetFullPath($"{Application.dataPath}/../output/_patchtest");
            if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
            var workEts = Path.Combine(workDir, "entry", "src", "main", "ets", "ability");
            Directory.CreateDirectory(workEts);

            foreach (var f in new[] { "TuanjiePlayerAbilityBase.ets", "TuanjiePlayerAbility.ets" })
            {
                var src = Path.Combine(templateEts, f);
                if (!File.Exists(src)) return "回归自检失败：模板缺少 " + f;
                File.Copy(src, Path.Combine(workEts, f), true);
            }

            var lines = new List<string>();

            // 第一次执行：应当全部 PATCHED / ADDED
            if (!HarmonyArkTsPatcher.Apply(workDir, out var log1, out var err1))
                return "回归自检失败（首次执行）：" + err1 + "\n" + string.Join("\n", log1);
            lines.Add("首次执行：");
            lines.AddRange(log1);

            // 第二次执行：应当全部 ALREADY（幂等）
            if (!HarmonyArkTsPatcher.Apply(workDir, out var log2, out var err2))
                return "回归自检失败（重复执行）：" + err2 + "\n" + string.Join("\n", log2);
            var notIdempotent = log2.Where(l => l.StartsWith("PATCHED") || l.StartsWith("ADDED")).ToList();
            if (notIdempotent.Count > 0)
                return "回归自检失败：补丁不是幂等的，以下项被重复修改：\n" + string.Join("\n", notIdempotent);
            lines.Add("重复执行：全部 ALREADY（幂等 OK）");

            // 校验所有 marker 落地
            var baseText = File.ReadAllText(Path.Combine(workEts, "TuanjiePlayerAbilityBase.ets"));
            var entryText = File.ReadAllText(Path.Combine(workEts, "TuanjiePlayerAbility.ets"));
            var guardPath = Path.Combine(workDir, "entry", "src", "main", "ets", "common",
                "EngineLifecycleGuard.ets");
            var checks = new List<Tuple<string, bool>>
            {
                Tuple.Create("guard 文件已生成", File.Exists(guardPath)),
                Tuple.Create("import EngineLifecycleGuard",
                    baseText.Contains("import { EngineLifecycleGuard } from '../common/EngineLifecycleGuard'")),
                Tuple.Create("onForeground 解除闸门",
                    baseText.Contains("EngineLifecycleGuard.leaveBackground();\n    this.onResume();")),
                Tuple.Create("onBackground 进入闸门",
                    baseText.Contains("EngineLifecycleGuard.enterBackground();")),
                Tuple.Create("RESUMED 解除闸门",
                    baseText.Contains("EngineLifecycleGuard.leaveBackground();\n          this.onResume();")),
                Tuple.Create("onResume 闸门校验",
                    baseText.Contains("if (!EngineLifecycleGuard.canResumeEngine()) {")),
                Tuple.Create("onBackground 无条件 nativeOnPause",
                    baseText.Contains("Tuanjie.nativeOnPause();")),
                Tuple.Create("entry onForeground 时间戳",
                    entryText.Contains("[TuanjiePlayerAbility] onForeground at ")),
                Tuple.Create("entry onBackground 时间戳",
                    entryText.Contains("[TuanjiePlayerAbility] onBackground at ")),
            };

            var failed = checks.Where(c => !c.Item2).Select(c => c.Item1).ToList();
            if (failed.Count > 0)
                return "回归自检失败：以下校验项未通过：\n- " + string.Join("\n- ", failed) +
                       "\n\n--- 补丁后的 base.ets ---\n" + baseText;

            lines.Add("校验项：");
            lines.AddRange(checks.Select(c => "  [OK] " + c.Item1));

            var result = string.Join("\n", lines);
            Debug.Log("[Harmony] 回归自检通过\n" + result);
            return result;
        }

        #region 工具链配置
        [Serializable]
        private class HarmonyToolchainConfig
        {
            /// <summary>DevEco Studio 安装目录</summary>
            public string devEcoHome = "";
            /// <summary>签名材料目录（含 hexa.p12 / *.cer / *.p7b）</summary>
            public string signingKeystoreDir = "";
            /// <summary>DevEco 加密后的 keyPassword</summary>
            public string keyPassword = "";
            /// <summary>DevEco 加密后的 storePassword</summary>
            public string storePassword = "";
            /// <summary>导出根目录名（相对 output/）</summary>
            public string outputDirName = "Harmony";
            /// <summary>导出工程目录名（相对 output/{outputDirName}/）</summary>
            public string projectDirName = "project";
            /// <summary>打包前是否重新构建 AssetBundle（默认否，AB 已预构建）</summary>
            public bool buildAssetBundles = false;

            public string keystorePath(string fileName) => Path.Combine(signingKeystoreDir, fileName);

            public string certFileName(bool isRelease) => isRelease ? "hexa_release.cer" : "hexa_debug.cer";

            public string profileFileName(bool isRelease) =>
                isRelease ? "hexa_releaseRelease.p7b" : "hexa_debugDebug.p7b";

            /// <summary>hvigor / ohpm 需要的环境变量。</summary>
            public Dictionary<string, string> cliEnv()
            {
                var env = new Dictionary<string, string>();
                var nodeHome = Path.Combine(devEcoHome, "tools", "node");
                var sdkHome = Path.Combine(devEcoHome, "sdk");
                env["NODE_HOME"] = nodeHome;
                env["DEVECO_SDK_HOME"] = sdkHome;
                var path = Environment.GetEnvironmentVariable("PATH") ?? "";
                env["PATH"] = nodeHome + ";" + path;
                return env;
            }
        }

        /// <summary>公开配置（入库，不含密文）</summary>
        private static readonly string HarmonyConfigPath =
            Path.Combine(Application.dataPath, "Middleware/Editor/AppBuilder/harmony_toolchain.json");

        /// <summary>本地私密配置（不入库，含 DevEco 加密密文），存在时覆盖公开配置</summary>
        private static readonly string HarmonyLocalConfigPath =
            Path.Combine(Application.dataPath, "Middleware/Editor/AppBuilder/harmony_toolchain.local.json");

        private static HarmonyToolchainConfig LoadHarmonyConfig()
        {
            var cfg = new HarmonyToolchainConfig();
            if (File.Exists(HarmonyConfigPath))
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(HarmonyConfigPath), cfg);
                }
                catch (Exception e)
                {
                    Debug.LogError("[Harmony] harmony_toolchain.json 解析失败: " + e.Message);
                }
            }

            // 本地私密配置优先（签名密文不入库，只放在被 gitignore 的 local 文件里）
            if (File.Exists(HarmonyLocalConfigPath))
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(HarmonyLocalConfigPath), cfg);
                }
                catch (Exception e)
                {
                    Debug.LogError("[Harmony] harmony_toolchain.local.json 解析失败: " + e.Message);
                }
            }

            if (string.IsNullOrEmpty(cfg.devEcoHome))
                cfg.devEcoHome = @"C:\Program Files\Huawei\DevEco Studio";
            if (string.IsNullOrEmpty(cfg.signingKeystoreDir))
                cfg.signingKeystoreDir = @"D:\GitFork\HexWordTemp\platform\Harmony";
            if (string.IsNullOrEmpty(cfg.outputDirName)) cfg.outputDirName = "Harmony";
            if (string.IsNullOrEmpty(cfg.projectDirName)) cfg.projectDirName = "project";
            return cfg;
        }

        private static bool ValidateHarmonyToolchain(HarmonyToolchainConfig cfg, out string error)
        {
            error = null;
            var problems = new List<string>();

            var hvigor = Path.Combine(cfg.devEcoHome, "tools", "hvigor", "bin", "hvigorw.bat");
            var ohpm = Path.Combine(cfg.devEcoHome, "tools", "ohpm", "bin", "ohpm.bat");
            var sdk = Path.Combine(cfg.devEcoHome, "sdk");
            var node = Path.Combine(cfg.devEcoHome, "tools", "node");
            if (!File.Exists(hvigor)) problems.Add("缺少 hvigorw: " + hvigor);
            if (!File.Exists(ohpm)) problems.Add("缺少 ohpm: " + ohpm);
            if (!Directory.Exists(sdk)) problems.Add("缺少 OpenHarmony SDK: " + sdk);
            if (!Directory.Exists(node)) problems.Add("缺少 node: " + node);

            foreach (var f in new[] { "hexa.p12" })
            {
                if (!File.Exists(cfg.keystorePath(f))) problems.Add($"缺少签名材料: {cfg.keystorePath(f)}");
            }
            foreach (var f in new[] { "hexa_debug.cer", "hexa_release.cer", "hexa_debugDebug.p7b", "hexa_releaseRelease.p7b" })
            {
                if (!File.Exists(cfg.keystorePath(f))) problems.Add($"缺少签名材料: {cfg.keystorePath(f)}");
            }

            if (string.IsNullOrEmpty(cfg.keyPassword))
                problems.Add("缺少 keyPassword（签名密文不入库，请写入 harmony_toolchain.local.json）");
            if (string.IsNullOrEmpty(cfg.storePassword))
                problems.Add("缺少 storePassword（签名密文不入库，请写入 harmony_toolchain.local.json）");

            if (problems.Count > 0)
            {
                error = "鸿蒙工具链检查未通过：\n- " + string.Join("\n- ", problems) +
                        "\n\n公开配置：harmony_toolchain.json\n私密配置：harmony_toolchain.local.json（不入库，需自行创建）";
                return false;
            }
            return true;
        }

        #endregion

        #region 签名配置注入

        private static class HarmonySigning
        {
            private static readonly Regex SigningBlock =
                new Regex("\"signingConfigs\"\\s*:\\s*\\[.*?\\]", RegexOptions.Singleline);

            /// <summary>
            /// 把签名配置写回 build-profile.json5。
            /// Unity 导出的模板里 signingConfigs 恒为 []，不注入的话 hvigor 无法签名。
            /// </summary>
            public static bool Inject(string exportDir, HarmonyToolchainConfig cfg, bool isRelease, out string error)
            {
                error = null;
                var bp = Path.Combine(exportDir, "build-profile.json5");
                if (!File.Exists(bp))
                {
                    error = "找不到 build-profile.json5，导出目录结构异常: " + exportDir;
                    return false;
                }

                var dir = cfg.signingKeystoreDir.Replace('\\', '/');
                var cert = $"{dir}/{cfg.certFileName(isRelease)}";
                var profile = $"{dir}/{cfg.profileFileName(isRelease)}";
                var store = $"{dir}/hexa.p12";

                var block =
                    "\"signingConfigs\": [\n" +
                    "      {\n" +
                    "        \"name\": \"default\",\n" +
                    "        \"type\": \"HarmonyOS\",\n" +
                    "        \"material\": {\n" +
                    $"          \"certpath\": \"{cert}\",\n" +
                    "          \"keyAlias\": \"hexa\",\n" +
                    $"          \"keyPassword\": \"{cfg.keyPassword}\",\n" +
                    $"          \"profile\": \"{profile}\",\n" +
                    "          \"signAlg\": \"SHA256withECDSA\",\n" +
                    $"          \"storeFile\": \"{store}\",\n" +
                    $"          \"storePassword\": \"{cfg.storePassword}\"\n" +
                    "        }\n" +
                    "      }\n" +
                    "    ]";

                var text = File.ReadAllText(bp);
                if (!SigningBlock.IsMatch(text))
                {
                    error = "build-profile.json5 里找不到 signingConfigs 节点，无法注入签名配置";
                    return false;
                }

                text = SigningBlock.Replace(text, block, 1);
                File.WriteAllText(bp, text, new UTF8Encoding(false));
                Debug.Log($"[Harmony] 签名配置已注入（cert={cert}）");
                return true;
            }
        }

        #endregion

        #region ArkTS 加固补丁

        /// <summary>
        /// 把「MiniAudio 后台崩溃」的 ArkTS 加固补丁重放到导出工程上。
        ///
        /// 为什么必须每次导出都重放：Unity 导出时会重建整个工程目录，
        /// 手改的 .ets 会被原样覆盖回模板版本，不重放等于修复失效。
        ///
        /// 与 tools/harmony_patch/apply_harmony_patch.py 逻辑等价（幂等，可重复执行）。
        /// </summary>
        private static class HarmonyArkTsPatcher
        {
            private struct Layout
            {
                public string Name;
                public string Base;      // TuanjiePlayerAbilityBase.ets
                public string Guard;     // EngineLifecycleGuard.ets
                public string Entry;     // TuanjiePlayerAbility.ets
                public string Exported;  // 双模块布局才有
            }

            private struct ReplacePair
            {
                public string Old;
                public string New;
                public string Marker;    // 幂等标记，必须是补丁独有串
                public ReplacePair(string oldText, string newText, string marker)
                {
                    Old = oldText;
                    New = newText;
                    Marker = marker;
                }
            }

            private static readonly Layout[] Layouts =
            {
                new Layout
                {
                    Name = "A-single-module",
                    Base = Path.Combine("entry", "src", "main", "ets", "ability", "TuanjiePlayerAbilityBase.ets"),
                    Guard = Path.Combine("entry", "src", "main", "ets", "common", "EngineLifecycleGuard.ets"),
                    Entry = Path.Combine("entry", "src", "main", "ets", "ability", "TuanjiePlayerAbility.ets"),
                    Exported = null
                },
                new Layout
                {
                    Name = "B-dual-module",
                    Base = Path.Combine("tuanjieLib", "src", "main", "ets", "ability", "TuanjiePlayerAbilityBase.ets"),
                    Guard = Path.Combine("tuanjieLib", "src", "main", "ets", "common", "EngineLifecycleGuard.ets"),
                    Entry = Path.Combine("entry", "src", "main", "ets", "ability", "TuanjiePlayerAbility.ets"),
                    Exported = Path.Combine("tuanjieLib", "exported.ets")
                }
            };

            /// <summary>补丁源文件（放在 Assets 下，随代码一起版本管理）。</summary>
            private static string GuardSourcePath => Path.Combine(Application.dataPath,
                "Middleware", "Editor", "AppBuilder", "HarmonyPatch", "EngineLifecycleGuard.ets");

            public static bool Apply(string root, out List<string> log, out string error)
            {
                log = new List<string>();
                error = null;

                Layout? layout = null;
                foreach (var cand in Layouts)
                {
                    var p = Path.Combine(root, cand.Base);
                    if (File.Exists(p) && File.ReadAllText(p).Contains("TuanjiePlayerAbilityBase"))
                    {
                        layout = cand;
                        break;
                    }
                }
                if (layout == null)
                {
                    error = "未识别的导出布局（找不到 TuanjiePlayerAbilityBase.ets）";
                    return false;
                }
                log.Add($"LAYOUT  {layout.Value.Name}");

                // 0) 补齐 entry 侧公共文件（仅双模块布局需要）
                //
                // Unity 导出双模块结构时，业务公共源码落在 tuanjieLib/src/main/ets/，
                // 而 entry/ 只拿到 ability/ 与 pages/ 两个模板文件。
                // 但 entry/ability/TuanjiePlayerAbility.ets 里有
                //     import { SetToGlobalThis } from '../common/GlobalThisUtil';
                // 这个相对路径是相对 entry/ 解析的，而该文件并不在 entry/common/ 下，
                // 于是 ArkTS 编译报 "Cannot find module '../common/GlobalThisUtil'"。
                //
                // 团队原始可出包工程（harmony/ 与 output/harmoney_1.3.3）正是把 common/ 与
                // log/ 手工同步到了 entry/。这里在每次导出后自动完成同样的同步。
                if (!SyncEntryCommonFiles(root, layout.Value, log, out error)) return false;

                // 1) 新增 EngineLifecycleGuard.ets
                var guardDst = Path.Combine(root, layout.Value.Guard);
                if (File.Exists(guardDst))
                {
                    log.Add("ALREADY " + layout.Value.Guard);
                }
                else
                {
                    if (!File.Exists(GuardSourcePath))
                    {
                        error = "找不到补丁源文件: " + GuardSourcePath;
                        return false;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(guardDst));
                    File.Copy(GuardSourcePath, guardDst, true);
                    log.Add("ADDED  " + layout.Value.Guard);
                }

                // 2) TuanjiePlayerAbilityBase.ets
                if (!PatchFile(Path.Combine(root, layout.Value.Base), BasePairs(), true, log, out error)) return false;

                // 3) exported.ets（仅双模块布局）
                if (!string.IsNullOrEmpty(layout.Value.Exported))
                {
                    if (!PatchFile(Path.Combine(root, layout.Value.Exported), ExportedPairs(), true, log, out error))
                        return false;
                }
                else
                {
                    log.Add("SKIP   (单模块布局，无需 exported.ets)");
                }

                // 4) entry 侧时间戳日志
                var entryPath = Path.Combine(root, layout.Value.Entry);
                if (File.Exists(entryPath))
                {
                    var entryText = File.ReadAllText(entryPath);
                    var head = entryText.Split(new[] { "export default class" }, StringSplitOptions.None)[0];
                    if (!head.Contains("TuanjieLog"))
                    {
                        var importPairs = entryText.Contains("from 'tuanjieLib'")
                            ? new[]
                            {
                                new ReplacePair(
                                    "import { TuanjiePlayerAbilityBase } from 'tuanjieLib';",
                                    "import { TuanjiePlayerAbilityBase, TuanjieLog } from 'tuanjieLib';",
                                    "import { TuanjiePlayerAbilityBase, TuanjieLog } from 'tuanjieLib';")
                            }
                            : new[]
                            {
                                new ReplacePair(
                                    "import { TuanjiePlayerAbilityBase } from './TuanjiePlayerAbilityBase';",
                                    "import { TuanjiePlayerAbilityBase } from './TuanjiePlayerAbilityBase';\n" +
                                    "import { TuanjieLog } from '../common/TuanjieLog';",
                                    "import { TuanjieLog } from '../common/TuanjieLog';")
                            };
                        if (!PatchFile(entryPath, importPairs, true, log, out error)) return false;
                    }

                    if (!PatchFile(entryPath, EntryPairs(), false, log, out error)) return false;
                }
                else
                {
                    log.Add("SKIP   " + layout.Value.Entry + " 文件不存在");
                }

                log.Add("补丁完成，失败项: 0");

                // 完整性校验：任何一处 marker 没落地都必须报错。
                // （历史上 entry 侧的时间戳因为行尾不匹配被静默跳过，这里补上兜底。）
                var missing = new List<string>();
                var baseText = File.ReadAllText(Path.Combine(root, layout.Value.Base));
                var entryFull = File.Exists(Path.Combine(root, layout.Value.Entry))
                    ? File.ReadAllText(Path.Combine(root, layout.Value.Entry))
                    : "";

                void Require(bool ok, string what)
                {
                    if (!ok) missing.Add(what);
                }

                Require(File.Exists(guardDst), "EngineLifecycleGuard.ets 未生成");
                Require(baseText.Contains("import { EngineLifecycleGuard } from '../common/EngineLifecycleGuard'"),
                    "base.ets 缺少 EngineLifecycleGuard import");
                Require(baseText.Contains("EngineLifecycleGuard.leaveBackground();"),
                    "base.ets 缺少 leaveBackground");
                Require(baseText.Contains("EngineLifecycleGuard.enterBackground();"),
                    "base.ets 缺少 enterBackground");
                Require(baseText.Contains("if (!EngineLifecycleGuard.canResumeEngine()) {"),
                    "base.ets 缺少 canResumeEngine 闸门校验");
                Require(baseText.Contains("Tuanjie.nativeOnPause();"),
                    "base.ets 缺少无条件 nativeOnPause");
                Require(entryFull.Contains("[TuanjiePlayerAbility] onForeground at "),
                    "entry.ets 缺少 onForeground 时间戳");
                Require(entryFull.Contains("[TuanjiePlayerAbility] onBackground at "),
                    "entry.ets 缺少 onBackground 时间戳");

                // 双模块布局下，entry 的相对 import 必须能在 entry/ 下解析到，
                // 否则 ArkTS 编译期报 "Cannot find module '../common/xxx'"。
                var entryEtsDir = Path.Combine(root, "entry", "src", "main", "ets");
                var entryImportsCommon = entryFull.Contains("from '../common/");
                Require(!entryImportsCommon ||
                        File.Exists(Path.Combine(entryEtsDir, "common", "GlobalThisUtil.ts")),
                    "entry/common/GlobalThisUtil.ts 缺失（entry 的相对 import 无法解析）");

                if (missing.Count > 0)
                {
                    error = "补丁未完整落地：\n- " + string.Join("\n- ", missing);
                    return false;
                }
                log.Add("完整性校验: 9/9 项通过");
                return true;
            }

            /// <summary>
            /// 把双模块布局下位于 tuanjieLib 的公共源码同步到 entry。
            ///
            /// 背景：Unity 导出双模块结构时，业务公共源码在 tuanjieLib/src/main/ets/，
            /// 而 entry/ability/TuanjiePlayerAbility.ets 里的
            ///     import { SetToGlobalThis } from '../common/GlobalThisUtil';
            /// 是相对 entry/ 解析的，该文件却不在 entry/common/ 下，
            /// ArkTS 编译即报 "Cannot find module '../common/GlobalThisUtil'"。
            ///
            /// 处理：从 tuanjieLib 复制 common/ 与 log/ 到 entry（幂等，已存在则跳过）。
            /// 单模块布局（源码本来就在 entry/）无需处理。
            /// </summary>
            private static bool SyncEntryCommonFiles(string root, Layout layout,
                List<string> log, out string error)
            {
                error = null;
                var entryEts = Path.Combine(root, "entry", "src", "main", "ets");
                var libEts = Path.Combine(root, "tuanjieLib", "src", "main", "ets");

                // 单模块布局：源码本来就在 entry/ 下
                if (!Directory.Exists(libEts))
                {
                    log.Add("SKIP   补齐 entry 公共文件（单模块布局，源码已在 entry/）");
                    return true;
                }

                // entry 侧实际引用到的公共文件（相对 entry/src/main/ets）
                var needed = new[]
                {
                    Path.Combine("common", "Constants.ets"),
                    Path.Combine("common", "GlobalThisUtil.ts"),
                    Path.Combine("common", "PlayerPrefs.ets"),
                    Path.Combine("common", "TuanjieLog.ets"),
                    Path.Combine("log", "Logger.ts")
                };

                var copied = 0;
                var skipped = 0;
                foreach (var rel in needed)
                {
                    var src = Path.Combine(libEts, rel);
                    var dst = Path.Combine(entryEts, rel);

                    if (File.Exists(dst))
                    {
                        skipped++;
                        continue;
                    }

                    if (!File.Exists(src))
                    {
                        // tuanjieLib 里没有 —— 只能从团队工程模板取
                        var fallback = Path.Combine(Application.dataPath, "..", "harmony",
                            "entry", "src", "main", "ets", rel);
                        if (File.Exists(fallback))
                        {
                            src = Path.GetFullPath(fallback);
                        }
                        else
                        {
                            log.Add("MISS   " + rel + "（tuanjieLib 与 harmony/ 模板中都没有，跳过）");
                            continue;
                        }
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    File.Copy(src, dst, true);
                    copied++;
                    log.Add("SYNC   entry/" + rel + "  <-  " +
                            (src.StartsWith(libEts) ? "tuanjieLib" : "harmony/模板"));
                }

                log.Add($"补齐 entry 公共文件: 新增 {copied} 个，已存在 {skipped} 个");
                return true;
            }

            private static ReplacePair[] BasePairs()
            {
                return new[]
                {
                    // 2.1 import
                    new ReplacePair(
                        "import { VideoPlayerProxy } from '../utils/VideoPlayerProxy'",
                        "import { VideoPlayerProxy } from '../utils/VideoPlayerProxy'\n" +
                        "import { EngineLifecycleGuard } from '../common/EngineLifecycleGuard'",
                        "import { EngineLifecycleGuard } from '../common/EngineLifecycleGuard'"),

                    // 2.2 onForeground 先解除闸门
                    new ReplacePair(
                        "    TuanjieLog.info('%{public}s', 'TuanjiePlayerAbility onForeground');\n    this.onResume();",
                        "    TuanjieLog.info('%{public}s', 'TuanjiePlayerAbility onForeground');\n" +
                        "    // 回到前台：先解除后台闸门，再恢复引擎\n" +
                        "    EngineLifecycleGuard.leaveBackground();\n" +
                        "    this.onResume();",
                        "EngineLifecycleGuard.leaveBackground();\n    this.onResume();"),

                    // 2.3 onBackground 无条件 nativeOnPause
                    new ReplacePair(
                        "    TuanjieLog.info('%{public}s', 'TuanjiePlayerAbility onBackground');\n    this.onPause();",
                        "    TuanjieLog.info('%{public}s', 'TuanjiePlayerAbility onBackground');\n" +
                        "    EngineLifecycleGuard.enterBackground();\n" +
                        "    this.onPause();\n" +
                        "    // [崩溃加固] 无条件暂停引擎主循环。原实现只在非视频播放态才调用 nativeOnPause(),\n" +
                        "    // 视频播放态下引擎继续运行, MiniAudio 线程在 OHAudio 流被系统回收后仍被调度,\n" +
                        "    // 回调到失效对象上的 NULL 函数指针 -> libtuanjie.so 内 CPP_CRASH / SIGSEGV @ pc=0x0。\n" +
                        "    // nativeOnPause 为幂等暂停语义, 重复下发安全。\n" +
                        "    Tuanjie.nativeOnPause();",
                        "EngineLifecycleGuard.enterBackground();"),

                    // 2.4 windowStage RESUMED 也要解除闸门
                    new ReplacePair(
                        "          TuanjieLog.info('windowStage resumed.');\n          this.onResume();",
                        "          TuanjieLog.info('windowStage resumed.');\n" +
                        "          // RESUMED 同样是『回到前台』的信号, 必须先解除闸门,\n" +
                        "          // 否则某些机型上 RESUMED 先于 onForeground 触发时会误挡引擎恢复。\n" +
                        "          EngineLifecycleGuard.leaveBackground();\n" +
                        "          this.onResume();",
                        "EngineLifecycleGuard.leaveBackground();\n          this.onResume();"),

                    // 2.5 onResume 增加闸门校验
                    new ReplacePair(
                        "  private onResume(): void {\n    if (VideoPlayerProxy.getInstance().IsPlaying()) {",
                        "  private onResume(): void {\n" +
                        "    // [崩溃加固] 后台状态下拒绝恢复引擎, 防止后台被误唤醒导致音频线程重新被调度。\n" +
                        "    if (!EngineLifecycleGuard.canResumeEngine()) {\n" +
                        "      return;\n" +
                        "    }\n" +
                        "    if (VideoPlayerProxy.getInstance().IsPlaying()) {",
                        "if (!EngineLifecycleGuard.canResumeEngine()) {")
                };
            }

            private static ReplacePair[] ExportedPairs()
            {
                return new[]
                {
                    new ReplacePair(
                        "export { TuanjiePlayerView } from './src/main/ets/pages/components/TuanjiePlayerView'",
                        "export { TuanjiePlayerView } from './src/main/ets/pages/components/TuanjiePlayerView'\n" +
                        "export { EngineLifecycleGuard } from './src/main/ets/common/EngineLifecycleGuard'\n" +
                        "export { TuanjieLog } from './src/main/ets/common/TuanjieLog'",
                        "export { EngineLifecycleGuard } from './src/main/ets/common/EngineLifecycleGuard'")
                };
            }

            private static ReplacePair[] EntryPairs()
            {
                return new[]
                {
                    new ReplacePair(
                        "  onForeground(): void {\n    super.onForeground();\n  }",
                        "  onForeground(): void {\n" +
                        "    TuanjieLog.info('%{public}s', '[TuanjiePlayerAbility] onForeground at ' + new Date().toISOString());\n" +
                        "    super.onForeground();\n" +
                        "  }",
                        "[TuanjiePlayerAbility] onForeground at "),
                    new ReplacePair(
                        "  onBackground(): void {\n    super.onBackground();\n  }",
                        "  onBackground(): void {\n" +
                        "    TuanjieLog.info('%{public}s', '[TuanjiePlayerAbility] onBackground at ' + new Date().toISOString());\n" +
                        "    super.onBackground();\n" +
                        "  }",
                        "[TuanjiePlayerAbility] onBackground at ")
                };
            }

            /// <summary>
            /// 幂等替换：命中 marker 视为已打过；命中 old 则只替换第一处；
            /// 都没命中且 required 为真则失败。
            ///
            /// 注意行尾：Unity 导出的 .ets **行尾不统一**（TuanjiePlayerAbility.ets 是 CRLF，
            /// TuanjiePlayerAbilityBase.ets 是 LF）。这里按文件实际行尾把替换串的 \n 转换后再匹配，
            /// 既能命中，又不会把整个文件的行尾改掉。
            /// </summary>
            private static bool PatchFile(string path, ReplacePair[] pairs, bool required,
                List<string> log, out string error)
            {
                error = null;
                if (!File.Exists(path))
                {
                    log.Add("SKIP   " + path + "  文件不存在");
                    return true;
                }

                var text = File.ReadAllText(path);
                var crlf = text.Contains("\r\n");
                var changed = false;
                foreach (var p in pairs)
                {
                    var marker = crlf ? p.Marker.Replace("\n", "\r\n") : p.Marker;
                    var oldText = crlf ? p.Old.Replace("\n", "\r\n") : p.Old;
                    var newText = crlf ? p.New.Replace("\n", "\r\n") : p.New;

                    if (text.Contains(marker)) continue;   // 已打过
                    if (text.Contains(oldText))
                    {
                        text = ReplaceFirst(text, oldText, newText);
                        changed = true;
                    }
                    else if (required)
                    {
                        error = "未找到待替换内容：" + FirstLine(p.Old) + "\n文件: " + path +
                                "\n（该文件行尾：" + (crlf ? "CRLF" : "LF") + "）";
                        log.Add("FAIL   " + path);
                        return false;
                    }
                }

                if (changed) File.WriteAllText(path, text, new UTF8Encoding(false));
                log.Add((changed ? "PATCHED " : "ALREADY ") + path);
                return true;
            }

            private static string ReplaceFirst(string src, string oldText, string newText)
            {
                var idx = src.IndexOf(oldText, StringComparison.Ordinal);
                if (idx < 0) return src;
                return src.Substring(0, idx) + newText + src.Substring(idx + oldText.Length);
            }

            private static string FirstLine(string s)
            {
                var lines = s.Replace("\r\n", "\n").Split('\n');
                var first = lines.Length > 0 ? lines[0] : s;
                return first.Length > 70 ? first.Substring(0, 70) : first;
            }
        }

        #endregion

        // 实现 Harmony 特有设置（目前无额外设置，因为已在主方法中完成）
        private static void ApplyHarmonySettings()
        {
            // 如果有额外需要（如修改鸿蒙特定属性），可在此实现
            // 例如：PlayerSettings.OpenHarmony.xxx = ...;
            Debug.Log("Apply Harmony specific settings (if any)");
        }
    }
}
