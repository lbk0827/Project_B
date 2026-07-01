using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Game.Arrow;
using Game.Data;

namespace Game.GuideLine
{
    /// <summary>
    /// UI Image 기반 가이드 라인 렌더러
    /// Canvas 내부에서 렌더링되어 Sorting Order로 정확하게 제어 가능
    /// </summary>
    public class GuideLineUIRenderer : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("UI 참조")]
        [SerializeField] private Image _lineImage;
        [SerializeField] private RectTransform _rectTransform;

        // ========== 내부 상태 변수 ==========
        private ArrowController _arrow;
        private Camera _camera;
        private Canvas _parentCanvas;
        private Tween _fadeTween;
        private Color _baseColor;
        private bool _isVisible;
        private float _lineWidth = 4f;

        // ========== 프로퍼티 ==========
        public ArrowController Arrow => _arrow;
        public bool IsVisible => _isVisible;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            if (_lineImage == null)
                _lineImage = GetComponent<Image>();
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();
        }

        private void OnDestroy()
        {
            _fadeTween?.Kill();
        }

        // ========== 공개 인터페이스 ==========
        /// <summary>
        /// 가이드 라인 초기화
        /// </summary>
        public void Initialize(ArrowController arrow, Color color, float width, Canvas parentCanvas, Camera camera)
        {
            Debug.Log($"[GuideLineUIRenderer] Initialize called - arrow:{arrow?.Id}, canvas:{parentCanvas?.name}, camera:{camera?.name}");

            _arrow = arrow;
            _baseColor = color;
            _lineWidth = width;
            _parentCanvas = parentCanvas;
            _camera = camera;
            _isVisible = false;

            if (_lineImage == null)
                _lineImage = GetComponent<Image>();
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();

            Debug.Log($"[GuideLineUIRenderer] Components - image:{_lineImage != null}, rect:{_rectTransform != null}");

            // Image 설정
            _lineImage.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0f);

            // RectTransform 앵커 설정 (중앙 기준)
            _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rectTransform.pivot = new Vector2(0f, 0.5f); // 왼쪽 중앙 피봇

            UpdateLine();
        }

        /// <summary>
        /// 라인 위치 업데이트 (Head → 화면 끝)
        /// </summary>
        public void UpdateLine()
        {
            if (_arrow == null || _rectTransform == null || _camera == null || _parentCanvas == null)
            {
                Debug.LogWarning($"[GuideLineUIRenderer] UpdateLine failed - arrow:{_arrow != null}, rect:{_rectTransform != null}, cam:{_camera != null}, canvas:{_parentCanvas != null}");
                return;
            }

            Vector2 headWorldPos = _arrow.GetHeadTipWorldPosition();
            ArrowDirection direction = _arrow.HeadDirection;

            // 월드 좌표 → 스크린 좌표 → Canvas 로컬 좌표
            Vector2 headScreenPos = _camera.WorldToScreenPoint(headWorldPos);

            RectTransform canvasRect = _parentCanvas.GetComponent<RectTransform>();
            Camera uiCamera = _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _parentCanvas.worldCamera;

            bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                headScreenPos,
                uiCamera,
                out Vector2 headLocalPos
            );

            if (!success)
            {
                Debug.LogWarning($"[GuideLineUIRenderer] ScreenPointToLocalPointInRectangle failed");
                return;
            }

            // 화면 끝점 계산
            Vector2 endLocalPos = CalculateEndPoint(headLocalPos, direction, canvasRect);

            // 라인 길이 및 각도 계산
            Vector2 delta = endLocalPos - headLocalPos;
            float length = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            // RectTransform 설정
            _rectTransform.anchoredPosition = headLocalPos;
            _rectTransform.sizeDelta = new Vector2(length, _lineWidth);
            _rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
        }

        /// <summary>
        /// 페이드 인 애니메이션
        /// </summary>
        public void FadeIn(float duration = 0.2f)
        {
            if (_isVisible)
                return;

            _isVisible = true;
            UpdateLine();

            _fadeTween?.Kill();
            _fadeTween = _lineImage.DOFade(_baseColor.a, duration).SetEase(Ease.OutQuad);
        }

        /// <summary>
        /// 페이드 아웃 애니메이션
        /// </summary>
        public void FadeOut(float duration = 0.2f)
        {
            if (!_isVisible)
                return;

            _isVisible = false;

            _fadeTween?.Kill();
            _fadeTween = _lineImage.DOFade(0f, duration).SetEase(Ease.OutQuad);
        }

        /// <summary>
        /// 즉시 숨기기
        /// </summary>
        public void HideImmediate()
        {
            _fadeTween?.Kill();
            _isVisible = false;
            SetAlpha(0f);
        }

        /// <summary>
        /// 즉시 표시
        /// </summary>
        public void ShowImmediate()
        {
            _isVisible = true;
            UpdateLine();
            SetAlpha(_baseColor.a);
        }

        // ========== 내부 유틸리티 ==========
        private void SetAlpha(float alpha)
        {
            if (_lineImage == null)
                return;

            Color color = _lineImage.color;
            color.a = alpha;
            _lineImage.color = color;
        }

        private Vector2 CalculateEndPoint(Vector2 startPos, ArrowDirection direction, RectTransform canvasRect)
        {
            // Canvas 실제 크기 가져오기 (Screen Space - Camera에서는 rect 사용)
            Rect rect = canvasRect.rect;
            float halfWidth = rect.width / 2f;
            float halfHeight = rect.height / 2f;

            // 방향에 따라 화면 끝까지 연장
            return direction switch
            {
                ArrowDirection.Up => new Vector2(startPos.x, halfHeight + 100f),
                ArrowDirection.Down => new Vector2(startPos.x, -halfHeight - 100f),
                ArrowDirection.Left => new Vector2(-halfWidth - 100f, startPos.y),
                ArrowDirection.Right => new Vector2(halfWidth + 100f, startPos.y),
                _ => new Vector2(startPos.x, halfHeight + 100f)
            };
        }
    }
}