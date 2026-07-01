using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Game.Grid;

namespace Game.Effects
{
    /// <summary>
    /// Dot Matrix Pulse 이펙트
    /// 그리드 중앙에서 외곽으로 퍼지는 웨이브 애니메이션 + 색상 변화
    /// </summary>
    public class DotMatrixPulseEffect : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("References")]
        [SerializeField] private GridSystem _gridSystem;

        [Header("Scale Animation")]
        [SerializeField] private float _scaleUpTime = 0.15f;
        [SerializeField] private float _scaleDownTime = 0.1f;
        [SerializeField] private float _maxScale = 6.0f;
        [SerializeField] private Ease _scaleUpEase = Ease.OutBack;
        [SerializeField] private Ease _scaleDownEase = Ease.InQuad;

        [Header("Color Animation")]
        [SerializeField] private float _colorTransitionTime = 0.1f;
        [SerializeField] private Ease _colorEase = Ease.OutQuad;

        [Header("Wave Settings")]
        [SerializeField] private float _delayPerDistance = 0.04f;
        [SerializeField] private bool _useEuclideanDistance = false;

        [Header("Pulse Color Palette")]
        [SerializeField] private Color[] _pulseColors = new Color[]
        {
            new Color(1f, 0.84f, 0f),       // Golden #FFD700
            new Color(1f, 0.42f, 0.42f),    // Coral #FF6B6B
            new Color(0.31f, 0.8f, 0.77f),  // Cyan #4ECDC4
            new Color(0.66f, 0.33f, 0.97f), // Violet #A855F7
            new Color(0.52f, 0.8f, 0.09f),  // Lime #84CC16
            new Color(0.93f, 0.29f, 0.6f),  // Pink #EC4899
            new Color(0.98f, 0.45f, 0.09f), // Orange #F97316
            new Color(0.22f, 0.74f, 0.97f)  // Sky #38BDF8
        };

        // ========== 내부 상태 변수 ==========
        private List<Sequence> _dotSequences = new List<Sequence>();
        private Color _currentPulseColor;
        private bool _isPlaying;
        private float _totalDuration;

        // ========== 유니티 라이프사이클 ==========
        private void OnDestroy()
        {
            Stop();
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// Pulse 효과 재생
        /// </summary>
        public void Play()
        {
            // GridSystem이 할당되지 않았으면 싱글톤에서 가져오기
            if (_gridSystem == null)
            {
                _gridSystem = GridSystem.Instance;
            }

            if (_gridSystem == null)
            {
                Debug.LogWarning("[DotMatrixPulseEffect] GridSystem not found!");
                return;
            }

            Stop(); // 기존 애니메이션 정리

            _isPlaying = true;
            _currentPulseColor = GetRandomPulseColor();

            Debug.Log($"[DotMatrixPulseEffect] Playing with color: {ColorUtility.ToHtmlStringRGB(_currentPulseColor)}");

            // 그리드 정보 가져오기
            int gridWidth = _gridSystem.GridWidth;
            int gridHeight = _gridSystem.GridHeight;

            // 중앙 좌표 계산
            float centerX = (gridWidth - 1) / 2f;
            float centerY = (gridHeight - 1) / 2f;

            // 최대 거리 계산 (총 애니메이션 시간 결정용)
            float maxDistance = 0f;

            // 모든 Dot에 대해 애니메이션 생성
            var dots = _gridSystem.GetAllDots();
            if (dots == null)
            {
                Debug.LogWarning("[DotMatrixPulseEffect] No dots found!");
                return;
            }

            foreach (var kvp in dots)
            {
                Vector2Int gridPos = kvp.Key;
                GameObject dot = kvp.Value;

                if (dot == null) continue;

                // 중앙으로부터의 거리 계산
                float distance = CalculateDistance(gridPos.x, gridPos.y, centerX, centerY);
                maxDistance = Mathf.Max(maxDistance, distance);

                // 거리 기반 딜레이
                float delay = distance * _delayPerDistance;

                // 애니메이션 생성
                CreateDotAnimation(dot, delay);
            }

            // 총 지속 시간 계산
            _totalDuration = (maxDistance * _delayPerDistance) + _scaleUpTime + _scaleDownTime;
            Debug.Log($"[DotMatrixPulseEffect] Total duration: {_totalDuration:F2}s");
        }

        /// <summary>
        /// 효과 중단
        /// </summary>
        public void Stop()
        {
            foreach (var seq in _dotSequences)
            {
                seq?.Kill();
            }
            _dotSequences.Clear();
            _isPlaying = false;
        }

        /// <summary>
        /// 총 애니메이션 지속 시간 반환
        /// </summary>
        public float GetTotalDuration()
        {
            return _totalDuration;
        }

        /// <summary>
        /// 현재 Pulse 색상 반환
        /// </summary>
        public Color CurrentPulseColor => _currentPulseColor;

        /// <summary>
        /// 재생 중인지 확인
        /// </summary>
        public bool IsPlaying => _isPlaying;

        // ========== 내부 유틸리티 ==========

        private float CalculateDistance(float x1, float y1, float x2, float y2)
        {
            if (_useEuclideanDistance)
            {
                // Euclidean Distance (원형 웨이브)
                return Mathf.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
            }
            else
            {
                // Manhattan Distance (다이아몬드 웨이브)
                return Mathf.Abs(x1 - x2) + Mathf.Abs(y1 - y2);
            }
        }

        private void CreateDotAnimation(GameObject dot, float delay)
        {
            var spriteRenderer = dot.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) return;

            // 원래 색상 저장
            Color originalColor = spriteRenderer.color;
            Vector3 originalScale = dot.transform.localScale;

            // 시퀀스 생성
            Sequence seq = DOTween.Sequence();

            // 딜레이
            seq.AppendInterval(delay);

            // 1. Scale Up + Color Change (동시)
            seq.Append(dot.transform.DOScale(originalScale * _maxScale, _scaleUpTime).SetEase(_scaleUpEase));
            seq.Join(spriteRenderer.DOColor(_currentPulseColor, _colorTransitionTime).SetEase(_colorEase));

            // 2. Scale Down + Fade Out (사라짐)
            seq.Append(dot.transform.DOScale(Vector3.zero, _scaleDownTime).SetEase(_scaleDownEase));
            seq.Join(spriteRenderer.DOFade(0f, _scaleDownTime));

            // 완료 시 비활성화
            seq.OnComplete(() =>
            {
                dot.SetActive(false);
            });

            _dotSequences.Add(seq);
        }

        private Color GetRandomPulseColor()
        {
            if (_pulseColors == null || _pulseColors.Length == 0)
            {
                return Color.yellow; // 기본 색상
            }

            int index = Random.Range(0, _pulseColors.Length);
            return _pulseColors[index];
        }

        /// <summary>
        /// 외부에서 색상 팔레트 설정
        /// </summary>
        public void SetPulseColors(Color[] colors)
        {
            _pulseColors = colors;
        }
    }
}