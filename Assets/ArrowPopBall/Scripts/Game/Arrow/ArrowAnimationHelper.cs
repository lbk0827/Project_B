using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Grid;
using Game.Utilities;
using DG.Tweening;

namespace Game.Arrow
{
    /// <summary>
    /// 화살표 애니메이션/연출 담당 (등장, 페이드 아웃, 실수 표시)
    /// ArrowController에서 분리됨
    /// </summary>
    public class ArrowAnimationHelper : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("등장 연출")]
        [SerializeField] private float _appearSpeedPerCell = 0.03f;
        [SerializeField] private float _headFadeInDuration = 0.1f;

        [Header("실수 표시 (깜빡임)")]
        [SerializeField] private bool _enableMistakeVisual = true;
        [SerializeField] private float _blinkDuration = 0.3f;
        [SerializeField] private int _blinkCount = 3;
        [SerializeField] private Color _warningColor = new Color(1f, 0.2f, 0.2f, 0.5f);

        // ========== 내부 상태 변수 ==========
        private bool _isAppearing;
        private Coroutine _appearCoroutine;
        private bool _hasMadeMistake;
        private Sequence _blinkSequence;

        // ========== 참조 ==========
        private ArrowVisualRenderer _visualRenderer;

        // ========== 프로퍼티 ==========
        public bool IsAppearing => _isAppearing;
        public bool HasMadeMistake => _hasMadeMistake;

        // ========== 유니티 라이프사이클 ==========
        private void OnDestroy()
        {
            _blinkSequence?.Kill();
        }

        // ========== 공개 인터페이스 ==========
        /// <summary>
        /// 초기화
        /// </summary>
        public void Initialize(ArrowVisualRenderer visualRenderer)
        {
            _visualRenderer = visualRenderer;
        }

        /// <summary>
        /// 등장 연출 시작 (Tail → Head 순차 등장)
        /// </summary>
        public void PlayAppearAnimation(List<Vector2> cellWorldPositions, Vector2Int moveDirection,
            float headOffset, float tailOffset, float delay = 0f, Action onComplete = null)
        {
            if (_appearCoroutine != null)
            {
                StopCoroutine(_appearCoroutine);
            }
            _appearCoroutine = StartCoroutine(AppearAnimationCoroutine(
                cellWorldPositions, moveDirection, headOffset, tailOffset, delay, onComplete));
        }

        /// <summary>
        /// 즉시 숨기기 (등장 연출 전 호출)
        /// </summary>
        public void HideImmediate()
        {
            _isAppearing = true;

            if (_visualRenderer != null)
            {
                _visualRenderer.HideLineRenderer();
                _visualRenderer.SetHeadAlpha(0f);
            }
        }

        /// <summary>
        /// 즉시 표시 (연출 없이)
        /// </summary>
        public void ShowImmediate()
        {
            _isAppearing = false;

            if (_visualRenderer != null)
            {
                _visualRenderer.SetHeadAlpha(1f);
            }
        }

        /// <summary>
        /// LineRenderer 페이드 아웃 전환 시작 (HomingArrow 전환 연출용)
        /// </summary>
        public void StartFadeOutTransition(float lineWidth, float duration, Action onComplete)
        {
            StartCoroutine(FadeOutLineRenderer(lineWidth, duration, onComplete));
        }

        /// <summary>
        /// 실수 표시 (깜빡임) 적용 - 충돌 후 복귀 시 호출
        /// </summary>
        public void ApplyMistakeVisual(GameColor color)
        {
            if (!_enableMistakeVisual || _visualRenderer == null)
                return;

            _hasMadeMistake = true;

            // 기존 시퀀스 정리
            _blinkSequence?.Kill();

            // 원래 색상 (반투명)
            Color originalColor = ArrowVisualRenderer.GetUnityColor(color);
            originalColor.a = 0.4f;

            // DOTween 시퀀스로 깜빡임 연출
            _blinkSequence = DOTween.Sequence();

            for (int i = 0; i < _blinkCount; i++)
            {
                // 경고 색상으로 전환
                _blinkSequence.AppendCallback(() => _visualRenderer.SetColor(_warningColor));
                _blinkSequence.AppendInterval(_blinkDuration * 0.5f);
                // 원래 색상으로 복귀
                _blinkSequence.AppendCallback(() => _visualRenderer.SetColor(originalColor));
                _blinkSequence.AppendInterval(_blinkDuration * 0.5f);
            }

            // 최종 색상 적용
            _blinkSequence.OnComplete(() => _visualRenderer.SetColor(originalColor));

            Debug.Log($"[ArrowAnimationHelper] Mistake visual (blink) applied");
        }

        // ========== 내부 유틸리티 ==========
        private IEnumerator AppearAnimationCoroutine(List<Vector2> cellWorldPositions,
            Vector2Int moveDirection, float headOffset, float tailOffset, float delay, Action onComplete)
        {
            _isAppearing = true;

            if (delay > 0)
            {
                yield return WaitForSecondsCache.Get(delay);
            }

            if (_visualRenderer == null || cellWorldPositions == null || cellWorldPositions.Count == 0)
            {
                _isAppearing = false;
                onComplete?.Invoke();
                yield break;
            }

            var lineRenderer = _visualRenderer.LineRenderer;
            var headRenderer = _visualRenderer.HeadRenderer;

            if (lineRenderer == null)
            {
                _isAppearing = false;
                onComplete?.Invoke();
                yield break;
            }

            // Transform 위치 설정
            transform.position = cellWorldPositions[0];

            float cellSize = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
            float headOffsetAmount = cellSize * headOffset;
            float tailOffsetAmount = cellSize * tailOffset;

            // Tail 방향 계산
            Vector2 tailOffsetVec = CalculateTailOffset(cellWorldPositions, moveDirection, tailOffsetAmount);

            // Head 방향 계산
            Vector2 headOffsetVec = (Vector2)moveDirection * headOffsetAmount;

            // Tail 돌출점부터 시작
            lineRenderer.positionCount = 1;
            lineRenderer.SetPosition(0, (Vector3)tailOffsetVec);

            yield return WaitForSecondsCache.Get(_appearSpeedPerCell);

            // 각 셀을 순차적으로 추가 (Tail → Head)
            for (int i = 0; i < cellWorldPositions.Count; i++)
            {
                Vector3 localPos = cellWorldPositions[i] - cellWorldPositions[0];

                lineRenderer.positionCount = i + 2;
                lineRenderer.SetPosition(i + 1, localPos);

                yield return WaitForSecondsCache.Get(_appearSpeedPerCell);
            }

            // Head 돌출점 추가
            Vector3 lastCellLocal = cellWorldPositions[cellWorldPositions.Count - 1] - cellWorldPositions[0];
            lineRenderer.positionCount = cellWorldPositions.Count + 2;
            lineRenderer.SetPosition(cellWorldPositions.Count + 1, lastCellLocal + (Vector3)headOffsetVec);

            // Head 스프라이트 페이드인
            if (headRenderer != null)
            {
                headRenderer.transform.localPosition = lastCellLocal + (Vector3)headOffsetVec;
                headRenderer.DOFade(1f, _headFadeInDuration);
            }

            yield return WaitForSecondsCache.Get(_headFadeInDuration);

            _isAppearing = false;
            _appearCoroutine = null;

            onComplete?.Invoke();
        }

        private IEnumerator FadeOutLineRenderer(float startWidth, float duration, Action onComplete)
        {
            if (_visualRenderer == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            var lineRenderer = _visualRenderer.LineRenderer;
            var headRenderer = _visualRenderer.HeadRenderer;

            if (lineRenderer == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            float elapsed = 0f;
            Color startColor = lineRenderer.startColor;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 두께 감소
                float newWidth = Mathf.Lerp(startWidth, 0f, t);
                lineRenderer.startWidth = newWidth;
                lineRenderer.endWidth = newWidth;

                // 알파 감소
                Color newColor = startColor;
                newColor.a = Mathf.Lerp(1f, 0f, t);
                lineRenderer.startColor = newColor;
                lineRenderer.endColor = newColor;

                // Head 스프라이트 페이드 아웃
                if (headRenderer != null)
                {
                    Color headColor = headRenderer.color;
                    headColor.a = Mathf.Lerp(1f, 0f, t);
                    headRenderer.color = headColor;
                }

                yield return null;
            }

            onComplete?.Invoke();
        }

        private Vector2 CalculateTailOffset(List<Vector2> positions, Vector2Int moveDirection, float offsetAmount)
        {
            if (positions.Count >= 2)
            {
                Vector2 tailDiff = positions[1] - positions[0];
                float tailDist = tailDiff.magnitude;

                if (tailDist > 0.01f)
                {
                    Vector2 tailToSecond = tailDiff / tailDist;
                    return -tailToSecond * offsetAmount;
                }
            }

            return -(Vector2)moveDirection * offsetAmount;
        }
    }
}