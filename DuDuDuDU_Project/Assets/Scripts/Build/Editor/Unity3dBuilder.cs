using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using System.Text;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

#if UNITY_IOS
    using UnityEditor.Callbacks;
    using AppleAuth.Editor;
    using UnityEditor.iOS.Xcode;
#endif

public static class Unity3dBuilder
{
    static string ANDROID_BUILD_PATH = "_Build/Android";
    static string IOS_BUILD_PATH = "_Build/iOS";

    static string KEYSTORE_PATH = "Keystore/osw.keystore";
    static string KEYSTORE_PWD = "dh121413";
    static string KEYSTORE_ALIAS_NAME = "osw";
    static string KEYSTORE_ALIAS_PWD = "dh121413";

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
        OJ.BuildEnvironmentSelectAsset asset = AssetDatabase.LoadAssetAtPath<OJ.BuildEnvironmentSelectAsset>("Assets/ScriptableObject/BuildEnvironmentSelect.asset");
        if (asset.BuildElement == OJ.BuildEnvironmentEnum.Develop
            || asset.BuildElement == OJ.BuildEnvironmentEnum.QA)
        {
            //return new string[] { "UNITASK_DOTWEEN_SUPPORT", "DEV_DEFINE" };
            return new string[] { "DEV_DEFINE" };
        }

        //return new string[] { "UNITASK_DOTWEEN_SUPPORT" };
        return new string[] { "" };
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
        PlayerSettings.Android.keystorePass = KEYSTORE_PWD;
        PlayerSettings.Android.keyaliasName = KEYSTORE_ALIAS_NAME;
        PlayerSettings.Android.keyaliasPass = KEYSTORE_ALIAS_PWD;

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
                // ���� ���� ���� ���� ����: �ش����� ���� ����
                plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
                //plist.root.SetString("NSAdvertisingAttributionReportEndpoint", "https://appsflyer-skadnetwork.com/");
            }
            File.WriteAllText(plistPath, plist.WriteToString());
        }
    }
}