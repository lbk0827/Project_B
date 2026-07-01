using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Game.Core;
using Game.Arrow;
using Game.Data;

namespace Game.UI
{
    /// <summary>
    /// Arrow Dash UI 컨트롤러
    /// 방향 선택 버튼 + 보유 개수 표시
    /// 애니메이션은 ArrowDashButtonAnimator에 위임
    /// </summary>
    public class ArrowDashUI : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("참조")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private ArrowDashButtonAnimator _buttonAnimator;

        [Header("하단 패널 - 방향 버튼")]
        [SerializeField] private Button _northButton;  // ↑ (Up)
        [SerializeField] private Button _southButton;  // ↓ (Down)
        [SerializeField] private Button _eastButton;   // → (Right)
        [SerializeField] private Button _westButton;   // ← (Left)

        [Header("방향별 화살표 개수 표시 (선택)")]
        [SerializeField] private TextMeshProUGUI _northCountText;
        [SerializeField] private TextMeshProUGUI _southCountText;
        [SerializeField] private TextMeshProUGUI _eastCountText;
        [SerializeField] private TextMeshProUGUI _westCountText;

        [Header("정보 표시")]
        [SerializeField] private TextMeshProUGUI _itemCountText;
        [SerializeField] private TextMeshProUGUI _messageText;

        [Header("애니메이션")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeDuration = 0.2f;
        [SerializeField] private float _messageDuration = 1.5f;

        // ========== 내부 상태 변수 ==========
        private bool _isShowing;
        private Tween _messageTween;

        // ========== 프로퍼티 ==========
        /// <summary>
        /// 애니메이션 진행 중 여부 (펼침/접힘)
        /// </summary>
        public bool IsAnimating => _buttonAnimator != null && _buttonAnimator.IsAnimating;

        // ========== 이벤트 ==========
        /// <summary>
        /// UI 닫힘 시 발생
        /// </summary>
        public event Action OnClosed;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            SetupButtons();

            // 초기 상태: 숨김
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            // ArrowDashManager 이벤트 구독
            if (ArrowDashManager.Instance != null)
            {
                ArrowDashManager.Instance.OnItemCountChanged += OnItemCountChanged;
                ArrowDashManager.Instance.OnUIStateChanged += OnUIStateChanged;
            }
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            if (ArrowDashManager.Instance != null)
            {
                ArrowDashManager.Instance.OnItemCountChanged -= OnItemCountChanged;
                ArrowDashManager.Instance.OnUIStateChanged -= OnUIStateChanged;
            }

            // 메시지 트윈 정리
            _messageTween?.Kill();
        }

