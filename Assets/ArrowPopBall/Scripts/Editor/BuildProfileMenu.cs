using System.Collections.Generic;
using Game.Ads;
using Game.Settings;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.Editor
{
    public static class BuildProfileMenu
    {
        private const string DevProfilePath = "Assets/ArrowPopBall/Settings/BuildProfiles/Dev.asset";
        private const string LiveProfilePath = "Assets/ArrowPopBall/Settings/BuildProfiles/Live.asset";
        private const string AdsSdkSettingsPath = "Assets/ArrowPopBall/Resources/Ads/AdsSdkSettings.asset";
        private const string DevAdsProfilePath = "Assets/ArrowPopBall/Settings/AdsSdkProfiles/Dev.asset";
        private const string LiveAdsProfilePath = "Assets/ArrowPopBall/Settings/AdsSdkProfiles/Live.asset";
        private const string AppLovinSettingsPath = "Assets/MaxSdk/Resources/AppLovinSettings.asset";

        [MenuItem("Tools/Arrow Pop/Build Profiles/Apply Dev")]
        private static void ApplyDevProfile()
        {
            ApplyProfile(DevProfilePath);
        }

        [MenuItem("Tools/Arrow Pop/Build Profiles/Apply Live")]
        private static void ApplyLiveProfile()
        {
            ApplyProfile(LiveProfilePath);
        }

        [MenuItem("Tools/Arrow Pop/Build Profiles/Select Dev Asset")]
        private static void SelectDevProfile()
        {
            PingAsset<BuildProfileSO>(DevProfilePath);
        }

        [MenuItem("Tools/Arrow Pop/Build Profiles/Select Live Asset")]
        private static void SelectLiveProfile()
        {
            PingAsset<BuildProfileSO>(LiveProfilePath);
        }

        [MenuItem("Tools/Arrow Pop/Ads/Select SDK Settings")]
        private static void SelectAdsSdkSettings()
        {
            PingAsset<AdsSdkSettingsSO>(AdsSdkSettingsPath);
        }

        [MenuItem("Tools/Arrow Pop/Ads/Select Dev Ads Profile")]
        private static void SelectDevAdsProfile()
        {
            PingAsset<AdsSdkSettingsSO>(DevAdsProfilePath);
        }

        [MenuItem("Tools/Arrow Pop/Ads/Select Live Ads Profile")]
        private static void SelectLiveAdsProfile()
        {
            PingAsset<AdsSdkSettingsSO>(LiveAdsProfilePath);
        }

        [MenuItem("Tools/Arrow Pop/Ads/Sync Runtime Settings To AppLovin Asset")]
        private static void SyncRuntimeSettingsToAppLovinAsset()
        {
            var runtimeAdsSettings = AssetDatabase.LoadAssetAtPath<AdsSdkSettingsSO>(AdsSdkSettingsPath);
            if (runtimeAdsSettings == null)
            {
                Debug.LogError($"[BuildProfileMenu] Runtime ads settings not found: {AdsSdkSettingsPath}");
                return;
            }

            SyncToAppLovinAsset(runtimeAdsSettings);
        }

        [MenuItem("Tools/Arrow Pop/Validate/Profiles And Ads")]
        private static void ValidateProfilesAndAds()
        {
            ValidateProfile(DevProfilePath);
            ValidateProfile(LiveProfilePath);
            ValidateAdsSdkSettings();
            Debug.Log("[BuildProfileMenu] Validation finished.");
        }

        private static void ApplyProfile(string assetPath)
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfileSO>(assetPath);
            if (profile == null)
            {
                Debug.LogError($"[BuildProfileMenu] Build profile not found: {assetPath}");
                return;
            }

            var changes = new List<string>();

            if (!string.IsNullOrWhiteSpace(profile.ProductName))
            {
                PlayerSettings.productName = profile.ProductName;
                changes.Add($"productName='{profile.ProductName}'");
            }

            if (!string.IsNullOrWhiteSpace(profile.AndroidApplicationIdentifier))
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, profile.AndroidApplicationIdentifier);
                changes.Add($"androidId='{profile.AndroidApplicationIdentifier}'");
            }

            if (!string.IsNullOrWhiteSpace(profile.IosApplicationIdentifier))
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, profile.IosApplicationIdentifier);
                changes.Add($"iosId='{profile.IosApplicationIdentifier}'");
            }

            if (!string.IsNullOrWhiteSpace(profile.AppleDeveloperTeamID))
            {
                PlayerSettings.iOS.appleDeveloperTeamID = profile.AppleDeveloperTeamID;
                changes.Add($"appleTeamId='{profile.AppleDeveloperTeamID}'");
            }

            if (profile.AdsSdkSettingsProfile != null)
            {
                var runtimeAdsSettings = AssetDatabase.LoadAssetAtPath<AdsSdkSettingsSO>(AdsSdkSettingsPath);
                if (runtimeAdsSettings == null)
                {
                    Debug.LogError($"[BuildProfileMenu] Runtime ads settings not found: {AdsSdkSettingsPath}");
                }
                else
                {
                    Undo.RecordObject(runtimeAdsSettings, "Apply Ads SDK Settings Profile");
                    EditorUtility.CopySerialized(profile.AdsSdkSettingsProfile, runtimeAdsSettings);
                    EditorUtility.SetDirty(runtimeAdsSettings);
                    changes.Add($"adsSdkProfile='{profile.AdsSdkSettingsProfile.name}'");
                    if (SyncToAppLovinAsset(runtimeAdsSettings))
                        changes.Add("appLovinSettingsSynced");
                }
            }

            AssetDatabase.SaveAssets();

            if (changes.Count == 0)
            {
                Debug.LogWarning($"[BuildProfileMenu] '{profile.name}' applied nothing because all fields were empty.");
                return;
            }

            Debug.Log($"[BuildProfileMenu] Applied '{profile.name}' ({profile.ProfileType}): {string.Join(", ", changes)}");
        }

        private static void ValidateProfile(string assetPath)
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfileSO>(assetPath);
            if (profile == null)
            {
                Debug.LogError($"[BuildProfileMenu] Missing build profile: {assetPath}");
                return;
            }

            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(profile.ProductName))
                issues.Add("productName");
            if (string.IsNullOrWhiteSpace(profile.AndroidApplicationIdentifier))
                issues.Add("androidApplicationIdentifier");
            if (string.IsNullOrWhiteSpace(profile.IosApplicationIdentifier))
                issues.Add("iosApplicationIdentifier");
            if (string.IsNullOrWhiteSpace(profile.AppleDeveloperTeamID))
                issues.Add("appleDeveloperTeamID");
            if (profile.AdsSdkSettingsProfile == null)
            {
                issues.Add("adsSdkSettingsProfile");
            }
            else
            {
                ValidateAdsSdkSettings(profile.AdsSdkSettingsProfile, $"profile '{profile.name}'");
            }

            if (issues.Count == 0)
            {
                Debug.Log($"[BuildProfileMenu] Profile '{profile.name}' looks complete.");
                return;
            }

            Debug.LogWarning($"[BuildProfileMenu] Profile '{profile.name}' is missing: {string.Join(", ", issues)}");
        }

        private static void ValidateAdsSdkSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AdsSdkSettingsSO>(AdsSdkSettingsPath);
            if (settings == null)
            {
                Debug.LogError($"[BuildProfileMenu] Missing ads SDK settings: {AdsSdkSettingsPath}");
                return;
            }

            ValidateAdsSdkSettings(settings, "runtime");
            ValidateAppLovinSettingsAssetExists();
        }

        private static void ValidateAdsSdkSettings(AdsSdkSettingsSO settings, string label)
        {
            if (settings == null)
            {
                Debug.LogError($"[BuildProfileMenu] Missing ads SDK settings for {label}");
                return;
            }

            var issues = new List<string>();
            if (!settings.HasSdkKey)
                issues.Add("appLovinSdkKey");
            if (!settings.HasUnitId(AdUnitSlot.Banner))
                issues.Add("bannerUnitId");
            if (!settings.HasUnitId(AdUnitSlot.Interstitial))
                issues.Add("interstitialUnitId");
            if (!settings.HasUnitId(AdUnitSlot.Rewarded))
                issues.Add("rewardedUnitId");

            if (issues.Count == 0)
            {
                Debug.Log($"[BuildProfileMenu] Ads SDK settings '{settings.name}' for {label} look complete.");
                return;
            }

            Debug.LogWarning($"[BuildProfileMenu] Ads SDK settings '{settings.name}' for {label} are missing: {string.Join(", ", issues)}");
        }

        private static void ValidateAppLovinSettingsAssetExists()
        {
            if (EnsureAppLovinSettingsAsset() == null)
            {
                Debug.LogWarning($"[BuildProfileMenu] AppLovin settings asset not found: {AppLovinSettingsPath}. MAX package is probably not installed yet.");
            }
        }

        private static bool SyncToAppLovinAsset(AdsSdkSettingsSO settings)
        {
            var appLovinSettingsAsset = EnsureAppLovinSettingsAsset();
            if (appLovinSettingsAsset == null)
            {
                Debug.LogWarning($"[BuildProfileMenu] AppLovin settings asset not found: {AppLovinSettingsPath}");
                return false;
            }

            var serializedObject = new SerializedObject(appLovinSettingsAsset);
            SetStringIfPropertyExists(serializedObject, "sdkKey", settings.AppLovinSdkKey);
            SetStringIfPropertyExists(serializedObject, "adMobAndroidAppId", settings.AdMobAndroidAppId);
            SetStringIfPropertyExists(serializedObject, "adMobIosAppId", settings.AdMobIosAppId);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(appLovinSettingsAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BuildProfileMenu] Synced AppLovin settings asset from '{settings.name}'.");
            return true;
        }

        private static Object EnsureAppLovinSettingsAsset()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(AppLovinSettingsPath);
            if (asset != null)
                return asset;

            var settingsType = FindEditorType("AppLovinSettings");
            var instanceProperty = settingsType?.GetProperty("Instance");
            _ = instanceProperty?.GetValue(null, null);

            AssetDatabase.Refresh();
            return AssetDatabase.LoadMainAssetAtPath(AppLovinSettingsPath);
        }

        private static void SetStringIfPropertyExists(SerializedObject serializedObject, string propertyName, string value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return;

            property.stringValue = value ?? string.Empty;
        }

        private static void PingAsset<T>(string assetPath) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                Debug.LogError($"[BuildProfileMenu] Asset not found: {assetPath}");
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static System.Type FindEditorType(string typeName)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type type = null;
                try
                {
                    type = assembly.GetType(typeName) ?? System.Array.Find(assembly.GetTypes(), candidate => candidate.Name == typeName);
                }
                catch (System.Reflection.ReflectionTypeLoadException)
                {
                    // Ignore broken editor assemblies and keep scanning.
                }

                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
