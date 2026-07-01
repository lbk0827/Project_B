using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Data;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 개별 풍선 UI 요소
    /// </summary>
    public class BalloonUIElement : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("참조")]
        [SerializeField] private Image _balloonImage;
        [SerializeField] private Image _highlightImage;

        [Header("팝 애니메이션")]
        [SerializeField] private float _popDuration = 0.35f;
        [SerializeField] private float _punchScale = 0.4f;       // 펀치 스케일 강도
        [SerializeField] private float _shakeStrength = 10f;     // 흔들림 강도

        [Header("둥실둥실 흔들림 (토글)")]
        [SerializeField] private bool _enableFloatAnimation = true;
        [SerializeField] private float _floatSpeed = 1.5f;           // 흔들림 속도
        [SerializeField] private float _floatAmplitude = 3f;         // Y축 이동 범위 (픽셀)
        [SerializeField] private float _floatScaleAmount = 0.03f;    // 스케일 변화량 (0.03 = ±3%)
        [SerializeField] private float _floatRotationAmount = 3f;    // 회전 범위 (도)

        // ========== 내부 상태 변수 ==========
        private GameColor _color;
        private bool _isPopped;
        private RectTransform _rectTransform;
        private float _floatTimer;
        private float _floatOffset;  // 개별 풍선마다 다른 타이밍
        private Vector3 _originalScale;
        private Vector2 _basePosition;
        private float _pressureScale = 1f;
        private Sequence _popSequence;  // 팝 애니메이션 시퀀스
        private bool _isRearranging = false;  // 재배치 애니메이션 중 플래그

        // ========== 프로퍼티 ==========
        public GameColor Color => _color;
        public bool IsPopped => _isPopped;
        public RectTransform CachedRectTransform => _rectTransform;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();

            if (_balloonImage == null)
            {
                _balloonImage = GetComponent<Image>();
            }
        }

        private void OnDestroy()
        {
            _popSequence?.Kill();
        }

        private void Update()
        {
            // 재배치 애니메이션 중에는 둥실둥실 효과 일시 중단
            if (!_isPopped && _enableFloatAnimation && !_isRearranging)
            {
                _floatTimer += Time.deltaTime * _floatSpeed;
                float t = _floatTimer + _floatOffset;

                // Y축 둥실둥실 이동
                float yOffset = Mathf.Sin(t) * _floatAmplitude;
                _rectTransform.anchoredPosition = _basePosition + new Vector2(0, yOffset);

                // 스케일 변화 (숨쉬는 듯한 효과)
                float scaleOffset = Mathf.Sin(t * 1.3f) * _floatScaleAmount;
                transform.localScale = _originalScale * (1f + scaleOffset);

                // 미세한 회전 (좌우 흔들림)
                float rotation = Mathf.Sin(t * 0.7f) * _floatRotationAmount;
                transform.localRotation = Quaternion.Euler(0, 0, rotation);
            }
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 풍선 UI 초기화
        /// </summary>
        public void Initialize(GameColor color, float size)
        {
            _color = color;
            _isPopped = false;

            // 크기 설정
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();

            _rectTransform.sizeDelta = new Vector2(size, size);
            _originalScale = Vector3.one;
            transform.localScale = _originalScale;
            transform.localRotation = Quaternion.identity;

            // 둥실둥실 애니메이션용 초기화
            _floatOffset = Random.Range(0f, Mathf.PI * 2f);  // 풍선마다 다른 타이밍
            _floatTimer = 0f;

            // 색상 설정
            if (_balloonImage != null)
            {
                _balloonImage.color = GetColorValue(color);
            }

            // 하이라이트 숨김
            if (_highlightImage != null)
            {
                _highlightImage.gameObject.SetActive(false);
            }

            gameObject.name = $"Balloon_{color}";
        }

        /// <summary>
        /// 기본 위치 설정 (둥실둥실 애니메이션 기준점)
        /// </summary>
        public void SetBasePosition(Vector2 position)
        {
            _basePosition = position;
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = position;
            }
        }

        /// <summary>
        /// 재배치 애니메이션 시작 (둥실둥실 일시 중단)
        /// </summary>
        public void StartRearrangeAnimation()
        {
            _isRearranging = true;
        }

        /// <summary>
        /// 재배치 애니메이션 완료 (둥실둥실 재개)
        /// </summary>
        public void EndRearrangeAnimation()
        {
            _isRearranging = false;
        }

        /// <summary>
        /// 압력감 스케일 설정 (풍선 많을수록 작아짐)
        /// </summary>
        public void SetPressureScale(float scale)
        {
            _pressureScale = scale;
            _originalScale = Vector3.one * _pressureScale;
            transform.localScale = _originalScale;
        }

        /// <summary>
        /// 풍선 터뜨리기
        /// </summary>
        public void Pop()
        {
            Pop(null);
        }

        /// <summary>
        /// 풍선 터뜨리기 (콜백 포함) - DOTween 시퀀스 사용
        /// </summary>
        public void Pop(System.Action onComplete)
        {
            if (_isPopped)
                return;

            _isPopped = true;

            // 팝 사운드 재생
            SFXManager.Instance?.PlayBalloonPop();

            // 둥실둥실 애니메이션 중단
            _enableFloatAnimation = false;

            // 기존 시퀀스 정리
            _popSequence?.Kill();

            // DOTween 시퀀스로 팝 애니메이션
            _popSequence = DOTween.Sequence();

            // 1. 펀치 스케일 (빵! 터지는 느낌)
            _popSequence.Append(
                transform.DOPunchScale(Vector3.one * _punchScale, _popDuration * 0.4f, 1, 0.5f)
            );

            // 2. 흔들림 + 페이드 아웃 (동시 실행)
            _popSequence.Append(
                transform.DOShakePosition(_popDuration * 0.3f, _shakeStrength, 20, 90f, false, true)
            );
            _popSequence.Join(
                _balloonImage.DOFade(0f, _popDuration * 0.3f)
            );

            // 3. 축소하며 사라짐
            _popSequence.Append(
                transform.DOScale(0f, _popDuration * 0.3f).SetEase(Ease.InBack)
            );

            // 완료 콜백
            _popSequence.OnComplete(() =>
            {
                gameObject.SetActive(false);
                onComplete?.Invoke();
            });
        }

        /// <summary>
        /// 풍선 하이라이트 표시 (타겟팅 시)
        /// </summary>
        public void SetHighlight(bool active)
        {
            if (_highlightImage != null)
            {
                _highlightImage.gameObject.SetActive(active);
            }
        }

        // ========== 내부 유틸리티 ==========

        private UnityEngine.Color GetColorValue(GameColor gameColor)
        {
            switch (gameColor)
            {
                case GameColor.Red: return new UnityEngine.Color(1f, 0.2f, 0.2f);
                case GameColor.Blue: return new UnityEngine.Color(0.2f, 0.4f, 1f);
                case GameColor.Green: return new UnityEngine.Color(0.2f, 0.8f, 0.2f);
                case GameColor.Yellow: return new UnityEngine.Color(1f, 0.9f, 0.2f);
                case GameColor.Purple: return new UnityEngine.Color(0.6f, 0.2f, 0.8f);
                case GameColor.Orange: return new UnityEngine.Color(1f, 0.5f, 0.1f);
                case GameColor.Cyan: return new UnityEngine.Color(0.2f, 0.9f, 0.9f);
                case GameColor.Pink: return new UnityEngine.Color(1f, 0.6f, 0.7f);
                case GameColor.Brown: return new UnityEngine.Color(0.6f, 0.4f, 0.2f);
                case GameColor.Lime: return new UnityEngine.Color(0.6f, 1f, 0.2f);
                case GameColor.Navy: return new UnityEngine.Color(0.1f, 0.1f, 0.5f);
                case GameColor.Magenta: return new UnityEngine.Color(1f, 0.2f, 0.8f);
                default: return UnityEngine.Color.white;
            }
        }
    }
}