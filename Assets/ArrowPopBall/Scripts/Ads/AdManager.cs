using System;
using System.Collections;
using Game.Utilities;
using UnityEngine;

namespace Game.Ads
{
    public enum AdBannerPlacement
    {
        LobbyBottom,
        IngameBottom
    }

    public enum AdTrigger
    {
        LevelClear,
        FailContinue
    }

    public class AdManager : SingletonMono<AdManager>
    {
        private const string SettingsResourcePath = "Ads/AdSettings";
        private const string SdkSettingsResourcePath = "Ads/AdsSdkSettings";

        private int _clearCountSinceInterstitial;
        private float _lastInterstitialTime = float.MinValue;
        private bool _bannerVisible;
        private AdBannerPlacement _currentBannerPlacement;
        private AdSettingsSO _settings;
        private AdsSdkSettingsSO _sdkSettings;

        public AdSettingsSO Settings => _settings;
        public AdsSdkSettingsSO SdkSettings => _sdkSettings;
        public bool IsBannerVisible => _bannerVisible;

        protected override void OnAwake()
        {
            LoadSettings();
            LoadSdkSettings();
            _clearCountSinceInterstitial = 0;
            _lastInterstitialTime = float.MinValue;
            Debug.Log($"[AdManager] Thin ad manager initialized settings='{_settings.name}' sdkSettings='{_sdkSettings.name}'");
        }

        public void ShowLobbyBanner()
        {
            if (!_settings.ShowLobbyBanner)
                return;

            ShowBanner(AdBannerPlacement.LobbyBottom);
        }

        public void ShowIngameBanner()
        {
            if (!_settings.ShowIngameBanner)
                return;

            ShowBanner(AdBannerPlacement.IngameBottom);
        }

        public void ShowBanner(AdBannerPlacement placement)
        {
            _bannerVisible = true;
            _currentBannerPlacement = placement;
            Debug.Log($"[AdManager] ShowBanner: {placement}, unitId='{GetBannerUnitId()}'");
        }

        public void HideBanner()
        {
            if (!_bannerVisible)
                return;

            _bannerVisible = false;
            Debug.Log($"[AdManager] HideBanner: {_currentBannerPlacement}");
        }

        public void RecordLevelClear()
        {
            _clearCountSinceInterstitial++;
            Debug.Log($"[AdManager] RecordLevelClear: count={_clearCountSinceInterstitial}");
        }

        public bool CanShowInterstitial(int currentLevel)
        {
            if (currentLevel < _settings.InterstitialStartLevel)
                return false;

            if (_clearCountSinceInterstitial < _settings.InterstitialEveryClears)
                return false;

            float elapsed = Time.realtimeSinceStartup - _lastInterstitialTime;
            if (elapsed < _settings.InterstitialCooldownSeconds)
                return false;

            return true;
        }

        public void TryShowInterstitial(AdTrigger trigger, int currentLevel, Action onComplete)
        {
            if (!CanShowInterstitial(currentLevel))
            {
                onComplete?.Invoke();
                return;
            }

            Debug.Log($"[AdManager] TryShowInterstitial: {trigger}, level={currentLevel}, unitId='{GetInterstitialUnitId()}'");

#if UNITY_EDITOR
            if (_settings.SimulateInterstitialInEditor)
            {
                StartCoroutine(SimulateInterstitialCoroutine(onComplete));
                return;
            }
#endif

            // TODO: Replace with actual network SDK show call.
            _lastInterstitialTime = Time.realtimeSinceStartup;
            _clearCountSinceInterstitial = 0;
            onComplete?.Invoke();
        }

        public void ShowRewarded(AdTrigger trigger, Action<bool> onComplete)
        {
            Debug.Log($"[AdManager] ShowRewarded: {trigger}, unitId='{GetRewardedUnitId()}'");

#if UNITY_EDITOR
            StartCoroutine(SimulateRewardedCoroutine(onComplete));
            return;
#endif

            // TODO: Replace with actual network SDK show call.
            onComplete?.Invoke(false);
        }

        private IEnumerator SimulateInterstitialCoroutine(Action onComplete)
        {
            yield return new WaitForSecondsRealtime(_settings.SimulatedAdDuration);
            _lastInterstitialTime = Time.realtimeSinceStartup;
            _clearCountSinceInterstitial = 0;
            Debug.Log("[AdManager] Simulated interstitial closed");
            onComplete?.Invoke();
        }

        private IEnumerator SimulateRewardedCoroutine(Action<bool> onComplete)
        {
            yield return new WaitForSecondsRealtime(_settings.SimulatedAdDuration);
            Debug.Log($"[AdManager] Simulated rewarded completed: {_settings.SimulateRewardedSuccessInEditor}");
            onComplete?.Invoke(_settings.SimulateRewardedSuccessInEditor);
        }

        private void LoadSettings()
        {
            _settings = Resources.Load<AdSettingsSO>(SettingsResourcePath);
            if (_settings != null)
                return;

            Debug.LogWarning($"[AdManager] Missing Resources/{SettingsResourcePath}.asset. Using runtime defaults.");
            _settings = ScriptableObject.CreateInstance<AdSettingsSO>();
        }

        private void LoadSdkSettings()
        {
            _sdkSettings = Resources.Load<AdsSdkSettingsSO>(SdkSettingsResourcePath);
            if (_sdkSettings != null)
                return;

            Debug.LogWarning($"[AdManager] Missing Resources/{SdkSettingsResourcePath}.asset. Using runtime defaults.");
            _sdkSettings = ScriptableObject.CreateInstance<AdsSdkSettingsSO>();
        }

        public string GetBannerUnitId()
        {
            return GetUnitId(AdUnitSlot.Banner);
        }

        public string GetInterstitialUnitId()
        {
            return GetUnitId(AdUnitSlot.Interstitial);
        }

        public string GetRewardedUnitId()
        {
            return GetUnitId(AdUnitSlot.Rewarded);
        }

        private string GetUnitId(AdUnitSlot slot)
        {
            if (_sdkSettings == null)
                return string.Empty;

            return _sdkSettings.GetUnitId(slot, Application.platform);
        }
    }
}
