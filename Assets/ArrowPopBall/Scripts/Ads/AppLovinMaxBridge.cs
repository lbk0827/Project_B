using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace Game.Ads
{
    public sealed class AppLovinMaxBridge
    {
        private readonly Type _maxSdkType;
        private readonly Type _maxSdkCallbacksType;
        private readonly Dictionary<string, Action<bool>> _pendingInterstitialCallbacks = new();
        private readonly Dictionary<string, Action<bool>> _pendingRewardedCallbacks = new();
        private readonly Dictionary<string, string> _pendingInterstitialPlacements = new();
        private readonly Dictionary<string, string> _pendingRewardedPlacements = new();
        private readonly HashSet<string> _bannerCreatedUnits = new();
        private readonly HashSet<string> _pendingBannerUnits = new();
        private readonly HashSet<string> _rewardedEarnedUnits = new();

        private bool _isInitialized;
        private bool _isInitializing;
        private bool _callbacksRegistered;

        public bool IsAvailable => _maxSdkType != null && _maxSdkCallbacksType != null;
        public bool IsInitialized => _isInitialized;

        public AppLovinMaxBridge()
        {
            _maxSdkType = FindType("MaxSdk");
            _maxSdkCallbacksType = FindType("MaxSdkCallbacks");
        }

        public void Initialize(AdsSdkSettingsSO sdkSettings)
        {
            if (!IsAvailable)
                return;

            if (_isInitialized || _isInitializing)
                return;

            if (sdkSettings == null || !sdkSettings.HasSdkKey)
            {
                Debug.LogWarning("[AppLovinMaxBridge] SDK key is missing. Initialization skipped.");
                return;
            }

            RegisterCallbacks();
            _isInitializing = true;

            TryInvokeVoid("SetVerboseLogging", Application.isEditor || Debug.isDebugBuild);
            TryInvokeVoid("InitializeSdk");
            Debug.Log("[AppLovinMaxBridge] InitializeSdk requested");
        }

        public bool ShowBanner(string unitId)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(unitId))
                return false;

            if (!_isInitialized)
            {
                _pendingBannerUnits.Add(unitId);
                return true;
            }

            if (!_bannerCreatedUnits.Contains(unitId))
            {
                CreateBanner(unitId);
            }

            TryInvokeVoid("ShowBanner", unitId);
            return true;
        }

        public void HideBanner(string unitId)
        {
            if (!IsReady(unitId) || string.IsNullOrWhiteSpace(unitId))
                return;

            TryInvokeVoid("HideBanner", unitId);
        }

        public void ShowInterstitial(string unitId, string placement, Action<bool> onClosed)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(unitId))
            {
                onClosed?.Invoke(false);
                return;
            }

            _pendingInterstitialCallbacks[unitId] = onClosed;
            _pendingInterstitialPlacements[unitId] = placement;

            if (!_isInitialized)
                return;

            if (TryInvokeBool("IsInterstitialReady", unitId))
            {
                ShowInterstitialInternal(unitId, placement);
                return;
            }

            TryInvokeVoid("LoadInterstitial", unitId);
        }

        public void ShowRewarded(string unitId, string placement, Action<bool> onClosed)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(unitId))
            {
                onClosed?.Invoke(false);
                return;
            }

            _pendingRewardedCallbacks[unitId] = onClosed;
            _pendingRewardedPlacements[unitId] = placement;
            _rewardedEarnedUnits.Remove(unitId);

            if (!_isInitialized)
                return;

            if (TryInvokeBool("IsRewardedAdReady", unitId))
            {
                ShowRewardedInternal(unitId, placement);
                return;
            }

            TryInvokeVoid("LoadRewardedAd", unitId);
        }

        private bool IsReady(string unitId)
        {
            return IsAvailable && _isInitialized && !string.IsNullOrWhiteSpace(unitId);
        }

        private void CreateBanner(string unitId)
        {
            try
            {
                var positionType = _maxSdkType.GetNestedType("AdViewPosition", BindingFlags.Public);
                var configType = _maxSdkType.GetNestedType("AdViewConfiguration", BindingFlags.Public);
                if (positionType == null || configType == null)
                {
                    Debug.LogWarning("[AppLovinMaxBridge] Banner configuration types not found.");
                    return;
                }

                object bottomCenter = Enum.Parse(positionType, "BottomCenter");
                object config = Activator.CreateInstance(configType, bottomCenter);
                InvokeMethod("CreateBanner", unitId, config);
                InvokeMethod("SetBannerBackgroundColor", unitId, Color.clear);
                _bannerCreatedUnits.Add(unitId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AppLovinMaxBridge] Failed to create banner: {ex.Message}");
            }
        }

        private void RegisterCallbacks()
        {
            if (_callbacksRegistered)
                return;

            RegisterEvent("OnSdkInitializedEvent", nameof(OnSdkInitialized));

            RegisterNestedEvent("Interstitial", "OnAdLoadedEvent", nameof(OnInterstitialLoaded));
            RegisterNestedEvent("Interstitial", "OnAdLoadFailedEvent", nameof(OnInterstitialLoadFailed));
            RegisterNestedEvent("Interstitial", "OnAdDisplayFailedEvent", nameof(OnInterstitialDisplayFailed));
            RegisterNestedEvent("Interstitial", "OnAdHiddenEvent", nameof(OnInterstitialHidden));

            RegisterNestedEvent("Rewarded", "OnAdLoadedEvent", nameof(OnRewardedLoaded));
            RegisterNestedEvent("Rewarded", "OnAdLoadFailedEvent", nameof(OnRewardedLoadFailed));
            RegisterNestedEvent("Rewarded", "OnAdDisplayFailedEvent", nameof(OnRewardedDisplayFailed));
            RegisterNestedEvent("Rewarded", "OnAdHiddenEvent", nameof(OnRewardedHidden));
            RegisterNestedEvent("Rewarded", "OnAdReceivedRewardEvent", nameof(OnRewardedReceivedReward));

            RegisterNestedEvent("Banner", "OnAdLoadedEvent", nameof(OnBannerLoaded));
            RegisterNestedEvent("Banner", "OnAdLoadFailedEvent", nameof(OnBannerLoadFailed));

            _callbacksRegistered = true;
        }

        private void RegisterNestedEvent(string nestedTypeName, string eventName, string handlerMethodName)
        {
            var nestedType = _maxSdkCallbacksType?.GetNestedType(nestedTypeName, BindingFlags.Public);
            var eventInfo = nestedType?.GetEvent(eventName, BindingFlags.Public | BindingFlags.Static);
            if (eventInfo == null)
                return;

            var handler = CreateCompatibleDelegate(eventInfo.EventHandlerType, handlerMethodName);
            eventInfo.AddEventHandler(null, handler);
        }

        private void RegisterEvent(string eventName, string handlerMethodName)
        {
            var eventInfo = _maxSdkCallbacksType?.GetEvent(eventName, BindingFlags.Public | BindingFlags.Static);
            if (eventInfo == null)
                return;

            var handler = CreateCompatibleDelegate(eventInfo.EventHandlerType, handlerMethodName);
            eventInfo.AddEventHandler(null, handler);
        }

        private Delegate CreateCompatibleDelegate(Type eventHandlerType, string methodName)
        {
            var invokeMethod = eventHandlerType.GetMethod("Invoke");
            var delegateParameters = invokeMethod.GetParameters()
                .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name))
                .ToArray();

            var targetMethod = GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            var targetParameters = targetMethod.GetParameters();
            var convertedParameters = new Expression[targetParameters.Length];
            for (int i = 0; i < targetParameters.Length; i++)
            {
                convertedParameters[i] = Expression.Convert(delegateParameters[i], targetParameters[i].ParameterType);
            }

            var call = Expression.Call(Expression.Constant(this), targetMethod, convertedParameters);
            return Expression.Lambda(eventHandlerType, call, delegateParameters).Compile();
        }

        private void OnSdkInitialized(object sdkConfiguration)
        {
            _isInitializing = false;
            _isInitialized = true;
            Debug.Log("[AppLovinMaxBridge] SDK initialized");

            foreach (var unitId in _pendingBannerUnits.ToArray())
            {
                ShowBanner(unitId);
            }
            _pendingBannerUnits.Clear();

            foreach (var unitId in _pendingInterstitialCallbacks.Keys.ToArray())
            {
                ShowInterstitial(unitId, _pendingInterstitialPlacements.TryGetValue(unitId, out var placement) ? placement : null, _pendingInterstitialCallbacks[unitId]);
            }

            foreach (var unitId in _pendingRewardedCallbacks.Keys.ToArray())
            {
                ShowRewarded(unitId, _pendingRewardedPlacements.TryGetValue(unitId, out var placement) ? placement : null, _pendingRewardedCallbacks[unitId]);
            }
        }

        private void OnInterstitialLoaded(string adUnitId, object adInfo)
        {
            if (_pendingInterstitialCallbacks.ContainsKey(adUnitId))
                ShowInterstitialInternal(adUnitId, _pendingInterstitialPlacements.TryGetValue(adUnitId, out var placement) ? placement : null);
        }

        private void OnInterstitialLoadFailed(string adUnitId, object errorInfo)
        {
            ResolvePending(_pendingInterstitialCallbacks, adUnitId, false);
        }

        private void OnInterstitialDisplayFailed(string adUnitId, object errorInfo, object adInfo)
        {
            ResolvePending(_pendingInterstitialCallbacks, adUnitId, false);
        }

        private void OnInterstitialHidden(string adUnitId, object adInfo)
        {
            ResolvePending(_pendingInterstitialCallbacks, adUnitId, true);
            TryInvokeVoid("LoadInterstitial", adUnitId);
        }

        private void OnRewardedLoaded(string adUnitId, object adInfo)
        {
            if (_pendingRewardedCallbacks.ContainsKey(adUnitId))
                ShowRewardedInternal(adUnitId, _pendingRewardedPlacements.TryGetValue(adUnitId, out var placement) ? placement : null);
        }

        private void OnRewardedLoadFailed(string adUnitId, object errorInfo)
        {
            ResolvePending(_pendingRewardedCallbacks, adUnitId, false);
        }

        private void OnRewardedDisplayFailed(string adUnitId, object errorInfo, object adInfo)
        {
            ResolvePending(_pendingRewardedCallbacks, adUnitId, false);
        }

        private void OnRewardedHidden(string adUnitId, object adInfo)
        {
            bool rewarded = _rewardedEarnedUnits.Contains(adUnitId);
            _rewardedEarnedUnits.Remove(adUnitId);
            ResolvePending(_pendingRewardedCallbacks, adUnitId, rewarded);
            TryInvokeVoid("LoadRewardedAd", adUnitId);
        }

        private void OnRewardedReceivedReward(string adUnitId, object reward, object adInfo)
        {
            _rewardedEarnedUnits.Add(adUnitId);
        }

        private void OnBannerLoaded(string adUnitId, object adInfo)
        {
            Debug.Log($"[AppLovinMaxBridge] Banner loaded: {adUnitId}");
        }

        private void OnBannerLoadFailed(string adUnitId, object errorInfo)
        {
            Debug.LogWarning($"[AppLovinMaxBridge] Banner load failed: {adUnitId}");
        }

        private void ShowInterstitialInternal(string unitId, string placement)
        {
            if (!string.IsNullOrWhiteSpace(placement) && TryInvokeMethod("ShowInterstitial", out _, unitId, placement))
                return;

            InvokeMethod("ShowInterstitial", unitId);
        }

        private void ShowRewardedInternal(string unitId, string placement)
        {
            if (!string.IsNullOrWhiteSpace(placement) && TryInvokeMethod("ShowRewardedAd", out _, unitId, placement))
                return;

            InvokeMethod("ShowRewardedAd", unitId);
        }

        private void ResolvePending(Dictionary<string, Action<bool>> callbacks, string unitId, bool result)
        {
            if (!callbacks.TryGetValue(unitId, out var callback))
                return;

            callbacks.Remove(unitId);
            _pendingInterstitialPlacements.Remove(unitId);
            _pendingRewardedPlacements.Remove(unitId);
            callback?.Invoke(result);
        }

        private bool TryInvokeBool(string methodName, params object[] args)
        {
            object result = InvokeMethod(methodName, args);
            return result is bool value && value;
        }

        private void TryInvokeVoid(string methodName, params object[] args)
        {
            InvokeMethod(methodName, args);
        }

        private object InvokeMethod(string methodName, params object[] args)
        {
            TryInvokeMethod(methodName, out var result, args);
            return result;
        }

        private bool TryInvokeMethod(string methodName, out object result, params object[] args)
        {
            result = null;

            if (_maxSdkType == null)
                return false;

            var methods = _maxSdkType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name == methodName)
                .ToArray();

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length != args.Length)
                    continue;

                try
                {
                    result = method.Invoke(null, args);
                    return true;
                }
                catch
                {
                    // try next overload
                }
            }

            return false;
        }

        private static Type FindType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = null;
                try
                {
                    type = assembly.GetTypes().FirstOrDefault(candidate => candidate.Name == typeName);
                }
                catch (ReflectionTypeLoadException)
                {
                    // ignore broken assemblies and continue scanning
                }

                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
