using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Middleware
{
	public class AppBuilder
	{
		public const string DefineRelease = "Unity_Release";
		public const string DefineShowLog = "Unity_ShowLog";
		public const string DefineResourceAb = "Unity_ResourceAb";

		private enum BuildType
		{
			JenkinsBuild,
			EditorBuild
		}

		private class BuildParam
		{
			public BuildType BuildType;
			public string BuildVersion;
			public bool IsBuildRelease;
			public bool IsBuildShowLog;

			public override string ToString()
			{
				return
					$"{nameof(BuildVersion)}: {BuildVersion}, {nameof(IsBuildRelease)}: {IsBuildRelease}, {nameof(IsBuildShowLog)}: {IsBuildShowLog}";
			}
		}

		#region Jenkins自动化打包

		private static BuildParam ParseJenkinsBuildSetting(string[] commandLineArgs)
		{
			BuildParam value = new BuildParam();
			for (var i = 0; i < commandLineArgs.Length; i++)
			{
				var arg = commandLineArgs[i];
				Debug.LogError("获取命令输入: " + arg);
				if (arg.StartsWith("-params"))
				{
					var param = arg.Split('-');
					foreach (var str in param)
					{
						Debug.LogError("获取命令输入参数: " + str);
					}

					value.BuildVersion = param[2];
					value.IsBuildRelease = param[3].ToUpper() == "RELEASE";
					value.IsBuildShowLog = param[4].ToUpper() == "TRUE";
				}
			}

			return value; // 返回参数
		}
		#if UNITY_OPENHARMONY
		public static void JenkinsBuildHarmony()
		{
			var buildParam = ParseJenkinsBuildSetting(Environment.GetCommandLineArgs());
			buildParam.BuildType = BuildType.JenkinsBuild;
			BuildHarmony(buildParam);
		}
		#endif
		
		public static void JenkinsBuildAndroid()
		{
			var buildParam = ParseJenkinsBuildSetting(Environment.GetCommandLineArgs());
			buildParam.BuildType = BuildType.JenkinsBuild;
			BuildAndroid(buildParam);
		}

		public static void JenkinsBuildIOS()
		{
			var buildParam = ParseJenkinsBuildSetting(Environment.GetCommandLineArgs());
			buildParam.BuildType = BuildType.JenkinsBuild;
			BuildIOS(buildParam);
		}
		#endregion

		#region 编辑器一键打包
		#if UNITY_2022_3_55 || UNITY_2022_3_61
		[MenuItem("Tools/自动化打包/Harmony/Debug", false, 110)]
		public static void BuildHarmonyDebug()
		{
			BuildHarmony(new BuildParam()
			{
				BuildVersion = "1.5.0",
				BuildType = BuildType.EditorBuild,
				IsBuildRelease = false,
				IsBuildShowLog = true
			});
		}

		[MenuItem("Tools/自动化打包/Harmony/Release", false, 111)]
		public static void BuildHarmonyRelease()
		{
			BuildHarmony(new BuildParam()
			{
				BuildVersion = "1.5.0",
				BuildType = BuildType.EditorBuild,
				IsBuildRelease = true,
				IsBuildShowLog = false
			});
		}
		#endif
		
		[MenuItem("Tools/自动化打包/Android/Debug", false, 112)]
		public static void BuildAndroidDebug()
		{
			BuildAndroid(new BuildParam()
			{
				BuildVersion = "1.5.0",
				BuildType = BuildType.EditorBuild,
				IsBuildRelease = false,
				IsBuildShowLog = true
			});
		}

		[MenuItem("Tools/自动化打包/Android/Release", false, 113)]
		public static void BuildAndroidRelease()
		{
			BuildAndroid(new BuildParam()
			{
				BuildVersion = "1.5.0",
				BuildType = BuildType.EditorBuild,
				IsBuildRelease = true,
				IsBuildShowLog = false
			});
		}

		[MenuItem("Tools/自动化打包/IOS/Debug", false, 114)]
		public static void BuildIOSDebug()
		{
			BuildIOS(new BuildParam()
			{
				BuildVersion = "1.0.0",
				BuildType = BuildType.EditorBuild,
				IsBuildRelease = false,
				IsBuildShowLog = true
			});
		}

		[MenuItem("Tools/自动化打包/IOS/Release", false, 115)]
		public static void BuildIOSRelease()
		{
			BuildIOS(new BuildParam()
			{
				BuildVersion = "1.0.0",
				BuildType = BuildType.EditorBuild,
				IsBuildRelease = true,
				IsBuildShowLog = false
			});
		}
		
		#endregion

		#region 切换平台
		#if UNITY_2022_3_55 || UNITY_2022_3_61
		[MenuItem("Tools/切换平台/Harmony", false, 101)]
		public static void SwitchToHarmony()
		{
			SwitchPlatform(BuildTarget.OpenHarmony);
			if (AssetDatabase.IsValidFolder("Assets/GeneratedLocalRepo"))
				AssetDatabase.DeleteAsset("Assets/GeneratedLocalRepo");
		}
		#endif
		
		[MenuItem("Tools/切换平台/Android", false, 102)]
		public static void SwitchToAndroid()
		{
			SwitchPlatform(BuildTarget.Android);
		}

		[MenuItem("Tools/切换平台/IOS", false, 103)]
		public static void SwitchToApple()
		{
			SwitchPlatform(BuildTarget.iOS);
		}
		
		private static void SwitchPlatform(BuildTarget targetPlatform)
		{
			if (EditorUserBuildSettings.activeBuildTarget == targetPlatform)
				return;
			ManagerPackage(targetPlatform);
			EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(targetPlatform),
				targetPlatform);
			Debug.Log("切换平台成功");
			AssetDatabase.Refresh();
		}

		private static void ManagerPackage(BuildTarget target)
		{
			var googles = new List<string>()
			{
				//"\"com.liufangzhiwu.google-ext\": \"git@github.com:liufangzhiwu/GoogleExt.git\",",
				"\"com.liufangzhiwu.google-ext\": \"file:local_packages/com.liufangzhiwu.google-ext\",",
				"\"com.unity.purchasing\": \"4.13.0\","
			};
			var hormanys = new List<string>()
			{
				"\"cn.tuanjie.openharmony.sdkkit\": \"1.0.3\","
			};
			var values = (int)target == 48 ? hormanys : googles;
			ModifyManifestFile(values);
		}

		private static void ModifyManifestFile(List<string> values)
		{
			var path = Path.Combine(Application.dataPath, "../Packages/manifest.json");
			var lines = File.ReadAllLines(path).ToList();
			var tagIndex = 0;
			for (var i = 0; i < lines.Count; i++)
			{
				if (lines[i].Contains("thinkingdata"))
				{
					tagIndex = i;
					break;
				}
			}

			for (var i = tagIndex - 1; i > 1; i--)
			{
				Debug.Log("remove:" + lines[i]);
				lines.RemoveAt(i);
			}

			foreach (var val in values)
			{
				Debug.Log("add:" + val);
				lines.Insert(2, "    " + val);
			}

			File.WriteAllLines(path, lines);
		}

		#endregion
		
		#if UNITY_2022_3_55 || UNITY_2022_3_61
		private static void BuildHarmony(BuildParam buildParam)
		{
			if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.OpenHarmony)
			{
				Debug.LogError("先切换平台");
				return;
			}

			//宏设置
			SetDefineSymbols(BuildTargetGroup.OpenHarmony, buildParam);
			//打包设置
			EditorUserBuildSettings.exportAsOpenHarmonyProject = true;
			EditorUserBuildSettings.development = false;
			PlayerSettings.stripEngineCode = true;
			PlayerSettings.bundleVersion = buildParam.BuildVersion;
			PlayerSettings.OpenHarmony.bundleVersionCode = GenBuildNumber();
			PlayerSettings.OpenHarmony.compatibleSdkVersion = 14;
			PlayerSettings.OpenHarmony.targetArchitectures = OpenHarmonyArchitecture.ARM64;
			//账户设置
			SetDefaultIcon(1);
			PlayerSettings.companyName = "HexaSpaceGames";
			PlayerSettings.productName = "成语消消闯关";
			PlayerSettings.applicationIdentifier = "chengyu.idiom.block.zen.huawei";
			PlayerSettings.OpenHarmony.useCustomKeystore = true;
			PlayerSettings.OpenHarmony.keystoreName =
				Path.GetFullPath($"{Application.dataPath}/../platform/Harmony/word_huawei.p12");
			PlayerSettings.OpenHarmony.keystorePass = "word123456";
			PlayerSettings.OpenHarmony.keyaliasName = "word";
			PlayerSettings.OpenHarmony.keyaliasPass = "word123456";
			PlayerSettings.OpenHarmony.openHarmonyAppID = "6917574075102471091";
			PlayerSettings.OpenHarmony.openHarmonyClientID = "461323198429956566";
			var p7Name = buildParam.IsBuildRelease ? "profile01Release.p7b" : "profile02Debug.p7b";
			var cerName = buildParam.IsBuildRelease ? "release01.cer" : "debug01.cer";
			PlayerSettings.OpenHarmony.openHarmonyProfile =
				Path.GetFullPath($"{Application.dataPath}/../platform/Harmony/{p7Name}");
			PlayerSettings.OpenHarmony.openHarmonyCertificate =
				Path.GetFullPath($"{Application.dataPath}/../platform/Harmony/{cerName}");

			//打资源包
			AssetBundleBuilder.BuildAssetBundles(false);
			//打版本包
			var outputDir = Path.GetFullPath($"{Application.dataPath}/../output/Harmony");
			if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
			// var symbolDefine = buildParam.IsBuildRelease ? "release" : "debug";
			// var version = PlayerSettings.bundleVersion.Replace(".", "");
			// var hapPath = $"{outputDir}/{symbolDefine}_{version}_{DateTime.Now:yyyy-MM-dd-HHmmss}.hap";
			var harProject = Path.Combine(outputDir, "project");

			var report = BuildPipeline.BuildPlayer(GetBuildScenes(), harProject, BuildTarget.OpenHarmony,
				BuildOptions.None);
			if (report.summary.result != BuildResult.Succeeded)
			{
				Debug.Log("打包失败");
				return;
			}

			Debug.Log("打包成功");
			if (buildParam.BuildType == BuildType.EditorBuild)
				Application.OpenURL(@"file://" + outputDir);
			//todo: hdc install xxx
		}
		#endif
		private static void BuildAndroid(BuildParam buildParam)
		{
			if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
			{
				Debug.LogError("先切换平台");
				return;
			}

			//宏设置
			SetDefineSymbols(BuildTargetGroup.Android, buildParam);
			//打包设置
			EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
			EditorUserBuildSettings.development = !buildParam.IsBuildRelease;
			EditorUserBuildSettings.buildAppBundle = buildParam.IsBuildRelease;
			EditorUserBuildSettings.androidCreateSymbols = buildParam.IsBuildRelease
				? AndroidCreateSymbols.Public
				: AndroidCreateSymbols.Disabled;
			PlayerSettings.Android.minifyRelease = buildParam.IsBuildRelease;
			PlayerSettings.bundleVersion = buildParam.BuildVersion;
			PlayerSettings.Android.bundleVersionCode = GenBuildNumber();
			PlayerSettings.stripEngineCode = true;
			PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
			PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
			PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
			PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
			PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;

			//账户设置
			SetDefaultIcon(2);
			PlayerSettings.companyName = "NeoPlay";
			PlayerSettings.productName = "熟語の消しクイズ";
			PlayerSettings.applicationIdentifier = "com.neoplay.zen.idiom.flow";
			PlayerSettings.Android.useCustomKeystore = true;
			PlayerSettings.Android.keystoreName =
				Path.GetFullPath($"{Application.dataPath}/../platform/Android/puzzle.keystore");
			PlayerSettings.Android.keystorePass = "neo654321";
			PlayerSettings.Android.keyaliasName = "neo";
			PlayerSettings.Android.keyaliasPass = "neo654321";
			//打资源包
			AssetBundleBuilder.BuildAssetBundles(false);
			//打版本包
			var outputDir = Path.GetFullPath($"{Application.dataPath}/../output/Android");
			if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
			var symbolDefine = buildParam.IsBuildRelease ? "release" : "debug";
			var version = PlayerSettings.bundleVersion.Replace(".", "");
			var extName = buildParam.IsBuildRelease ? "aab" : "apk";
			var apkPath = $"{outputDir}/{symbolDefine}_{version}_{DateTime.Now:yyyy-MM-dd-HHmmss}.{extName}";
			//var andProject = Path.Combine(outputDir, "project");

			var opts = !buildParam.IsBuildRelease ? BuildOptions.Development : BuildOptions.None;
			var report = BuildPipeline.BuildPlayer(GetBuildScenes(), apkPath, BuildTarget.Android, opts);
			if (report.summary.result != BuildResult.Succeeded)
			{
				Debug.Log("打包失败");
				return;
			}

			Debug.Log("打包成功");
			if (buildParam.BuildType == BuildType.EditorBuild)
				Application.OpenURL(@"file://" + outputDir);
			//todo: adb install xxx
		}

		private static void BuildIOS(BuildParam buildParam)
		{
			if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
			{
				Debug.LogError("先切换平台");
				return;
			}

			//宏设置
			SetDefineSymbols(BuildTargetGroup.iOS, buildParam);
			//打包设置
			PlayerSettings.stripEngineCode = true;
			PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.iOS, ManagedStrippingLevel.Low);
			PlayerSettings.bundleVersion = buildParam.BuildVersion;
			PlayerSettings.iOS.buildNumber = GenBuildNumber().ToString();

			PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
			PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
			PlayerSettings.iOS.targetOSVersionString = "13.0";

			PlayerSettings.iOS.appleDeveloperTeamID = "xxx";
			PlayerSettings.iOS.appleEnableAutomaticSigning = true;
			PlayerSettings.iOS.appInBackgroundBehavior = iOSAppInBackgroundBehavior.Custom;
			PlayerSettings.iOS.backgroundModes = iOSBackgroundMode.RemoteNotification | iOSBackgroundMode.Fetch;
			//账户设置
			SetDefaultIcon(3);
			PlayerSettings.companyName = "HexaSpace Games";
			PlayerSettings.productName = "禅の熟語消し";
			PlayerSettings.applicationIdentifier = "idiom.block.zen.tw";
			//打资源包
			AssetBundleBuilder.BuildAssetBundles(false);
			//打版本包
			var outputDir = Path.GetFullPath($"{Application.dataPath}/../output/IOS");
			if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
			var xcodePath = Path.Combine(outputDir, "xcode");

			var opts = !buildParam.IsBuildRelease ? BuildOptions.Development : BuildOptions.None;
			var report = BuildPipeline.BuildPlayer(GetBuildScenes(), xcodePath, BuildTarget.iOS, opts);
			if (report.summary.result != BuildResult.Succeeded)
			{
				Debug.Log("打包失败");
				return;
			}

			Debug.Log("打包成功");
			if (buildParam.BuildType == BuildType.EditorBuild)
				Application.OpenURL(@"file://" + xcodePath);
			//iOSUtil.WriteLanguage(xcodePath);
		}

		private static void SetDefaultIcon(int id)
		{
			var iconPath = "Assets/FourWordIdiom/LocalGame/Icons/icon.png";
			var sourcePath = $"Assets/FourWordIdiom/LocalGame/Icons/icon{id}.png";
			File.Copy(sourcePath, iconPath, true);
			AssetDatabase.ImportAsset(iconPath);
		}
		private static void SetDefineSymbols(BuildTargetGroup target, BuildParam buildParam)
		{
			var defines = new List<string>();
			if (buildParam.IsBuildRelease)
				defines.Add(DefineRelease);
			if (buildParam.IsBuildShowLog)
				defines.Add(DefineShowLog);
			PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defines.ToArray());
		}

		private static int GenBuildNumber()
		{
			var nowDate = DateTime.Now;
			var strBuildNumber =
				$"{nowDate.Year - 2000}{nowDate.Month:00}{nowDate.Day:00}{(nowDate.Hour * 60 + nowDate.Minute) / 15}";
			var buildNumber = int.Parse(strBuildNumber);
			return buildNumber;
		}

		private static string[] GetBuildScenes()
		{
			var names = new List<string>();
			foreach (var e in EditorBuildSettings.scenes)
			{
				if (e == null) continue;
				if (e.enabled) names.Add(e.path);
			}

			return names.ToArray();
		}
	}
}

