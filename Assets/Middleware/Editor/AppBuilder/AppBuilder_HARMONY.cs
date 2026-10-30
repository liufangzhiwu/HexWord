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
            PlayerSettings.OpenHarmony.bundleVersionCode = GenBuildNumber();
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
            if (!CleanHarmonyExportDir(harProject, out error)) return null;

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

            // ---------- ④.1 覆盖 OHKitSdk 源码 ----------
            // 必须放在 ohpm install **之后**：ohpm 会按 har 重新解包 oh_modules，
            // 把团队对 sdk 目录的改动（改过 4 个 + 新增 SendOAID/TuanjieNative）冲掉。
            Debug.Log("[Harmony] ④.1 覆盖 OHKitSdk sdk 源码");
            if (!HarmonyArkTsPatcher.ApplyOHKitSdkSourcesAfterInstall(harProject, out var sdkLog, out error))
                return null;
            foreach (var line in sdkLog) Debug.Log("    " + line);

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
            // ArkTS 编译期「找不到模块」：要区分两类——
            //   · @thinkingdata/analytics / OHKitSdk 等依赖名 → ohpm 没装全
            //   · tuanjieLib / ../common/xxx / ../log/xxx 等源码模块 → 导出布局与补丁 import 不匹配
            if (hvOut.Contains("Cannot find module"))
            {
                var missLines = ExtractMissingModuleLines(hvOut);
                var missing = string.Join("\n", missLines.Select(l => "  " + l));

                if (hvOut.Contains("'@thinkingdata/analytics'") || hvOut.Contains("'OHKitSdk'"))
                    return "鸿蒙依赖未安装（Cannot find module '@thinkingdata/analytics' / 'OHKitSdk'）。\n" +
                           "Unity 重新导出会改写 oh-package.json5，需要重新执行 ohpm install。\n\n" +
                           "缺失明细：\n" + missing + "\n\n" + Tail(hvOut, 10);

                return "ArkTS 找不到源码模块（Cannot find module）。\n" +
                       "缺失明细：\n" + missing + "\n\n" +
                       "常见原因（按可能性排序）：\n" +
                       "1) 定制补丁文件（如 entry/pages/Index.ets）的 import 与当前导出布局不匹配：\n" +
                       "   空目录导出 = 单模块（源码全在 entry/），有残留导出 = 双模块（源码在 tuanjieLib/）。\n" +
                       "   补丁器会按布局自动改写 import；若仍看到此错误，说明出现了补丁未覆盖的新 import。\n" +
                       "2) 导出目录残留上一轮构建产物导致结构错乱：流水线导出前会自动清空并校验，\n" +
                       "   清理失败会直接中止导出；若跳过了清理，请删除整个导出目录后重新导出。\n\n" +
                       "报错原文（节选）：\n" + missing + "\n" + Tail(hvOut, 6);
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

        /// <summary>
        /// 从 hvigor 输出里提取 "Cannot find module" 报错行（去重）。
        /// 形如：Error Message: Cannot find module 'X' ... At File: path:line:col
        /// 此前只展示 Tail(10)（构建页脚），真正的缺失模块名被截掉，导致无从下手。
        /// </summary>
        private static List<string> ExtractMissingModuleLines(string hvOut)
        {
            if (string.IsNullOrEmpty(hvOut)) return new List<string>();
            return hvOut.Replace("\r\n", "\n").Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Contains("Cannot find module"))
                .Distinct()
                .Take(10)
                .ToList();
        }

        private static string Tail(string text, int lines)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var all = text.Replace("\r\n", "\n").Split('\n');
            var take = Math.Min(lines, all.Length);
            return string.Join("\n", all, all.Length - take, take);
        }

        /// <summary>
        /// 清空导出目录内容，保证 Unity 导出器看到的是一个干净目录。
        ///
        /// 为什么必须清整个导出目录（不能只清 entry/build）：
        /// Unity 导出器会检测目标目录里是否已存在 tuanjieLib 模块。
        ///   · 空目录     → 单模块结构，源码全部落在 entry/src/main/ets/
        ///   · 有残留     → 双模块结构，源码被挪进 tuanjieLib/，entry 变成薄壳
        /// 两种布局现在都能出包（补丁按布局自适应），但残留还会带来
        /// 旧构建产物 / 旧 oh_modules 污染，所以仍要清空以保证可复现。
        ///
        /// 清理失败绝不能继续导出：残留会悄悄改变导出布局，
        /// 表现为 ArkTS 编译期 "Cannot find module" 且极难定位。
        /// 这里做三重兜底：重试 → 整目录改名挪走 → 中止并报可操作错误。
        ///
        /// 删除方式：逐文件删除而不是 Directory.Delete(path, true)。
        /// Unity 导出的 .so 是硬链接（与 tuanjieLib/libs 共享 inode），
        /// Mono 的递归删目录会抛 "参数错误 : 'arm64-v8a'"；
        /// File.Delete 对硬链接是安全的（只减少链接计数，不动原文件）。
        /// </summary>
        private static bool CleanHarmonyExportDir(string dir, out string error)
        {
            error = null;
            var root = Path.GetFullPath($"{Application.dataPath}/../output");
            var full = Path.GetFullPath(dir);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[Harmony] 导出目录不在 output 下，跳过清理: " + full);
                return true;
            }
            if (!Directory.Exists(full)) return true;

            // 第一/二道：清空内容（文件被 DevEco / 杀毒软件短暂占用时，重试通常能成功）
            List<string> failures = null;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                failures = DeleteDirContents(full);
                if (failures.Count == 0)
                {
                    Debug.Log($"[Harmony] 已清空导出目录内容: {full}（第 {attempt} 次尝试）");
                    return true;
                }
                System.Threading.Thread.Sleep(300);
            }

            // 第三道：把整个残留目录改名挪走，再建一个全新的空目录。
            // Windows 下即使目录内文件被占用，目录改名通常也能成功。
            var aside = full.TrimEnd('\\', '/') + "_dirty_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            try
            {
                Directory.Move(full, aside);
                Directory.CreateDirectory(full);
                Debug.LogWarning(
                    "[Harmony] 导出目录有文件被占用，已把残留内容挪到:\n  " + aside + "\n" +
                    "请关闭占用进程（DevEco Studio / hvigor 守护进程 / 文件管理器）后手动删除该目录。");
                try { Directory.Delete(aside, true); } catch { /* 挪走即可，删不掉不影响本次导出 */ }
                return true;
            }
            catch (Exception e)
            {
                error = "导出目录清理失败，拒绝在脏目录上导出（残留会改变模块结构导致 Cannot find module）。\n" +
                        "被占用的文件（共 " + failures.Count + " 个，节选）：\n- " +
                        string.Join("\n- ", failures.Take(8)) + "\n" +
                        "改名兜底也失败：" + e.Message + "\n" +
                        "请关闭占用该目录的进程（DevEco Studio / hvigor / 杀毒软件）后重试。";
                return false;
            }
        }

        /// <summary>
        /// 逐文件删除目录内容，再删除空目录（规避硬链接导致的递归删失败）。
        /// 返回删除失败的路径清单（空表示目录已清干净）。
        /// </summary>
        private static List<string> DeleteDirContents(string dir)
        {
            var failures = new List<string>();

            foreach (var file in Directory.GetFiles(dir))
            {
                try
                {
                    // 只读/系统属性会让 File.Delete 抛 UnauthorizedAccessException
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch (Exception e) { failures.Add(file + " — " + e.Message); }
            }

            foreach (var sub in Directory.GetDirectories(dir))
            {
                failures.AddRange(DeleteDirContents(sub));
                try { Directory.Delete(sub, false); }
                catch (Exception e)
                {
                    // 子项还有没删掉的文件时会走到这里；残留文件已在 failures 里
                    if (Directory.Exists(sub) && Directory.GetFileSystemEntries(sub).Length > 0)
                        failures.Add(sub + " — " + e.Message);
                }
            }
            return failures;
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
            var templateEts = Path.Combine(templateDir, "entry", "src", "main", "ets");
            if (!Directory.Exists(Path.Combine(templateEts, "ability")))
                return "回归自检失败：找不到 Unity ArkTS 模板目录 " + templateEts;

            foreach (var f in new[] { "TuanjiePlayerAbilityBase.ets", "TuanjiePlayerAbility.ets" })
            {
                if (!File.Exists(Path.Combine(templateEts, "ability", f)))
                    return "回归自检失败：模板缺少 ability/" + f;
            }

            var workDir = Path.GetFullPath($"{Application.dataPath}/../output/_patchtest");
            if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
            var workEts = Path.Combine(workDir, "entry", "src", "main", "ets");

            // 夹具必须与真实单模块导出同构：整个 entry/src/main 模板树
            // （ets/ 全量 + module.json5 + resources/）。
            // 早期只拷 ability 两个文件，entry/common、pages/components、log 全缺，
            // 补丁的 import 解析校验与完整性校验会误报，
            // 也测不出「定制 Index.ets 的 import 与布局不匹配」这类问题。
            // 只拷 ets/ 也不够：权限注入 ApplyPermissionsAndStrings 要写 module.json5
            // 与 resources/base/element/string.json，缺了直接报 "找不到 module.json5"
            // （string.json 反而只是 SKIP），回归自检会挂在权限这步。
            void CopyDir(string srcDir, string dstDir)
            {
                Directory.CreateDirectory(dstDir);
                foreach (var file in Directory.GetFiles(srcDir))
                    File.Copy(file, Path.Combine(dstDir, Path.GetFileName(file)), true);
                foreach (var sub in Directory.GetDirectories(srcDir))
                    CopyDir(sub, Path.Combine(dstDir, Path.GetFileName(sub)));
            }
            CopyDir(Path.Combine(templateDir, "entry", "src", "main"),
                Path.Combine(workDir, "entry", "src", "main"));

            // gen/ 是 Unity 导出时按工程生成的，模板树里没有；但 workers/ 对它有 import：
            //   HostProxy   → ../gen/TuanjieJSScriptRegister（具名 registerJSScriptToCSharp）
            //                 ../gen/BuiltinWorkerMsgRegistration（副作用 import）
            //   WorkerProxy → ../gen/BuiltinHostMsgRegistration（副作用 import）
            // 真实导出里 gen/ 由 Unity 生成，夹具补最小 stub 让 import 可解析即可
            // （补丁与 import 校验不执行运行时逻辑）。
            var workGen = Path.Combine(workEts, "gen");
            Directory.CreateDirectory(workGen);
            var genStubs = new Dictionary<string, string>
            {
                ["TuanjieJSScriptRegister.ets"] =
                    "// fixture stub —— 真实文件由 Unity 导出生成\n" +
                    "export function registerJSScriptToCSharp() {\n}\n",
                ["BuiltinHostMsgRegistration.ets"] =
                    "// fixture stub —— 真实文件由 Unity 导出生成\n",
                ["BuiltinWorkerMsgRegistration.ets"] =
                    "// fixture stub —— 真实文件由 Unity 导出生成\n"
            };
            foreach (var kv in genStubs)
            {
                var p = Path.Combine(workGen, kv.Key);
                if (!File.Exists(p)) File.WriteAllText(p, kv.Value);
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
            var baseText = File.ReadAllText(Path.Combine(workEts, "ability", "TuanjiePlayerAbilityBase.ets"));
            var entryText = File.ReadAllText(Path.Combine(workEts, "ability", "TuanjiePlayerAbility.ets"));
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

                // 0.2) 覆盖 entry 的 Index.ets 为团队定制版
                //      （OAID 页面：SDKManager 通知回调、退出确认弹窗、权限状态）
                if (!ApplyEntryIndexPage(root, log, out error)) return false;

                // 0.3) 注入权限声明与权限说明字符串
                if (!ApplyPermissionsAndStrings(root, log, out error)) return false;

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

                // 3.1) exported.ets 追加 SDKManager 导出（entry/pages/Index.ets 要用）
                if (!AppendSdkManagerExport(root, log, out error)) return false;

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

                // 定制版 Index.ets：SDKManager 回调 + 退出确认弹窗
                // （import 形态随布局不同：双模块 from 'tuanjieLib'，单模块 from 'OHKitSdk'）
                var indexText = File.Exists(Path.Combine(entryEtsDir, "pages", "Index.ets"))
                    ? File.ReadAllText(Path.Combine(entryEtsDir, "pages", "Index.ets"))
                    : "";
                Require(indexText.Contains("SDKManager, TuanjiePlayerView") ||
                        indexText.Contains("from 'OHKitSdk'"),
                    "entry/pages/Index.ets 不是定制版（缺 SDKManager import）");
                Require(indexText.Contains("SDKManager.OnNotificationSettingsClosed()"),
                    "entry/pages/Index.ets 缺 onFocus 回调");
                Require(indexText.Contains("你确定要退出游戏吗"), "entry/pages/Index.ets 缺退出确认弹窗");

                // exported.ets 必须导出 SDKManager，否则 Index.ets 从 'tuanjieLib' 导入不到
                var exportedPath = Path.Combine(root, "tuanjieLib", "exported.ets");
                Require(!File.Exists(exportedPath) ||
                        File.ReadAllText(exportedPath).Contains("export { SDKManager }"),
                    "tuanjieLib/exported.ets 未导出 SDKManager");

                // OHKitSdk sdk 目录的校验放在 ohpm install 之后单独做
                // （见 VerifyOHKitSdkSources），此处 oh_modules 可能还没解包。

                // 权限与说明字符串
                var moduleText = File.Exists(Path.Combine(root, "entry", "src", "main", "module.json5"))
                    ? File.ReadAllText(Path.Combine(root, "entry", "src", "main", "module.json5"))
                    : "";
                Require(moduleText.Contains("ohos.permission.APP_TRACKING_CONSENT"),
                    "module.json5 缺 APP_TRACKING_CONSENT 权限");
                Require(moduleText.Contains("ohos.permission.PUBLISH_AGENT_REMINDER"),
                    "module.json5 缺 PUBLISH_AGENT_REMINDER 权限");
                Require(moduleText.Contains("ohos.permission.VIBRATE"), "module.json5 缺 VIBRATE 权限");

                var stringText = File.Exists(Path.Combine(root, "entry", "src", "main", "resources",
                    "base", "element", "string.json"))
                    ? File.ReadAllText(Path.Combine(root, "entry", "src", "main", "resources",
                        "base", "element", "string.json"))
                    : "";
                Require(stringText.Contains("app_tracking_reason"), "string.json 缺 app_tracking_reason");
                Require(stringText.Contains("reason_publish_reminder"), "string.json 缺 reason_publish_reminder");

                if (missing.Count > 0)
                {
                    error = "补丁未完整落地：\n- " + string.Join("\n- ", missing);
                    return false;
                }

                // 兜底：entry 源码里所有相对 import（以及 'tuanjieLib' 裸模块引用）
                // 必须在当前布局下可解析。历史上多次出现「补丁文件 import 与布局不匹配 →
                // hvigor 报 Cannot find module 但日志被 Tail 截断看不到模块名」，
                // 这里在补丁阶段就拦下，并给出精确的 文件:import 清单。
                if (!VerifyEntryImportsResolve(root, log, out error)) return false;

                log.Add("完整性校验: 18/18 项通过（OHKitSdk 源码另在 ohpm 之后校验）");
                return true;
            }

            /// <summary>
            /// 校验 entry/src/main/ets 下所有 .ets/.ts 的 import 在当前布局可解析：
            ///   · 相对路径（./、../）→ 必须能落到实际文件（支持 .ts/.ets/Index.ets 省略后缀）
            ///   · 'tuanjieLib'      → 必须存在 tuanjieLib 模块（双模块布局）
            ///   · 其他裸模块（@kit.*、@ohos.*、OHKitSdk、@thinkingdata/* 等）
            ///     → ohpm/SDK 依赖，由 ohpm install 保证，这里不拦
            /// 发现解析不了的 import 直接失败，列出 文件:import 明细。
            /// </summary>
            private static bool VerifyEntryImportsResolve(string root, List<string> log, out string error)
            {
                error = null;
                var entryEts = Path.Combine(root, "entry", "src", "main", "ets");
                if (!Directory.Exists(entryEts)) return true;

                var hasTuanjieLib = Directory.Exists(Path.Combine(root, "tuanjieLib"));
                var unresolved = new List<string>();
                var importRegex = new Regex(
                    @"(?:from|import)\s+['""]([^'""]+)['""]",
                    RegexOptions.Compiled);

                foreach (var file in Directory.GetFiles(entryEts, "*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext != ".ets" && ext != ".ts") continue;

                    var text = File.ReadAllText(file);
                    foreach (Match m in importRegex.Matches(text))
                    {
                        var spec = m.Groups[1].Value;
                        string where = file.Substring(entryEts.Length).TrimStart('\\', '/');

                        if (spec.StartsWith("./") || spec.StartsWith("../"))
                        {
                            if (!RelativeImportResolves(file, spec))
                                unresolved.Add($"{where}: import '{spec}'（相对路径在 entry 内解析不到）");
                        }
                        else if (spec == "tuanjieLib")
                        {
                            if (!hasTuanjieLib)
                                unresolved.Add($"{where}: import 'tuanjieLib'（当前是单模块布局，没有 tuanjieLib 模块；" +
                                               "应改写为 'OHKitSdk' / './components/...' 或改用双模块导出）");
                        }
                        // 其余裸模块（@kit.* / @ohos.* / ohpm 依赖）不在源码层校验
                    }
                }

                if (unresolved.Count > 0)
                {
                    error = "entry 源码存在无法解析的 import（多半是补丁文件与导出布局不匹配）：\n- " +
                            string.Join("\n- ", unresolved) +
                            "\n请检查 HarmonyPatch 下补丁源的 import，或改用与布局匹配的导出目录。";
                    return false;
                }

                log.Add("import 解析校验: entry 全部相对 import 可解析" +
                        (hasTuanjieLib ? "（双模块布局）" : "（单模块布局）"));
                return true;
            }

            /// <summary>相对 import 能否落到实际文件（支持省略 .ts/.ets 后缀与目录 Index.ets）。</summary>
            private static bool RelativeImportResolves(string fromFile, string spec)
            {
                var baseDir = Path.GetDirectoryName(fromFile) ?? "";
                var target = Path.GetFullPath(Path.Combine(baseDir, spec));
                var candidates = new List<string> { target };
                if (!Path.HasExtension(target))
                {
                    candidates.Add(target + ".ts");
                    candidates.Add(target + ".ets");
                    candidates.Add(Path.Combine(target, "Index.ets"));
                    candidates.Add(Path.Combine(target, "index.ets"));
                }
                return candidates.Any(File.Exists);
            }

            /// <summary>
            /// 补齐 entry 侧的公共源码（两种布局都要跑）。
            ///
            /// 背景：entry/ability/TuanjiePlayerAbility.ets 与定制 Index.ets 里有
            ///     import { SetToGlobalThis } from '../common/GlobalThisUtil';
            ///     import Logger from '../log/Logger';
            /// 这类相对 entry/ 解析的 import，目标文件必须实际存在于 entry/ 下：
            ///   · 双模块布局：业务公共源码在 tuanjieLib/src/main/ets/，entry 拿不到 → 从 tuanjieLib 同步
            ///   · 单模块布局：common/ 等由 Unity 直接导出到 entry，但 log/Logger.ts 是
            ///     团队自定义文件（Unity 导出里没有）→ 也要补
            /// 早期版本在单模块布局直接跳过，导致 log/Logger.ts 缺失、
            /// ArkTS 编译报 "Cannot find module '../log/Logger'"。
            ///
            /// 处理：缺什么补什么（tuanjieLib → harmony/ 模板 → HarmonyPatch 补丁源，逐个兜底），幂等。
            /// common/PlayerPrefs.ets 例外：只在单模块布局同步（双模块下它的
            /// '../workers/MessageProcessor' 在 entry 内无法解析，见下方注释）。
            /// </summary>
            private static bool SyncEntryCommonFiles(string root, Layout layout,
                List<string> log, out string error)
            {
                error = null;
                var entryEts = Path.Combine(root, "entry", "src", "main", "ets");
                var libEts = Path.Combine(root, "tuanjieLib", "src", "main", "ets");
                // Exported 非空 = 双模块布局（B-dual-module），空 = 单模块（A-single-module）
                var dualModule = !string.IsNullOrEmpty(layout.Exported);

                // entry 侧实际引用到的公共文件（相对 entry/src/main/ets）
                var needed = new List<string>
                {
                    Path.Combine("common", "Constants.ets"),
                    Path.Combine("common", "GlobalThisUtil.ts"),
                    Path.Combine("common", "TuanjieLog.ets"),
                    Path.Combine("log", "Logger.ts")
                };

                // common/PlayerPrefs.ets 只在单模块布局下同步：
                //   · 文件末尾有 REGISTER_BUILTIN_MODULE(MSG_RECEIVER.HOST_UI, PlayerPrefs)
                //     自注册，且依赖 '../workers/MessageProcessor' —— workers/、gen/ 这套
                //     只在「单模块的 entry/」或「双模块的 tuanjieLib/」里成体系存在。
                //   · 双模块下 entry 没有 workers/，同步进 entry/common/ 只会得到一个相对
                //     import 解析不到的死文件（entry 侧没有任何文件 import 它，注册走
                //     tuanjieLib/gen/BuiltinHostMsgRegistration.ets），必被 import 解析校验拦下：
                //       common/PlayerPrefs.ets: import '../workers/MessageProcessor'
                //   · 单模块下 Unity 把整棵源码树（含 workers/、gen/）导出到 entry，
                //     PlayerPrefs.ets 是 gen/BuiltinHostMsgRegistration.ets 的 import 目标，
                //     必须存在，且 '../workers/MessageProcessor' 在 entry 内可解析。
                if (!dualModule)
                    needed.Add(Path.Combine("common", "PlayerPrefs.ets"));

                // 自愈：双模块下 entry/common/PlayerPrefs.ets 若已存在（旧版补丁同步留下的
                // 死文件，import 无法解析），删掉。单模块下绝不能动（那是活文件）。
                var stalePlayerPrefs = Path.Combine(entryEts, "common", "PlayerPrefs.ets");
                if (dualModule && File.Exists(stalePlayerPrefs))
                {
                    File.Delete(stalePlayerPrefs);
                    log.Add("REMOVED entry/common/PlayerPrefs.ets" +
                            "（双模块死文件，../workers/MessageProcessor 在 entry 内无法解析）");
                }

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
                        // tuanjieLib 里没有 —— 依次从团队工程模板、版本化补丁源取。
                        // harmony/ 不在 git 里（见 .gitignore），所以补丁源兜底是必须的，
                        // 否则换台机器就会丢 log/Logger.ts 再次触发 "Cannot find module '../log/Logger'"。
                        var fallbacks = new[]
                        {
                            Path.Combine(Application.dataPath, "..", "harmony",
                                "entry", "src", "main", "ets", rel),
                            Path.Combine(PatchSourceRoot, rel)
                        };
                        var found = fallbacks.FirstOrDefault(File.Exists);
                        if (found != null)
                        {
                            src = Path.GetFullPath(found);
                        }
                        else
                        {
                            log.Add("MISS   " + rel + "（tuanjieLib / harmony/ 模板 / HarmonyPatch 补丁源中都没有，跳过）");
                            continue;
                        }
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    File.Copy(src, dst, true);
                    copied++;
                    var srcName = src.StartsWith(libEts) ? "tuanjieLib"
                        : src.StartsWith(Path.GetFullPath(PatchSourceRoot)) ? "HarmonyPatch 补丁源"
                        : "harmony/模板";
                    log.Add("SYNC   entry/" + rel + "  <-  " + srcName);
                }

                log.Add($"补齐 entry 公共文件: 新增 {copied} 个，已存在 {skipped} 个");
                return true;
            }

            /// <summary>补丁源根目录（Assets 下，随代码版本管理）。</summary>
            private static string PatchSourceRoot => Path.Combine(Application.dataPath,
                "Middleware", "Editor", "AppBuilder", "HarmonyPatch");

            /// <summary>
            /// 用团队定制版覆盖 entry/src/main/ets/pages/Index.ets（OAID 页面），并按导出布局改写 import。
            ///
            /// Unity 模板自带的 Index.ets 只有一个 TuanjiePlayerView，没有：
            ///   · SDKManager 的 onFocus/onBlur 回调（通知设置关闭后要回调 SDK）
            ///   · 返回键退出确认弹窗（原来只 return !AppStorage.get(...)，直接退出）
            ///   · OAID / 权限状态等页面级状态
            /// 这些是团队在 harmony/ 工程里手改的，每次导出都会被覆盖回模板，
            /// 所以这里每次导出后重新覆盖一次。
            ///
            /// 定制版源码写的是双模块世界的 import：
            ///     import { SDKManager, TuanjiePlayerView } from 'tuanjieLib';
            /// 它依赖 tuanjieLib 模块的 exported.ets 转发。但空目录导出是单模块布局
            /// （没有 tuanjieLib 模块），照抄会报 "Cannot find module 'tuanjieLib'"。
            /// 单模块布局下改写为直接引用：
            ///     SDKManager        ← 'OHKitSdk'（entry 自身的 ohpm 依赖，har 的 Index.ets 有导出）
            ///     TuanjiePlayerView ← './components/TuanjiePlayerView'（同在 entry/pages/ 下）
            /// 双模块布局保持原样（'tuanjieLib' 通过 entry/oh_modules/tuanjieLib 解析）。
            /// </summary>
            private static bool ApplyEntryIndexPage(string root, List<string> log, out string error)
            {
                error = null;
                var src = Path.Combine(PatchSourceRoot, "EntryPages", "Index.ets");
                if (!File.Exists(src))
                {
                    error = "找不到 Index.ets 补丁源: " + src;
                    return false;
                }

                var dst = Path.Combine(root, "entry", "src", "main", "ets", "pages", "Index.ets");
                Directory.CreateDirectory(Path.GetDirectoryName(dst));

                var dualModule = Directory.Exists(Path.Combine(root, "tuanjieLib"));
                var content = File.ReadAllText(src);
                if (!dualModule)
                {
                    const string dualImport = "import { SDKManager, TuanjiePlayerView } from 'tuanjieLib';";
                    const string singleImport =
                        "import { SDKManager } from 'OHKitSdk';\n" +
                        "import { TuanjiePlayerView } from './components/TuanjiePlayerView';";
                    if (content.Contains(dualImport))
                    {
                        content = content.Replace(dualImport, singleImport);
                        log.Add("REWRITE Index.ets import（单模块布局：tuanjieLib → OHKitSdk + ./components/TuanjiePlayerView）");
                    }
                }

                var before = File.Exists(dst) ? File.ReadAllText(dst) : null;
                if (before == content)
                {
                    log.Add("ALREADY entry/pages/Index.ets（已是定制版）");
                    return true;
                }

                File.WriteAllText(dst, content, new UTF8Encoding(false));
                log.Add((before == null ? "ADDED  " : "PATCHED ") + "entry/pages/Index.ets（OAID/SDKManager/退出确认）");
                return true;
            }

            /// <summary>
            /// 覆盖 OHKitSdk 的 sdk 目录源码。
            ///
            /// OHKitSdk 是 ohpm 从 har 解包到 oh_modules/.ohpm/OHKitSdk@&lt;hash&gt;=/ 的依赖。
            /// har 原版只有 4 个文件（AdPlayer / AdStatusHandler / SDKHandler / SDKManager），
            /// 团队在工程里改了这 4 个并新增了 SendOAID.ets、TuanjieNative.ets
            /// （OAID 上报与 native 桥接），这些改动不在 har 里，每次 ohpm install 都会丢失。
            ///
            /// 注意：.ohpm 下的目录名带哈希后缀、且随 har 内容变化，所以不能写死路径，
            /// 这里按前缀 OHKitSdk@ 动态查找。
            /// </summary>
            /// <summary>
            /// 供流水线在 ohpm install 之后调用：覆盖 OHKitSdk 源码并输出日志。
            /// 必须晚于 ohpm install —— 否则会被 har 重新解包覆盖。
            /// </summary>
            public static bool ApplyOHKitSdkSourcesAfterInstall(string root, out List<string> log, out string error)
            {
                var localLog = new List<string>();
                var ok = ApplyOHKitSdkSources(root, localLog, out error);
                log = localLog;
                if (ok) ok = VerifyOHKitSdkSources(root, localLog, out error);
                log = localLog;
                return ok;
            }

            /// <summary>校验 OHKitSdk sdk 目录里的补丁文件确实落地（ohpm install 之后调用）。</summary>
            private static bool VerifyOHKitSdkSources(string root, List<string> log, out string error)
            {
                error = null;
                var ohpmDir = Path.Combine(root, "oh_modules", ".ohpm");
                if (!Directory.Exists(ohpmDir)) return true;

                var sdkDirs = Directory.GetDirectories(ohpmDir, "OHKitSdk@*")
                    .Select(d => Path.Combine(d, "oh_modules", "OHKitSdk", "src", "main", "ets", "components", "sdk"))
                    .Where(Directory.Exists)
                    .ToArray();
                if (sdkDirs.Length == 0) return true;

                // 团队版 SDKManager 含 OnNotificationSettingsClosed（har 原版没有）
                foreach (var d in sdkDirs)
                {
                    var mgr = Path.Combine(d, "SDKManager.ets");
                    if (File.Exists(mgr) && !File.ReadAllText(mgr).Contains("OnNotificationSettingsClosed"))
                    {
                        error = "OHKitSdk/sdk/SDKManager.ets 仍是 har 原版（缺 OnNotificationSettingsClosed）。\n" +
                                "说明补丁被 ohpm install 覆盖了 —— 检查流水线里覆盖动作是否在 ohpm 之后。";
                        return false;
                    }
                    if (!File.Exists(Path.Combine(d, "SendOAID.ets")))
                    {
                        error = "OHKitSdk/sdk/SendOAID.ets 缺失（补丁未落地）";
                        return false;
                    }
                }

                log.Add("OHKitSdk sdk 目录校验: 通过");
                return true;
            }

            private static bool ApplyOHKitSdkSources(string root, List<string> log, out string error)
            {
                error = null;
                var srcDir = Path.Combine(PatchSourceRoot, "OHKitSdk", "sdk");
                if (!Directory.Exists(srcDir))
                {
                    error = "找不到 OHKitSdk sdk 补丁源: " + srcDir;
                    return false;
                }

                // 定位解包目录：oh_modules/.ohpm/OHKitSdk@<hash>=/oh_modules/OHKitSdk/src/main/ets/components/sdk
                var ohpmDir = Path.Combine(root, "oh_modules", ".ohpm");
                if (!Directory.Exists(ohpmDir))
                {
                    log.Add("SKIP   OHKitSdk 补丁（oh_modules/.ohpm 不存在，可能 ohpm install 未执行）");
                    return true;
                }

                var candidates = Directory.GetDirectories(ohpmDir, "OHKitSdk@*");
                if (candidates.Length == 0)
                {
                    log.Add("SKIP   OHKitSdk 补丁（.ohpm 下没有 OHKitSdk@* 目录）");
                    return true;
                }

                var copied = 0;
                var same = 0;
                foreach (var cand in candidates)
                {
                    var dstDir = Path.Combine(cand, "oh_modules", "OHKitSdk", "src", "main", "ets", "components", "sdk");
                    if (!Directory.Exists(dstDir)) continue;

                    foreach (var file in Directory.GetFiles(srcDir, "*.ets"))
                    {
                        var name = Path.GetFileName(file);
                        var dst = Path.Combine(dstDir, name);
                        var content = File.ReadAllText(file);

                        if (File.Exists(dst) && File.ReadAllText(dst) == content)
                        {
                            same++;
                            continue;
                        }

                        File.WriteAllText(dst, content, new UTF8Encoding(false));
                        copied++;
                        log.Add("SYNC   OHKitSdk/sdk/" + name);
                    }
                }

                log.Add($"OHKitSdk sdk 目录: 写入 {copied} 个，已是最新 {same} 个");
                return true;
            }

            /// <summary>
            /// 注入权限声明（module.json5）与权限说明字符串（string.json）。
            ///
            /// Unity 导出只给 INTERNET 与 GET_NETWORK_INFO 两个权限，团队还需要：
            ///   · PUBLISH_AGENT_REMINDER  —— 每日字谜提醒（Agent 提醒）
            ///   · APP_TRACKING_CONSENT    —— 个性化广告（OAID 相关）
            ///   · VIBRATE                 —— 震动反馈
            /// 每个需要 reason 的权限都要在 string.json 里有对应说明，否则上架审核不过。
            /// </summary>
            private static bool ApplyPermissionsAndStrings(string root, List<string> log, out string error)
            {
                error = null;

                // ---------- 1. 权限说明字符串 ----------
                var stringPath = Path.Combine(root, "entry", "src", "main", "resources", "base", "element", "string.json");
                if (File.Exists(stringPath))
                {
                    var text = File.ReadAllText(stringPath);
                    var added = 0;

                    var reasons = new[]
                    {
                        ("reason_publish_reminder", "用于按时为您推送每日字谜提醒和活动消息"),
                        ("app_tracking_reason", "用于提供个性化广告内容，优化您的使用体验")
                    };

                    foreach (var (name, value) in reasons)
                    {
                        if (text.Contains($"\"{name}\"")) continue;

                        // 在 string 数组末尾追加一项
                        var idx = text.LastIndexOf(']');
                        if (idx < 0) { error = "string.json 结构异常（找不到数组结尾）"; return false; }

                        var entry = $"    {{\n      \"name\": \"{name}\",\n      \"value\": \"{value}\"\n    }}";
                        var before = text.Substring(0, idx).TrimEnd();
                        var needComma = before.EndsWith("}") || before.EndsWith("\"");
                        text = before + (needComma ? ",\n" : "\n") + entry + "\n  " + text.Substring(idx);
                        added++;
                    }

                    if (added > 0)
                    {
                        File.WriteAllText(stringPath, text, new UTF8Encoding(false));
                        log.Add($"SYNC   resources/base/element/string.json（新增 {added} 条权限说明）");
                    }
                    else
                    {
                        log.Add("ALREADY resources/base/element/string.json（权限说明已存在）");
                    }
                }
                else
                {
                    log.Add("SKIP   权限说明字符串（string.json 不存在）");
                }

                // ---------- 2. 权限声明 ----------
                var modulePath = Path.Combine(root, "entry", "src", "main", "module.json5");
                if (!File.Exists(modulePath))
                {
                    error = "找不到 module.json5: " + modulePath;
                    return false;
                }

                var moduleText = File.ReadAllText(modulePath);
                var permAdded = 0;

                // 需要补齐的权限：name -> reason 资源名（null 表示不需要 reason）
                var perms = new (string name, string reason)[]
                {
                    ("ohos.permission.PUBLISH_AGENT_REMINDER", "reason_publish_reminder"),
                    ("ohos.permission.APP_TRACKING_CONSENT", "app_tracking_reason"),
                    ("ohos.permission.VIBRATE", null)
                };

                foreach (var (name, reason) in perms)
                {
                    if (moduleText.Contains($"\"{name}\"")) continue;

                    var item = reason == null
                        ? $"      {{\n        \"name\": \"{name}\"\n      }}"
                        : $"      {{\n        \"name\": \"{name}\",\n" +
                          $"        \"usedScene\": {{\n" +
                          $"          \"abilities\": [\n            \"TuanjiePlayerAbility\"\n          ],\n" +
                          $"          \"when\": \"always\"\n        }},\n" +
                          $"        \"reason\": \"$string:{reason}\"\n      }}";

                    var idx = moduleText.LastIndexOf(']');
                    if (idx < 0) { error = "module.json5 结构异常（找不到数组结尾）"; return false; }

                    var before = moduleText.Substring(0, idx).TrimEnd();
                    var needComma = before.EndsWith("}");
                    moduleText = before + (needComma ? ",\n" : "\n") + item + "\n    " + moduleText.Substring(idx);
                    permAdded++;
                }

                if (permAdded > 0)
                {
                    File.WriteAllText(modulePath, moduleText, new UTF8Encoding(false));
                    log.Add($"SYNC   entry/src/main/module.json5（新增 {permAdded} 个权限）");
                }
                else
                {
                    log.Add("ALREADY entry/src/main/module.json5（权限已齐备）");
                }

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

            /// <summary>
            /// 在 tuanjieLib/exported.ets 末尾追加 SDKManager 导出。
            ///
            /// entry/pages/Index.ets 里写的是
            ///     import { SDKManager, TuanjiePlayerView } from 'tuanjieLib';
            /// 而 tuanjieLib 的对外出口就是 exported.ets，模板里没有 SDKManager，
            /// 不追加就会报 "Cannot find module 'tuanjieLib'" 或找不到 SDKManager。
            /// </summary>
            private static bool AppendSdkManagerExport(string root, List<string> log, out string error)
            {
                error = null;
                var path = Path.Combine(root, "tuanjieLib", "exported.ets");
                if (!File.Exists(path))
                {
                    log.Add("SKIP   exported.ets 追加 SDKManager（文件不存在，单模块布局）");
                    return true;
                }

                const string marker = "export { SDKManager }";
                var text = File.ReadAllText(path);
                if (text.Contains(marker))
                {
                    log.Add("ALREADY tuanjieLib/exported.ets（SDKManager 已导出）");
                    return true;
                }

                var line = "export { SDKManager } from './oh_modules/OHKitSdk/src/main/ets/components/sdk/SDKManager'";
                var nl = text.Contains("\r\n") ? "\r\n" : "\n";
                text = text.TrimEnd() + nl + nl + line + nl;
                File.WriteAllText(path, text, new UTF8Encoding(false));
                log.Add("PATCHED tuanjieLib/exported.ets（追加 SDKManager 导出）");
                return true;
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
