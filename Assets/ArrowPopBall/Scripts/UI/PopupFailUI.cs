using System;
using DG.Tweening;
using Game.Ads;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Fail popup UI controller shown when lives reach zero.
    /// </summary>
    public class PopupFailUI : MonoBehaviour
    {
        private static PopupFailUI _instance;
        public static PopupFailUI Instance => _instance;

        [Header("Required References")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Image _background;

        [Header("Buttons")]
        [SerializeField] private Button _btnClose;
        [SerializeField] private Button _btnRestart;
        [SerializeField] private Button _btnPlayOn;

        [Header("Scene Settings")]
        [SerializeField] private string _lobbySceneName = "LobbyScene";

        [Header("Animation Settings")]
        [SerializeField] private float _showDuration = 0.3f;
        [SerializeField] private float _hideDuration = 0.2f;
        [SerializeField] private float _backgroundAlpha = 0.7f;

        private bool _isShowing;
        private bool _isAnimating;
        private Sequence _currentSequence;

        public bool IsShowing => _isShowing;
        public bool IsAnimating => _isAnimating;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            HideImmediate();

            if (_btnClose != null)
                _btnClose.onClick.AddListener(OnCloseClicked);

            if (_btnRestart != null)
                _btnRestart.onClick.AddListener(OnRestartClicked);

            if (_btnPlayOn != null)
                _btnPlayOn.onClick.AddListener(OnPlayOnClicked);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            _currentSequence?.Kill();
        }

        public void Show(Action onComplete = null)
        {
            if (_isShowing || _isAnimating)
                return;

            _isShowing = true;
            _isAnimating = true;

            gameObject.SetActive(true);

            _currentSequence?.Kill();
            _currentSequence = DOTween.Sequence();

            _canvasGroup.alpha = 0f;
            _panel.localScale = Vector3.zero;

            if (_background != null)
                _background.color = new Color(_background.color.r, _background.color.g, _background.color.b, 0f);

            _currentSequence
                .Append(_background.DOFade(_backgroundAlpha, _showDuration * 0.6f))
                .Join(_panel.DOScale(1f, _showDuration).SetEase(Ease.OutBack))
                .Join(_canvasGroup.DOFade(1f, _showDuration * 0.5f))
                .OnComplete(() =>
                {
                    _isAnimating = false;
                    onComplete?.Invoke();
                });

            Debug.Log("[PopupFailUI] Show");
        }

        public void Hide(Action onComplete = null)
        {
            if (!_isShowing || _isAnimating)
                return;

            _isAnimating = true;

            _currentSequence?.Kill();
            _currentSequence = DOTween.Sequence();

            _currentSequence
                .Append(_panel.DOScale(0.9f, _hideDuration).SetEase(Ease.InBack))
                .Join(_canvasGroup.DOFade(0f, _hideDuration))
                .Join(_background.DOFade(0f, _hideDuration))
                .OnComplete(() =>
                {
                    _isShowing = false;
                    _isAnimating = false;
                    gameObject.SetActive(false);
                    onComplete?.Invoke();
                });

            Debug.Log("[PopupFailUI] Hide");
        }

        public void HideImmediate()
        {
            _currentSequence?.Kill();

            _isShowing = false;
            _isAnimating = false;

            _canvasGroup.alpha = 0f;
            _panel.localScale = Vector3.zero;

            if (_background != null)
                _background.color = new Color(_background.color.r, _background.color.g, _background.color.b, 0f);

            gameObject.SetActive(false);
        }

        private void OnCloseClicked()
        {
            if (_isAnimating)
                return;

            Debug.Log("[PopupFailUI] Close clicked - returning to lobby");

            Hide(() =>
            {
                SceneManager.LoadScene(_lobbySceneName);
            });
        }

        private void OnRestartClicked()
        {
            if (_isAnimating)
                return;

            Debug.Log("[PopupFailUI] Restart clicked");

            Hide(() =>
            {
                if (Game.Core.GameManager.Instance != null)
                    Game.Core.GameManager.Instance.RestartLevel();
            });
        }

        private void OnPlayOnClicked()
        {
            if (_isAnimating)
                return;

            Debug.Log("[PopupFailUI] PlayOn clicked");

            if (AdManager.Instance == null)
            {
                Debug.LogWarning("[PopupFailUI] AdManager not found");
                return;
            }

            AdManager.Instance.ShowRewarded(AdTrigger.FailContinue, rewarded =>
            {
                if (!rewarded)
                {
                    Debug.Log("[PopupFailUI] Rewarded ad was not completed");
                    return;
                }

                Hide(() =>
                {
                    if (Game.Core.GameManager.Instance != null)
                        Game.Core.GameManager.Instance.ContinueWithFullLife();
                });
            });
        }
    }
}
