using System.Collections.Generic;
using Game.Ads;
using Game.Settings;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class BuildProfileMenu
    {
        private const string DevProfilePath = "Assets/ArrowPopBall/Settings/BuildProfiles/Dev.asset";
        private const string LiveProfilePath = "Assets/ArrowPopBall/Settings/BuildProfiles/Live.asset";
        private const string AdsSdkSettingsPath = "Assets/ArrowPopBall/Resources/Ads/AdsSdkSettings.asset";

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
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, profile.AndroidApplicationIdentifier);
                changes.Add($"androidId='{profile.AndroidApplicationIdentifier}'");
            }

            if (!string.IsNullOrWhiteSpace(profile.IosApplicationIdentifier))
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, profile.IosApplicationIdentifier);
                changes.Add($"iosId='{profile.IosApplicationIdentifier}'");
            }

            if (!string.IsNullOrWhiteSpace(profile.AppleDeveloperTeamID))
            {
                PlayerSettings.iOS.appleDeveloperTeamID = profile.AppleDeveloperTeamID;
                changes.Add($"appleTeamId='{profile.AppleDeveloperTeamID}'");
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
                Debug.Log($"[BuildProfileMenu] Ads SDK settings '{settings.name}' look complete.");
                return;
            }

            Debug.LogWarning($"[BuildProfileMenu] Ads SDK settings '{settings.name}' are missing: {string.Join(", ", issues)}");
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
    }
}
