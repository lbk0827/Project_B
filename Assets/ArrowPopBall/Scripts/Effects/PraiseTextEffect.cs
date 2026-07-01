using UnityEngine;
using TMPro;
using DG.Tweening;

namespace Game.Effects
{
    /// <summary>
    /// 칭찬 텍스트 이펙트
    /// 레벨 클리어 시 랜덤 칭찬 문구를 화면 중앙에 표시
    /// </summary>
    public class PraiseTextEffect : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("UI Reference")]
        [SerializeField] private TextMeshProUGUI _praiseText;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Animation Settings")]
        [SerializeField] private float _appearDuration = 0.3f;
        [SerializeField] private float _punchScale = 1.2f;
        [SerializeField] private Ease _appearEase = Ease.OutBack;
        [SerializeField] private float _fadeOutDuration = 0.5f;
        [SerializeField] private bool _autoHide = false;  // false: 외부에서 FadeOut() 호출 시까지 유지
        [SerializeField] private float _autoHideDelay = 1.5f;  // autoHide가 true일 때만 사용

        [Header("Praise Words")]
        [SerializeField] private string[] _praiseWords = new string[]
        {
            "Amazing!",
            "Perfect!",
            "Excellent!",
            "Awesome!",
            "Great!",
            "Wonderful!",
            "Fantastic!",
            "Brilliant!",
            "Super!",
            "Nice!"
        };

        [Header("Visual Settings")]
        [SerializeField] private Color _textColor = new Color(1f, 0.84f, 0f); // Golden
        [SerializeField] private bool _useGradient = true;
        [SerializeField] private Color _gradientTopColor = new Color(1f, 0.92f, 0.23f);
        [SerializeField] private Color _gradientBottomColor = new Color(1f, 0.65f, 0f);

        // ========== 내부 상태 변수 ==========
        private Sequence _sequence;
        private bool _isShowing;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            // 자동 참조 (같은 오브젝트에 있을 경우)
            if (_praiseText == null)
            {
                _praiseText = GetComponent<TextMeshProUGUI>();
            }

            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }

            // 영어 칭찬 문구 강제 설정 (Inspector 값 덮어쓰기)
            _praiseWords = new string[]
            {
                "Amazing!",
                "Perfect!",
                "Excellent!",
                "Awesome!",
                "Great!",
                "Wonderful!",
                "Fantastic!",
                "Brilliant!",
                "Super!",
                "Nice!"
            };

            // 초기 상태: 숨김 (게임오브젝트는 활성화 상태 유지, alpha와 scale로 숨김)
            HideImmediate();
        }

        /// <summary>
        /// 즉시 숨김 (애니메이션 없이)
        /// </summary>
        private void HideImmediate()
        {
            if (_praiseText != null)
            {
                _praiseText.transform.localScale = Vector3.zero;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }

            _isShowing = false;
        }

        private void OnDestroy()
        {
            _sequence?.Kill();
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 칭찬 텍스트 표시
        /// </summary>
        public void Show()
        {
            if (_praiseText == null)
            {
                Debug.LogWarning("[PraiseTextEffect] PraiseText not assigned!");
                return;
            }

            Hide(); // 기존 애니메이션 정리

            _isShowing = true;

            // 랜덤 칭찬 문구 선택
            string praise = GetRandomPraise();
            _praiseText.text = praise;

            // 색상 설정
            ApplyColors();

            // 표시
            _praiseText.gameObject.SetActive(true);

            Debug.Log($"[PraiseTextEffect] Showing: {praise}");

            // 애니메이션 시퀀스 - 등장만
            _sequence = DOTween.Sequence();

            // 1. 등장 (Scale 0 → punchScale → 1)
            _praiseText.transform.localScale = Vector3.zero;
            _sequence.Append(_praiseText.transform.DOScale(_punchScale, _appearDuration * 0.6f).SetEase(_appearEase));
            _sequence.Append(_praiseText.transform.DOScale(1f, _appearDuration * 0.4f).SetEase(Ease.OutQuad));

            // CanvasGroup 페이드 인
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _sequence.Join(_canvasGroup.DOFade(1f, _appearDuration).SetEase(Ease.OutQuad));
            }

            // autoHide가 true일 때만 자동으로 사라짐
            if (_autoHide)
            {
                _sequence.AppendInterval(_autoHideDelay);
                _sequence.AppendCallback(() => FadeOut());
            }
        }

        /// <summary>
        /// 페이드 아웃 애니메이션으로 사라짐 (외부에서 호출)
        /// </summary>
        public void FadeOut()
        {
            if (!_isShowing || _praiseText == null)
                return;

            Debug.Log("[PraiseTextEffect] FadeOut called");

            // 기존 시퀀스 정리
            _sequence?.Kill();

            // 페이드 아웃 시퀀스
            _sequence = DOTween.Sequence();

            // 살짝 커졌다가 사라지는 연출
            _sequence.Append(_praiseText.transform.DOScale(1.1f, _fadeOutDuration * 0.3f));
            if (_canvasGroup != null)
            {
                _sequence.Join(_canvasGroup.DOFade(0f, _fadeOutDuration));
            }
            _sequence.Append(_praiseText.transform.DOScale(0f, _fadeOutDuration * 0.7f).SetEase(Ease.InBack));

            _sequence.OnComplete(() =>
            {
                _praiseText.gameObject.SetActive(false);
                _isShowing = false;
            });
        }

        /// <summary>
        /// 즉시 숨김
        /// </summary>
        public void Hide()
        {
            _sequence?.Kill();
            _sequence = null;

            if (_praiseText != null)
            {
                _praiseText.gameObject.SetActive(false);
                _praiseText.transform.localScale = Vector3.zero;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }

            _isShowing = false;
        }

        /// <summary>
        /// 표시 중인지 확인
        /// </summary>
        public bool IsShowing => _isShowing;

        /// <summary>
        /// 페이드 아웃 지속 시간
        /// </summary>
        public float FadeOutDuration => _fadeOutDuration;

        // ========== 내부 유틸리티 ==========

        private string GetRandomPraise()
        {
            if (_praiseWords == null || _praiseWords.Length == 0)
            {
                return "Great!";
            }

            int index = Random.Range(0, _praiseWords.Length);
            return _praiseWords[index];
        }

        private void ApplyColors()
        {
            if (_praiseText == null) return;

            if (_useGradient)
            {
                // 그라디언트 색상 적용
                _praiseText.enableVertexGradient = true;
                _praiseText.colorGradient = new VertexGradient(
                    _gradientTopColor,    // Top Left
                    _gradientTopColor,    // Top Right
                    _gradientBottomColor, // Bottom Left
                    _gradientBottomColor  // Bottom Right
                );
            }
            else
            {
                // 단색 적용
                _praiseText.enableVertexGradient = false;
                _praiseText.color = _textColor;
            }
        }

        /// <summary>
        /// 외부에서 칭찬 문구 목록 설정
        /// </summary>
        public void SetPraiseWords(string[] words)
        {
            _praiseWords = words;
        }
    }
}