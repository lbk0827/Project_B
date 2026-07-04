using System;
using System.Collections;
using Game.UI;
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

    [Serializable]
    public class AdPolicy
    {
        [SerializeField] private bool _showLobbyBanner = true;
        [SerializeField] private bool _showIngameBanner = true;
        [SerializeField] private int _interstitialStartLevel = 3;
        [SerializeField] private int _interstitialEveryClears = 2;
        [SerializeField] private float _interstitialCooldownSeconds = 90f;

        public bool ShowLobbyBanner => _showLobbyBanner;
        public bool ShowIngameBanner => _showIngameBanner;
        public int InterstitialStartLevel => _interstitialStartLevel;
        public int InterstitialEveryClears => Mathf.Max(1, _interstitialEveryClears);
        public float InterstitialCooldownSeconds => Mathf.Max(0f, _interstitialCooldownSeconds);
    }

    public class AdManager : SingletonMono<AdManager>
    {
        [Header("Policy")]
        [SerializeField] private AdPolicy _policy = new AdPolicy();

        [Header("Editor Simulation")]
        [SerializeField] private bool _simulateInterstitialInEditor = true;
        [SerializeField] private bool _simulateRewardedSuccessInEditor = true;
        [SerializeField] private float _simulatedAdDuration = 0.5f;

        private int _clearCountSinceInterstitial;
        private float _lastInterstitialTime = float.MinValue;
        private bool _bannerVisible;
        private AdBannerPlacement _currentBannerPlacement;

        public AdPolicy Policy => _policy;
        public bool IsBannerVisible => _bannerVisible;

        protected override void OnAwake()
        {
            _clearCountSinceInterstitial = 0;
            _lastInterstitialTime = float.MinValue;
            Debug.Log("[AdManager] Thin ad manager initialized");
        }

        public void ShowLobbyBanner()
        {
            if (!_policy.ShowLobbyBanner)
                return;

            ShowBanner(AdBannerPlacement.LobbyBottom);
        }

        public void ShowIngameBanner()
        {
            if (!_policy.ShowIngameBanner)
                return;

            ShowBanner(AdBannerPlacement.IngameBottom);
        }

        public void ShowBanner(AdBannerPlacement placement)
        {
            _bannerVisible = true;
            _currentBannerPlacement = placement;
            Debug.Log($"[AdManager] ShowBanner: {placement}");
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
            if (currentLevel < _policy.InterstitialStartLevel)
                return false;

            if (_clearCountSinceInterstitial < _policy.InterstitialEveryClears)
                return false;

            float elapsed = Time.realtimeSinceStartup - _lastInterstitialTime;
            if (elapsed < _policy.InterstitialCooldownSeconds)
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

            Debug.Log($"[AdManager] TryShowInterstitial: {trigger}, level={currentLevel}");

#if UNITY_EDITOR
            if (_simulateInterstitialInEditor)
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
            Debug.Log($"[AdManager] ShowRewarded: {trigger}");

#if UNITY_EDITOR
            StartCoroutine(SimulateRewardedCoroutine(onComplete));
            return;
#endif

            // TODO: Replace with actual network SDK show call.
            onComplete?.Invoke(false);
        }

        private IEnumerator SimulateInterstitialCoroutine(Action onComplete)
        {
            yield return new WaitForSecondsRealtime(_simulatedAdDuration);
            _lastInterstitialTime = Time.realtimeSinceStartup;
            _clearCountSinceInterstitial = 0;
            Debug.Log("[AdManager] Simulated interstitial closed");
            onComplete?.Invoke();
        }

        private IEnumerator SimulateRewardedCoroutine(Action<bool> onComplete)
        {
            yield return new WaitForSecondsRealtime(_simulatedAdDuration);
            Debug.Log($"[AdManager] Simulated rewarded completed: {_simulateRewardedSuccessInEditor}");
            onComplete?.Invoke(_simulateRewardedSuccessInEditor);
        }
    }
}
