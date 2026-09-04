using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using System.Text;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using OJ.Build;

namespace OJ.EditorTools
{
    #if UNITY_IOS
        using UnityEditor.iOS.Xcode;
        using UnityEditor.Callbacks;
        using AppleAuth.Editor;
        using UnityEditor.iOS.Xcode;
    #endif

    public static class Unity3dBuilder
    {
        static string ANDROID_BUILD_PATH = "_Build/Android";
        static string IOS_BUILD_PATH = "_Build/iOS";

        static string KEYSTORE_PATH = "Keystore/osw.keystore";
        static string KEYSTORE_ALIAS_NAME = "osw";

        // 자격증명은 소스에 두지 않는다. 빌드하는 사람이 환경 변수로 주입한다.
        //
        //   set OJ_KEYSTORE_PASS=...
        //   set OJ_KEYSTORE_ALIAS_PASS=...
        //
        // 비어 있으면 Unity 는 조용히 디버그 키스토어로 서명해 버린다. 스토어에
        // 올리는 순간까지 정상으로 보이므로, 여기서 명시적으로 실패시킨다.
        const string KEYSTORE_PWD_ENV = "OJ_KEYSTORE_PASS";
        const string KEYSTORE_ALIAS_PWD_ENV = "OJ_KEYSTORE_ALIAS_PASS";

        static string IOS_TARGET_OS = "12.0";
        static string IOS_PROFILE_DEVELOPMENT_UUID = "";
        static string IOS_PROFILE_DISTRIBUTE_UUID = "";

        [MenuItem("Build/Android (Development)")]
        public static void PerformAndroidDevelopmentBuild()
        {
            SetSettingsForAndroid();
            EditorUserBuildSettings.buildAppBundle = false;

            string directoryName = CreateAndroidDirectory(ANDROID_BUILD_PATH);
            string fileName = string.Format("{0}.apk", Application.productName);

            Build(directoryName + fileName, BuildTarget.Android, BuildOptions.None);
        }

        [MenuItem("Build/Android (Release)")]
        public static void PerformAndroidReleaseBuild()
        {
            SetSettingsForAndroid();
            EditorUserBuildSettings.buildAppBundle = true;

            string directoryName = CreateAndroidDirectory(ANDROID_BUILD_PATH);
            string fileName = string.Format("{0}.aab", Application.productName);

            Build(directoryName + fileName, BuildTarget.Android, BuildOptions.None);
        }

        [MenuItem("Build/iOS (Development)")]
        public static void PerformiOSDevelopmentBuild()
        {
            SetSettingsForIOS(IOS_PROFILE_DEVELOPMENT_UUID);

            Build(CreateDirectoryForIOS(IOS_BUILD_PATH), BuildTarget.iOS, BuildOptions.AllowDebugging);
        }

        [MenuItem("Build/iOS (Release)")]
        public static void PerformiOSReleaseBuild()
        {
            SetSettingsForIOS(IOS_PROFILE_DISTRIBUTE_UUID);

            Build(CreateDirectoryForIOS(IOS_BUILD_PATH), BuildTarget.iOS, BuildOptions.None);
        }

        private static string[] GetDefine_Symbols()
        {
            OJ.Build.BuildEnvironmentSelectAsset asset = AssetDatabase.LoadAssetAtPath<OJ.Build.BuildEnvironmentSelectAsset>("Assets/ScriptableObject/BuildEnvironmentSelect.asset");
            if (asset.BuildElement == OJ.Build.BuildEnvironmentEnum.Develop
                || asset.BuildElement == OJ.Build.BuildEnvironmentEnum.QA)
            {
                //return new string[] { "UNITASK_DOTWEEN_SUPPORT", "DEV_DEFINE" };
                return new string[] { "DEV_DEFINE" };
            }

            //return new string[] { "UNITASK_DOTWEEN_SUPPORT" };
            return new string[] { "" };
        }

        static string RequireEnv(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value))
            {
                throw new Exception(string.Format(
                    "환경 변수 {0} 가 비어 있어 안드로이드 빌드를 중단한다. " +
                    "키스토어 자격증명은 소스에 두지 않으므로 빌드 전에 직접 설정해야 한다. " +
                    "그대로 진행하면 디버그 키로 서명된 채 정상처럼 보이는 산출물이 나온다.", name));
            }

