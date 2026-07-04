using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Ads
{
    public enum AdUnitSlot
    {
        Banner,
        Interstitial,
        Rewarded
    }

    [Serializable]
    public class AdUnitPlatformIds
    {
        [SerializeField] private AdUnitSlot _slot;
        [SerializeField] private string _androidUnitId;
        [SerializeField] private string _iosUnitId;

        public AdUnitSlot Slot => _slot;
        public string AndroidUnitId => _androidUnitId;
        public string IosUnitId => _iosUnitId;

        public string GetUnitId(RuntimePlatform platform)
        {
            return platform switch
            {
                RuntimePlatform.IPhonePlayer => _iosUnitId,
                RuntimePlatform.Android => _androidUnitId,
                _ => string.IsNullOrWhiteSpace(_androidUnitId) ? _iosUnitId : _androidUnitId
            };
        }
    }

    [CreateAssetMenu(fileName = "AdsSdkSettings", menuName = "ArrowPopBall/Ads/Ads SDK Settings")]
    public class AdsSdkSettingsSO : ScriptableObject
    {
        [Header("AppLovin MAX")]
        [SerializeField] private string _appLovinSdkKey;

        [Header("Optional Mediation IDs")]
        [SerializeField] private string _adMobAndroidAppId;
        [SerializeField] private string _adMobIosAppId;

        [Header("Ad Unit IDs")]
        [SerializeField] private List<AdUnitPlatformIds> _adUnits = new List<AdUnitPlatformIds>();

        public string AppLovinSdkKey => _appLovinSdkKey;
        public string AdMobAndroidAppId => _adMobAndroidAppId;
        public string AdMobIosAppId => _adMobIosAppId;
        public IReadOnlyList<AdUnitPlatformIds> AdUnits => _adUnits;
        public bool HasSdkKey => !string.IsNullOrWhiteSpace(_appLovinSdkKey);

        public string GetUnitId(AdUnitSlot slot, RuntimePlatform platform)
        {
            if (_adUnits == null)
                return string.Empty;

            foreach (var adUnit in _adUnits)
            {
                if (adUnit != null && adUnit.Slot == slot)
                    return adUnit.GetUnitId(platform);
            }

            return string.Empty;
        }

        public bool HasUnitId(AdUnitSlot slot)
        {
            if (_adUnits == null)
                return false;

            foreach (var adUnit in _adUnits)
            {
                if (adUnit == null || adUnit.Slot != slot)
                    continue;

                if (!string.IsNullOrWhiteSpace(adUnit.AndroidUnitId) || !string.IsNullOrWhiteSpace(adUnit.IosUnitId))
                    return true;
            }

            return false;
        }
    }
}
