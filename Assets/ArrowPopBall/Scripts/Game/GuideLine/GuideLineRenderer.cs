using System;
using UnityEngine;
using DG.Tweening;
using Game.Arrow;
using Game.Grid;
using Game.Data;

namespace Game.GuideLine
{
    /// <summary>
    /// 가이드 라인 렌더러 - 개별 화살표의 가이드 라인 표시
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class GuideLineRenderer : MonoBehaviour
    {
        // ========== 내부 상태 변수 ==========
        private LineRenderer _lineRenderer;
        private ArrowController _arrow;
        private Tween _fadeTween;
        private Color _baseColor;
        private bool _isVisible;
        private float _topUIAreaRatio = 0.25f;

        // ========== 프로퍼티 ==========
        public ArrowController Arrow => _arrow;
        public bool IsVisible => _isVisible;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
        }

        private void OnDestroy()
        {
            _fadeTween?.Kill();
        }

        // ========== 공개 인터페이스 ==========
        /// <summary>
        /// 가이드 라인 초기화
        /// </summary>
        public void Initialize(ArrowController arrow, Color color, float width, string sortingLayerName, int orderInLayer, float topUIAreaRatio = 0.25f)
        {
            _arrow = arrow;
            _baseColor = color;
            _isVisible = false;
            _topUIAreaRatio = topUIAreaRatio;

            if (_lineRenderer == null)
                _lineRenderer = GetComponent<LineRenderer>();

            // LineRenderer 설정
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.positionCount = 2;
            _lineRenderer.startWidth = width;
            _lineRenderer.endWidth = width;
            _lineRenderer.sortingLayerName = sortingLayerName;
            _lineRenderer.sortingOrder = orderInLayer;

            // Material 생성 및 renderQueue 설정 (UI보다 먼저 렌더링되도록)
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.renderQueue = 2000; // Geometry 큐 (UI는 3000)
            _lineRenderer.material = mat;

            // 초기 상태: 투명
            SetAlpha(0f);
            UpdateLine();
        }

        /// <summary>
        /// 라인 위치 업데이트 (Head Tip → 화면 끝)
        /// </summary>
        public void UpdateLine()
        {
            if (_arrow == null || _lineRenderer == null)
                return;

            // Head의 뾰족한 끝(Tip)에서 시작
            Vector2 tipPos = _arrow.GetHeadTipWorldPosition();
            Vector2 direction = GetDirectionVector(_arrow.HeadDirection);
            Vector2 endPos = CalculateEndPoint(tipPos, direction);

            // Z=15로 설정하여 UI (PlaneDistance=10) 뒤에 렌더링
            _lineRenderer.SetPosition(0, new Vector3(tipPos.x, tipPos.y, 15f));
            _lineRenderer.SetPosition(1, new Vector3(endPos.x, endPos.y, 15f));
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
            _fadeTween = DOTween.To(
                () => _lineRenderer.startColor.a,
                SetAlpha,
                _baseColor.a,
                duration
            ).SetEase(Ease.OutQuad);
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
            _fadeTween = DOTween.To(
                () => _lineRenderer.startColor.a,
                SetAlpha,
                0f,
                duration
            ).SetEase(Ease.OutQuad);
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
            if (_lineRenderer == null)
                return;

            Color color = _baseColor;
            color.a = alpha;
            _lineRenderer.startColor = color;
            _lineRenderer.endColor = color;
        }

        private Vector2 GetDirectionVector(ArrowDirection direction)
        {
            return direction switch
            {
                ArrowDirection.Up => Vector2.up,
                ArrowDirection.Down => Vector2.down,
                ArrowDirection.Left => Vector2.left,
                ArrowDirection.Right => Vector2.right,
                _ => Vector2.up
            };
        }

        private Vector2 CalculateEndPoint(Vector2 startPos, Vector2 direction)
        {
            // 화면 끝까지 충분히 연장 (50 유닛)
            return startPos + direction * 50f;
        }
    }
}