            return value;
        }

        static string CreateAndroidDirectory(string directoryName)
        {
            string buildPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + directoryName;
            Directory.CreateDirectory(buildPath);

            return buildPath.TrimEnd('/') + Path.DirectorySeparatorChar;
        }

        static void SetSettingsForAndroid()
        {
            PlayerSettings.Android.keystoreName = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + KEYSTORE_PATH;
            PlayerSettings.Android.keystorePass = RequireEnv(KEYSTORE_PWD_ENV);
            PlayerSettings.Android.keyaliasName = KEYSTORE_ALIAS_NAME;
            PlayerSettings.Android.keyaliasPass = RequireEnv(KEYSTORE_ALIAS_PWD_ENV);

            PlayerSettings.bundleVersion = Project.version;
            PlayerSettings.Android.bundleVersionCode = Project.versionCode;

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, GetDefine_Symbols());

            //SetGoogleLoginPlugins();

            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        static string CreateDirectoryForIOS(string directoryName)
        {
            string buildPath = Path.GetFullPath(".") + Path.DirectorySeparatorChar + directoryName;
            Directory.CreateDirectory(buildPath);

            return buildPath;
        }

        static void SetSettingsForIOS(string profileUUID)
        {
            //string buildNumber = GetCommandArg("BUILD_NUMBER");

            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.bundleVersion = Project.version;
            PlayerSettings.iOS.buildNumber = Project.versionCode.ToString();
            //PlayerSettings.iOS.buildNumber = !string.IsNullOrEmpty(buildNumber) ? buildNumber : PlayerSettings.iOS.buildNumber;
            PlayerSettings.iOS.targetOSVersionString = IOS_TARGET_OS;
            PlayerSettings.iOS.iOSManualProvisioningProfileID = profileUUID;
            PlayerSettings.iOS.appleEnableAutomaticSigning = string.IsNullOrEmpty(profileUUID);
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS, GetDefine_Symbols());

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
        }

        static void Build(string pathName, BuildTarget target, BuildOptions options)
        {
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
            buildPlayerOptions.scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            buildPlayerOptions.locationPathName = pathName;
            buildPlayerOptions.target = target;
            buildPlayerOptions.options = options;

            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log("Build Succeeded: " + report.summary.totalSize + " Bytes");
            }
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Failed)
            {
                throw new Exception("Build Failed: " + report.summary.result);
            }
        }

        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget target, string buildPath)
        {
    #if UNITY_IOS
            if (target == BuildTarget.iOS)
            {
                var projectPath = PBXProject.GetPBXProjectPath(buildPath);
                var project = new PBXProject();
                project.ReadFromFile(projectPath);
                {
                    //project.SetBuildProperty(project.GetUnityMainTargetGuid(), "ENABLE_BITCODE", "NO");
                    project.SetBuildProperty(project.GetUnityFrameworkTargetGuid(), "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");
                    //project.SetBuildProperty(project.TargetGuidByName(PBXProject.GetUnityTestTargetName()), "ENABLE_BITCODE", "NO");
                    //project.SetBuildProperty(project.GetUnityFrameworkTargetGuid(), "ENABLE_BITCODE", "NO");
                    //project.SetBuildProperty(project.GetUnityMainTargetGuid(), "ENABLE_BITCODE", "NO");
                }

                project.WriteToFile(projectPath);

                var capability = new ProjectCapabilityManager(projectPath, "Entitlements.entitlements", targetGuid: project.GetUnityMainTargetGuid());
                {
                    capability.AddSignInWithApple();
                    capability.AddPushNotifications(true);
                    //capability.AddGameCenter();
                }
                capability.WriteToFile();

                var plistPath = Path.Combine(buildPath, "Info.plist");
                var plist = new PlistDocument();
                plist.ReadFromString(File.ReadAllText(plistPath));
                {
                    // 수출 규정(암호화 사용) 신고 대응: 해당 없음으로 표시해 업로드마다 묻지 않게 한다
                    plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
                    //plist.root.SetString("NSAdvertisingAttributionReportEndpoint", "https://appsflyer-skadnetwork.com/");
                }
                File.WriteAllText(plistPath, plist.WriteToString());
            }
    #endif
        }
    }
}