        // ========== 초기화 ==========
        private void SetupButtons()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnCloseClicked);

            if (_northButton != null)
                _northButton.onClick.AddListener(() => OnDirectionClicked(ArrowDirection.Up));

            if (_southButton != null)
                _southButton.onClick.AddListener(() => OnDirectionClicked(ArrowDirection.Down));

            if (_eastButton != null)
                _eastButton.onClick.AddListener(() => OnDirectionClicked(ArrowDirection.Right));

            if (_westButton != null)
                _westButton.onClick.AddListener(() => OnDirectionClicked(ArrowDirection.Left));
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// UI 표시
        /// </summary>
        public void Show()
        {
            if (_isShowing)
                return;

            _isShowing = true;
            gameObject.SetActive(true);

            UpdateItemCount();
            UpdateDirectionCounts();
            HideMessage();

            // 버튼 펼침 + TopPanel 슬라이드 애니메이션
            _buttonAnimator?.PlayExpand();

            // 페이드 인
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.DOFade(1f, _fadeDuration).SetEase(Ease.OutCubic);
            }

            Debug.Log("[ArrowDashUI] Show");
        }

        /// <summary>
        /// UI 숨김
        /// </summary>
        public void Hide()
        {
            if (!_isShowing)
                return;

            _isShowing = false;

            // 버튼 접힘 + TopPanel 슬라이드 업 애니메이션
            float collapseAnimDuration = _buttonAnimator != null ? _buttonAnimator.PlayCollapse() : 0f;

            // 페이드 아웃 (버튼 애니메이션 완료 후)
            if (_canvasGroup != null)
            {
                _canvasGroup.DOFade(0f, _fadeDuration)
                    .SetDelay(collapseAnimDuration)
                    .SetEase(Ease.InCubic)
                    .OnComplete(() =>
                    {
                        _buttonAnimator?.ResetTopPanel();
                        gameObject.SetActive(false);
                        OnClosed?.Invoke();
                    });
            }
            else
            {
                DOVirtual.DelayedCall(collapseAnimDuration, () =>
                {
                    _buttonAnimator?.ResetTopPanel();
                    gameObject.SetActive(false);
                    OnClosed?.Invoke();
                });
            }

            Debug.Log("[ArrowDashUI] Hide");
        }

        /// <summary>
        /// "해당 방향에 화살표가 없습니다" 안내 메시지 표시
        /// </summary>
        public void ShowNoArrowMessage()
        {
            ShowMessage("해당 방향에 화살표가 없습니다");
        }

        /// <summary>
        /// 안내 메시지 표시
        /// </summary>
        public void ShowMessage(string message)
        {
            if (_messageText == null)
                return;

            _messageTween?.Kill();

            _messageText.text = message;
            _messageText.gameObject.SetActive(true);

            // 페이드 인 → 유지 → 페이드 아웃
            _messageText.alpha = 0f;
            _messageTween = DOTween.Sequence()
                .Append(_messageText.DOFade(1f, 0.2f))
                .AppendInterval(_messageDuration)
                .Append(_messageText.DOFade(0f, 0.2f))
                .OnComplete(() => _messageText.gameObject.SetActive(false));
        }

        /// <summary>
        /// 메시지 숨김
        /// </summary>
        public void HideMessage()
        {
            _messageTween?.Kill();

            if (_messageText != null)
            {
                _messageText.alpha = 0f;
                _messageText.gameObject.SetActive(false);
            }
        }

        // ========== 내부 유틸리티 ==========

        private void UpdateItemCount()
        {
            if (_itemCountText != null)
            {
                int count = ArrowDashData.GetCount();
                _itemCountText.text = $"보유: {count}개";
            }
        }

        private void UpdateDirectionCounts()
        {
            if (ArrowDashManager.Instance == null)
                return;

            UpdateDirectionCount(_northCountText, ArrowDirection.Up);
            UpdateDirectionCount(_southCountText, ArrowDirection.Down);
            UpdateDirectionCount(_eastCountText, ArrowDirection.Right);
            UpdateDirectionCount(_westCountText, ArrowDirection.Left);
        }

        private void UpdateDirectionCount(TextMeshProUGUI text, ArrowDirection direction)
        {
            if (text == null)
                return;

            int count = ArrowDashManager.Instance.GetArrowCountByDirection(direction);
            text.text = count.ToString();

            // 0개면 비활성 색상
            text.color = count > 0 ? Color.white : new Color(1f, 1f, 1f, 0.5f);
        }

        private void OnItemCountChanged(int newCount)
        {
            UpdateItemCount();
        }

        private void OnUIStateChanged(bool isOpen)
        {
            // Manager에서 UI 닫기 요청 시 Hide() 호출
            if (!isOpen && _isShowing)
            {
                Hide();
            }
        }

        // ========== 버튼 콜백 ==========

        private void OnCloseClicked()
        {
            if (ArrowDashManager.Instance != null)
            {
                ArrowDashManager.Instance.CloseUI();
            }
            else
            {
                Hide();
            }
        }

        private void OnDirectionClicked(ArrowDirection direction)
        {
            if (ArrowDashManager.Instance == null)
                return;

            // 해당 방향에 화살표가 있는지 확인
            int count = ArrowDashManager.Instance.GetArrowCountByDirection(direction);

            if (count == 0)
            {
                ShowNoArrowMessage();
                return;
            }

            // Dash 실행
            bool success = ArrowDashManager.Instance.ExecuteDash(direction);

            if (!success)
            {
                ShowMessage("아이템이 부족합니다");
            }
        }
    }
}
