using UnityEngine;

namespace Game.Ads
{
    [CreateAssetMenu(fileName = "AdSettings", menuName = "ArrowPopBall/Ads/Ad Settings")]
    public class AdSettingsSO : ScriptableObject
    {
        [Header("Banner")]
        [SerializeField] private bool _showLobbyBanner = true;
        [SerializeField] private bool _showIngameBanner = true;

        [Header("Interstitial")]
        [SerializeField] private int _interstitialStartLevel = 3;
        [SerializeField] private int _interstitialEveryClears = 2;
        [SerializeField] private float _interstitialCooldownSeconds = 90f;

        [Header("Editor Simulation")]
        [SerializeField] private bool _simulateInterstitialInEditor = true;
        [SerializeField] private bool _simulateRewardedSuccessInEditor = true;
        [SerializeField] private float _simulatedAdDuration = 0.5f;

        public bool ShowLobbyBanner => _showLobbyBanner;
        public bool ShowIngameBanner => _showIngameBanner;
        public int InterstitialStartLevel => Mathf.Max(1, _interstitialStartLevel);
        public int InterstitialEveryClears => Mathf.Max(1, _interstitialEveryClears);
        public float InterstitialCooldownSeconds => Mathf.Max(0f, _interstitialCooldownSeconds);
        public bool SimulateInterstitialInEditor => _simulateInterstitialInEditor;
        public bool SimulateRewardedSuccessInEditor => _simulateRewardedSuccessInEditor;
        public float SimulatedAdDuration => Mathf.Max(0f, _simulatedAdDuration);
    }
}
