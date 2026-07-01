using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 풍선 UI 레이아웃 관리 - Packed Layout, Grid Layout 지원
    /// TargetAreaUI에서 분리됨
    /// </summary>
    public class BalloonLayoutManager : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("풍선 크기 설정")]
        [SerializeField] private float _minBalloonSize = 25f;
        [SerializeField] private float _maxBalloonSize = 70f;
        [SerializeField] private float _containerPadding = 10f;
        [SerializeField] private float _balloonSpacing = 5f;

        [Header("레이아웃 설정")]
        [SerializeField] private int _maxRows = 3;

        [Header("Packed Layout 설정")]
        [SerializeField] private float _overlapRatio = 0.35f;
        [SerializeField] private float _jitterAmount = 5f;
        [SerializeField] private bool _useHexPattern = true;
        [SerializeField] private bool _usePackedLayout = true;
        [SerializeField] private int _forcedRows = 0;

        [Header("재배치 애니메이션")]
        [SerializeField] private float _rearrangeDuration = 0.4f;
        [SerializeField] private Ease _rearrangeEase = Ease.OutBack;
        [SerializeField] private float _rearrangeDelay = 0.1f;

        [Header("압력감 연출")]
        [SerializeField] private bool _enablePressureEffect = true;
        [SerializeField] private float _pressureScaleMin = 0.85f;
        [SerializeField] private int _pressureMaxBalloons = 20;

        // ========== 내부 상태 변수 ==========
        private RectTransform _containerRect;
        private GridLayoutGroup _gridLayout;
        private float _currentBalloonSize;
        private readonly List<BalloonUIElement> _tempSortList = new List<BalloonUIElement>();

        // ========== 프로퍼티 ==========
        public float CurrentBalloonSize => _currentBalloonSize;
        public bool UsePackedLayout => _usePackedLayout;
        public float OverlapRatio => _overlapRatio;

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 초기화
        /// </summary>
        public void Initialize(Transform balloonContainer)
        {
            if (balloonContainer != null)
            {
                _containerRect = balloonContainer.GetComponent<RectTransform>();

                if (_usePackedLayout)
                {
                    // Packed Layout 사용 시 GridLayoutGroup 제거
                    _gridLayout = balloonContainer.GetComponent<GridLayoutGroup>();
                    if (_gridLayout != null)
                    {
                        Destroy(_gridLayout);
                        _gridLayout = null;
                    }
                }
                else
                {
                    // 기존 Grid Layout 사용
                    _gridLayout = balloonContainer.GetComponent<GridLayoutGroup>();
                    if (_gridLayout == null)
                    {
                        _gridLayout = balloonContainer.gameObject.AddComponent<GridLayoutGroup>();
                    }
                    _gridLayout.childAlignment = TextAnchor.MiddleCenter;
                    _gridLayout.constraint = GridLayoutGroup.Constraint.Flexible;
                }
            }
        }

        /// <summary>
        /// 풍선 크기 계산
        /// </summary>
        public float CalculateBalloonSize(int count)
        {
            if (_containerRect == null || count <= 0)
                return _minBalloonSize;

            float containerWidth = _containerRect.rect.width - (_containerPadding * 2);
            float containerHeight = _containerRect.rect.height - (_containerPadding * 2);

            if (_usePackedLayout)
            {
                // Packed Layout: 겹침을 고려한 크기 계산
                float overlapFactor = 1f - _overlapRatio;

                // 정사각형 그리드 기준으로 대략적인 크기 계산
                float aspect = containerWidth / containerHeight;
                int cols = Mathf.CeilToInt(Mathf.Sqrt(count * aspect));
                int rows = Mathf.CeilToInt((float)count / cols);

                float sizeByWidth = containerWidth / (cols * overlapFactor);
                float sizeByHeight = containerHeight / (rows * overlapFactor);

                float size = Mathf.Min(sizeByWidth, sizeByHeight);
                _currentBalloonSize = Mathf.Clamp(size, _minBalloonSize, _maxBalloonSize);
                return _currentBalloonSize;
            }

            // 기존 Grid Layout 로직
            float singleRowSize = (containerWidth - (_balloonSpacing * (count - 1))) / count;

            if (singleRowSize >= _minBalloonSize)
            {
                _currentBalloonSize = Mathf.Clamp(singleRowSize, _minBalloonSize, _maxBalloonSize);
                return _currentBalloonSize;
            }

            float bestSize = _minBalloonSize;

            for (int rows = 2; rows <= _maxRows; rows++)
            {
                int cols = Mathf.CeilToInt((float)count / rows);

                float sizeByWidth = (containerWidth - (_balloonSpacing * (cols - 1))) / cols;
                float sizeByHeight = (containerHeight - (_balloonSpacing * (rows - 1))) / rows;

                float possibleSize = Mathf.Min(sizeByWidth, sizeByHeight);

                if (possibleSize > bestSize)
                {
                    bestSize = possibleSize;
                }
            }

            _currentBalloonSize = Mathf.Clamp(bestSize, _minBalloonSize, _maxBalloonSize);
            return _currentBalloonSize;
        }

        /// <summary>
        /// 풍선 배치 실행 (레이아웃 타입에 따라)
        /// </summary>
        public void ArrangeBalloons(List<BalloonUIElement> balloons, float balloonSize)
        {
            if (_usePackedLayout)
            {
                ArrangeBalloonsPacked(balloons, balloonSize);
            }
            else
            {
                UpdateGridLayout(balloonSize);
            }
        }

        /// <summary>
        /// Packed Layout으로 풍선 배치
        /// </summary>
        public void ArrangeBalloonsPacked(List<BalloonUIElement> balloons, float balloonSize)
        {
            if (balloons.Count == 0 || _containerRect == null)
                return;

            float containerWidth = _containerRect.rect.width - (_containerPadding * 2);
            float containerHeight = _containerRect.rect.height - (_containerPadding * 2);

            // 겹침을 고려한 실제 간격
            float effectiveSpacing = balloonSize * (1f - _overlapRatio);

            int totalCount = balloons.Count;
            int rowCount;
            int colsPerRow;

            if (_forcedRows > 0)
            {
                rowCount = _forcedRows;
                colsPerRow = Mathf.CeilToInt((float)totalCount / rowCount);
            }
            else
            {
                // Container 너비에 맞춰 열 수 계산
                colsPerRow = Mathf.Max(1, Mathf.FloorToInt((containerWidth - balloonSize) / effectiveSpacing) + 1);
                rowCount = Mathf.CeilToInt((float)totalCount / colsPerRow);

                // 1줄이면 강제로 2줄로 (밀집 효과)
                if (rowCount == 1 && totalCount >= 3)
                {
                    rowCount = 2;
                    colsPerRow = Mathf.CeilToInt((float)totalCount / rowCount);
                }
            }

            // 전체 클러스터 높이
            float clusterHeight = (rowCount - 1) * effectiveSpacing + balloonSize;

            // 중앙 정렬을 위한 Y 시작 오프셋
            float startY = clusterHeight / 2f - balloonSize / 2f;

            // 압력감 스케일 계산
            float pressureScale = CalculatePressureScale(balloons.Count);

            int index = 0;
            for (int row = 0; row < rowCount && index < balloons.Count; row++)
            {
                // 이 행에 들어갈 풍선 개수 계산
                int remainingBalloons = totalCount - index;
                int remainingRows = rowCount - row;
                int balloonsInThisRow = Mathf.CeilToInt((float)remainingBalloons / remainingRows);

                // 이 행의 클러스터 너비
                float rowClusterWidth = (balloonsInThisRow - 1) * effectiveSpacing + balloonSize;

                // 홀수 줄은 오프셋 (Hexagonal 패턴)
                float hexOffset = (_useHexPattern && row % 2 == 1) ? effectiveSpacing * 0.5f : 0f;

                // 중앙 정렬을 위한 X 시작 오프셋
                float startX = -rowClusterWidth / 2f + balloonSize / 2f + hexOffset;

                for (int col = 0; col < balloonsInThisRow && index < balloons.Count; col++)
                {
                    var balloon = balloons[index];
                    RectTransform rect = balloon.CachedRectTransform;

                    if (rect != null)
                    {
                        // 기본 위치
                        float x = startX + col * effectiveSpacing;
                        float y = startY - row * effectiveSpacing;

                        // Jitter 추가 (미세한 랜덤 오프셋)
                        x += Random.Range(-_jitterAmount, _jitterAmount);
                        y += Random.Range(-_jitterAmount, _jitterAmount);

                        // 기본 위치 설정
                        balloon.SetBasePosition(new Vector2(x, y));

                        // 압력감 스케일 적용
                        if (_enablePressureEffect)
                        {
                            balloon.SetPressureScale(pressureScale);
                        }
                    }

                    index++;
                }
            }

            // Z 정렬
            ApplyZOrderByYPosition(balloons);

            Debug.Log($"[BalloonLayoutManager] Packed layout: {balloons.Count} balloons, {rowCount} rows, overlap={_overlapRatio:P0}, pressure={pressureScale:F2}");
        }

        /// <summary>
        /// 풍선 재배치 (DOTween 애니메이션 포함)
        /// </summary>
        public void RearrangeBalloonsAnimated(List<BalloonUIElement> balloons, float balloonSize)
        {
            if (balloons.Count == 0 || _containerRect == null)
                return;

            float containerWidth = _containerRect.rect.width - (_containerPadding * 2);
            float effectiveSpacing = balloonSize * (1f - _overlapRatio);

            int totalCount = balloons.Count;
            int rowCount;
            int colsPerRow;

            if (_forcedRows > 0)
            {
                rowCount = _forcedRows;
                colsPerRow = Mathf.CeilToInt((float)totalCount / rowCount);
            }
            else
            {
                colsPerRow = Mathf.Max(1, Mathf.FloorToInt((containerWidth - balloonSize) / effectiveSpacing) + 1);
                rowCount = Mathf.CeilToInt((float)totalCount / colsPerRow);

                if (rowCount == 1 && totalCount >= 3)
                {
                    rowCount = 2;
                    colsPerRow = Mathf.CeilToInt((float)totalCount / rowCount);
                }
            }

            float clusterHeight = (rowCount - 1) * effectiveSpacing + balloonSize;
            float startY = clusterHeight / 2f - balloonSize / 2f;

            // 압력감 스케일 계산
            float pressureScale = CalculatePressureScale(balloons.Count);

            int index = 0;
            for (int row = 0; row < rowCount && index < balloons.Count; row++)
            {
                int remainingBalloons = totalCount - index;
                int remainingRows = rowCount - row;
                int balloonsInThisRow = Mathf.CeilToInt((float)remainingBalloons / remainingRows);

                float rowClusterWidth = (balloonsInThisRow - 1) * effectiveSpacing + balloonSize;
                float hexOffset = (_useHexPattern && row % 2 == 1) ? effectiveSpacing * 0.5f : 0f;
                float startX = -rowClusterWidth / 2f + balloonSize / 2f + hexOffset;

                for (int col = 0; col < balloonsInThisRow && index < balloons.Count; col++)
                {
                    var balloon = balloons[index];
                    RectTransform rect = balloon.CachedRectTransform;

                    if (rect != null)
                    {
                        float x = startX + col * effectiveSpacing;
                        float y = startY - row * effectiveSpacing;

                        Vector2 targetPos = new Vector2(x, y);

                        // 재배치 애니메이션 시작
                        balloon.StartRearrangeAnimation();

                        // DOTween으로 부드럽게 이동
                        rect.DOKill();
                        var currentBalloon = balloon;
                        var currentPressureScale = pressureScale;
                        rect.DOAnchorPos(targetPos, _rearrangeDuration)
                            .SetDelay(_rearrangeDelay)
                            .SetEase(_rearrangeEase)
                            .OnComplete(() =>
                            {
                                currentBalloon.SetBasePosition(targetPos);
                                if (_enablePressureEffect)
                                {
                                    currentBalloon.SetPressureScale(currentPressureScale);
                                }
                                currentBalloon.EndRearrangeAnimation();
                            });
                    }

                    index++;
                }
            }

            // 애니메이션 완료 후 Z 정렬
            float totalAnimDuration = _rearrangeDelay + _rearrangeDuration;
            DOVirtual.DelayedCall(totalAnimDuration * 0.7f, () => ApplyZOrderByYPosition(balloons));

            Debug.Log($"[BalloonLayoutManager] Rearranged {balloons.Count} balloons with animation, pressure={pressureScale:F2}");
        }

        /// <summary>
        /// Grid Layout 업데이트
        /// </summary>
        public void UpdateGridLayout(float cellSize)
        {
            if (_gridLayout == null)
                return;

            _gridLayout.cellSize = new Vector2(cellSize, cellSize);
            _gridLayout.spacing = new Vector2(_balloonSpacing, _balloonSpacing);
            _gridLayout.padding = new RectOffset(
                (int)_containerPadding,
                (int)_containerPadding,
                (int)_containerPadding,
                (int)_containerPadding
            );
        }

        // ========== 내부 유틸리티 ==========

        /// <summary>
        /// 압력감 스케일 계산
        /// </summary>
        private float CalculatePressureScale(int balloonCount)
        {
            if (!_enablePressureEffect)
                return 1f;

            float t = Mathf.InverseLerp(1, _pressureMaxBalloons, balloonCount);
            return Mathf.Lerp(1f, _pressureScaleMin, t);
        }

        /// <summary>
        /// Y 위치에 따른 Z 정렬 적용
        /// </summary>
        private void ApplyZOrderByYPosition(List<BalloonUIElement> balloons)
        {
            _tempSortList.Clear();
            for (int i = 0; i < balloons.Count; i++)
            {
                if (balloons[i] != null)
                    _tempSortList.Add(balloons[i]);
            }

            _tempSortList.Sort((a, b) =>
                b.CachedRectTransform.anchoredPosition.y.CompareTo(
                    a.CachedRectTransform.anchoredPosition.y));

            for (int i = 0; i < _tempSortList.Count; i++)
            {
                _tempSortList[i].transform.SetSiblingIndex(i);
            }
        }
    }
}