using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 실패 팝업 UI 컨트롤러
    /// Life가 0이 되었을 때 표시
    /// </summary>
    public class PopupFailUI : MonoBehaviour
    {
        // ========== 싱글톤 ==========
        private static PopupFailUI _instance;
        public static PopupFailUI Instance => _instance;

        // ========== 인스펙터 노출 변수 ==========
        [Header("필수 참조")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Image _background;

        [Header("버튼")]
        [SerializeField] private Button _btnClose;
        [SerializeField] private Button _btnRestart;
        [SerializeField] private Button _btnPlayOn;

        [Header("씬 설정")]
        [SerializeField] private string _lobbySceneName = "LobbyScene";

        [Header("애니메이션 설정")]
        [SerializeField] private float _showDuration = 0.3f;
        [SerializeField] private float _hideDuration = 0.2f;
        [SerializeField] private float _backgroundAlpha = 0.7f;

        // ========== 내부 상태 변수 ==========
        private bool _isShowing;
        private bool _isAnimating;
        private Sequence _currentSequence;

        // ========== 프로퍼티 ==========
        public bool IsShowing => _isShowing;
        public bool IsAnimating => _isAnimating;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // 초기 상태: 숨김
            HideImmediate();

            // 버튼 이벤트 연결
            if (_btnClose != null)
            {
                _btnClose.onClick.AddListener(OnCloseClicked);
            }

            if (_btnRestart != null)
            {
                _btnRestart.onClick.AddListener(OnRestartClicked);
            }

            if (_btnPlayOn != null)
            {
                _btnPlayOn.onClick.AddListener(OnPlayOnClicked);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            _currentSequence?.Kill();
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 팝업 표시
        /// </summary>
        public void Show(Action onComplete = null)
        {
            if (_isShowing || _isAnimating)
                return;

            _isShowing = true;
            _isAnimating = true;

            gameObject.SetActive(true);

            _currentSequence?.Kill();
            _currentSequence = DOTween.Sequence();

            // 초기 상태
            _canvasGroup.alpha = 0f;
            _panel.localScale = Vector3.zero;
            if (_background != null)
            {
                _background.color = new Color(_background.color.r, _background.color.g, _background.color.b, 0f);
            }

            // 애니메이션
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

        /// <summary>
        /// 팝업 숨김 (애니메이션)
        /// </summary>
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

        /// <summary>
        /// 즉시 숨김 (애니메이션 없음)
        /// </summary>
        public void HideImmediate()
        {
            _currentSequence?.Kill();

            _isShowing = false;
            _isAnimating = false;

            _canvasGroup.alpha = 0f;
            _panel.localScale = Vector3.zero;

            if (_background != null)
            {
                _background.color = new Color(_background.color.r, _background.color.g, _background.color.b, 0f);
            }

            gameObject.SetActive(false);
        }

        // ========== 버튼 이벤트 ==========

        private void OnCloseClicked()
        {
            if (_isAnimating)
                return;

            Debug.Log("[PopupFailUI] Close clicked - returning to lobby");

            Hide(() =>
            {
                // 로비 씬으로 이동
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
                // GameManager에 레벨 재시작 요청
                if (Game.Core.GameManager.Instance != null)
                {
                    Game.Core.GameManager.Instance.RestartLevel();
                }
            });
        }

        private void OnPlayOnClicked()
        {
            if (_isAnimating)
                return;

            Debug.Log("[PopupFailUI] PlayOn clicked");

            // MVP: 광고 없이 바로 처리
            // TODO: AdManager 연동 시 보상형 광고 재생 추가
            Hide(() =>
            {
                // GameManager에 Life 풀 회복 후 계속 요청
                if (Game.Core.GameManager.Instance != null)
                {
                    Game.Core.GameManager.Instance.ContinueWithFullLife();
                }
            });
        }
    }
}