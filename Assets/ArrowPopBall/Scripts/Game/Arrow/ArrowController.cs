using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Grid;
using Game.Core;
using DG.Tweening;

namespace Game.Arrow
{
    /// <summary>
    /// 화살표 컨트롤러 - 폴리라인(꺾이는) 화살표 지원, Snake 방식 이동
    /// 렌더링은 ArrowVisualRenderer, 연출은 ArrowAnimationHelper에 위임
    /// </summary>
    public class ArrowController : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("필수 참조")]
        [SerializeField] private ArrowVisualRenderer _visualRenderer;
        [SerializeField] private ArrowAnimationHelper _animationHelper;
        [SerializeField] private ArrowColliderManager _colliderManager;

        [Header("이동 설정")]
        [SerializeField] private float _moveSpeed = 8f;

        [Header("애니메이션 설정")]
        [SerializeField] private Ease _moveEase = Ease.OutQuad;
        [SerializeField] private float _launchPunchScale = 0.15f;
        [SerializeField] private float _launchPunchDuration = 0.1f;

        // ========== 내부 상태 변수 ==========
        private int _id;
        private GameColor _color;
        private ArrowState _state;
        private SegmentData[] _segments;
        private List<Vector2Int> _occupiedCells;
        private Vector2Int _headPosition;
        private ArrowDirection _headDirection;
        private Vector2Int _moveDirection;

        // Snake 이동용 변수
        private bool _isExtracting;
        private bool _isReturning;
        private bool _ignoreCollision;  // Arrow Dash용 충돌 무시 플래그
        private float _moveProgress;
        private List<Vector2> _cellWorldPositions;
        private List<Vector2> _previousWorldPositions;
        private Tween _moveTween;
        private Tween _highlightTween;


        // 발사 시점 위치 백업 (충돌 시 복원용)
        private List<Vector2Int> _launchOccupiedCells = new List<Vector2Int>();
        private List<Vector2> _launchWorldPositions = new List<Vector2>();
        private Vector2Int _launchHeadPosition;
        private int _returnStepsRemaining;

        // 이동 경로 기록 (역방향 복귀용)
        private List<List<Vector2Int>> _movementHistory;
        private int _currentHistoryIndex;

        // 애니메이션 계산용 재사용 리스트 (GC 할당 방지)
        private readonly List<Vector2> _tempAnimatedPositions = new List<Vector2>();
        private readonly List<Vector2> _tempTargetPositions = new List<Vector2>();
        private readonly List<Vector2> _tempFullPath = new List<Vector2>();


        // ========== 이벤트 ==========
        public event Action<ArrowController> OnExtracted;
        public event Action<ArrowController> OnStateChanged;
        public event Action<ArrowController> OnStopped;
        public event Action<ArrowController> OnCollided;
        public event Action<ArrowController> OnWallHit;
        public event Action<ArrowController, Vector2, ArrowDirection> OnExtractionStarted;
        public event Action<ArrowController> OnMoveStarted;

        // ========== 프로퍼티 ==========
        public int Id => _id;
        public Vector2Int HeadPosition => _headPosition;
        public ArrowDirection HeadDirection => _headDirection;
        public GameColor Color => _color;
        public int TotalLength => _occupiedCells?.Count ?? 0;
        public ArrowState State => _state;
        public bool CanLaunch => _state == ArrowState.Idle && !(_animationHelper?.IsAppearing ?? false);
        public bool IsExtracting => _isExtracting;
        public bool IsAppearing => _animationHelper?.IsAppearing ?? false;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            if (_colliderManager == null)
            {
                _colliderManager = GetComponent<ArrowColliderManager>();
                if (_colliderManager == null)
                    _colliderManager = gameObject.AddComponent<ArrowColliderManager>();
            }
        }

private void OnDestroy()
        {
            _moveTween?.Kill();
            _highlightTween?.Kill();
        }

        // ========== 공개 인터페이스 ==========
        /// <summary>
        /// 화살표 초기화 (폴리라인)
        /// </summary>
        public void Initialize(ArrowData data)
        {
            _id = data.id;
            _color = data.color;
            _segments = data.segments;
            _state = ArrowState.Idle;
            _isExtracting = false;

            // 세그먼트로부터 점유 셀 목록 계산
            CalculateOccupiedCells(data.StartPosition);

            // 머리 방향 설정
            _headDirection = data.HeadDirection;
            _moveDirection = GridSystem.Instance.GetDirectionVector(_headDirection);

            // 월드 좌표 캐시
            CacheWorldPositions();

            // 시각 렌더러 초기화
            if (_visualRenderer != null)
            {
                _visualRenderer.Initialize(_color, _headDirection);
                _visualRenderer.UpdateLineRenderer(_cellWorldPositions, _moveDirection);
            }

            // 애니메이션 헬퍼 초기화
            if (_animationHelper != null)
            {
                _animationHelper.Initialize(_visualRenderer);
            }

            RegisterOccupiedCells(true);
            _colliderManager?.UpdateColliders(_occupiedCells);
        }

        /// <summary>
        /// 화살표 발사 (탭 시 호출)
        /// </summary>
        public void Launch()
        {
            if (_state != ArrowState.Idle)
                return;

            _ignoreCollision = false;
            BackupLaunchPosition();

            // 이동 시작 이벤트 발생 (가이드 라인 숨김용)
            OnMoveStarted?.Invoke(this);

            // 펀치 애니메이션과 이동을 동시에 시작 (즉각적인 반응)
            transform.DOPunchScale(Vector3.one * _launchPunchScale, _launchPunchDuration, 1, 0f);
            TryMoveToNext();
        }

        /// <summary>
        /// 충돌 무시하고 즉시 탈출 (Arrow Dash용)
        /// 다른 화살표와 충돌하지 않고 바로 탈출 모드로 진입
        /// </summary>
        public void LaunchWithoutCollision()
        {
            if (_state != ArrowState.Idle)
                return;

            Debug.Log($"[ArrowController] LaunchWithoutCollision - Arrow {_id}, Direction: {_headDirection}");

            _ignoreCollision = true;

            // 이동 시작 이벤트 발생
            OnMoveStarted?.Invoke(this);

            // 바로 탈출 모드 시작 (충돌 체크 없이)
            _isExtracting = true;
            RegisterOccupiedCells(false);

            Vector2 headWorldPos = GetHeadWorldPosition();
            OnExtractionStarted?.Invoke(this, headWorldPos, _headDirection);

            // 펀치 애니메이션과 탈출을 동시에 시작
            transform.DOPunchScale(Vector3.one * _launchPunchScale, _launchPunchDuration, 1, 0f);

            Vector2Int nextHeadPos = _headPosition + _moveDirection;
            StartSnakeExtract(nextHeadPos);
        }

        /// <summary>
        /// 화살표가 차지하는 모든 그리드 위치 반환
        /// </summary>
        public Vector2Int[] GetOccupiedPositions()
        {
            return _occupiedCells?.ToArray() ?? new Vector2Int[0];
        }

        /// <summary>
        /// Head의 현재 월드 좌표 반환 (셀 중심)
        /// </summary>
        public Vector2 GetHeadWorldPosition()
        {
            return GridSystem.Instance.GridToWorld(_headPosition);
        }

        /// <summary>
        /// Head의 뾰족한 끝(Tip) 월드 좌표 반환
        /// </summary>
        public Vector2 GetHeadTipWorldPosition()
        {
            Vector2 headCenter = GetHeadWorldPosition();
            float halfCell = GridSystem.Instance.CellSize * 0.5f;
            Vector2 direction = GetDirectionVector(_headDirection);
            return headCenter + direction * halfCell;
        }

        private Vector2 GetDirectionVector(Data.ArrowDirection dir)
        {
            return dir switch
            {
                Data.ArrowDirection.Up => Vector2.up,
                Data.ArrowDirection.Down => Vector2.down,
                Data.ArrowDirection.Left => Vector2.left,
                Data.ArrowDirection.Right => Vector2.right,
                _ => Vector2.up
            };
        }

        /// <summary>
        /// 모든 셀의 월드 좌표 반환
        /// </summary>
        public List<Vector2> GetAllWorldPositions()
        {
            return new List<Vector2>(_cellWorldPositions);
        }

        /// <summary>
        /// 등장 연출 시작 (Tail → Head 순차 등장)
        /// </summary>
        public void PlayAppearAnimation(float delay = 0f, Action onComplete = null)
        {
            if (_animationHelper != null)
            {
                _animationHelper.PlayAppearAnimation(
                    _cellWorldPositions,
                    _moveDirection,
                    _visualRenderer?.HeadOffset ?? 0.35f,
                    _visualRenderer?.TailOffset ?? 0.35f,
                    delay,
                    () =>
                    {
                        _colliderManager?.UpdateColliders(_occupiedCells);
                        onComplete?.Invoke();
                    });
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// 즉시 숨기기 (등장 연출 전 호출)
        /// </summary>
        public void HideImmediate()
        {
            _animationHelper?.HideImmediate();
        }

        /// <summary>
        /// 즉시 표시 (연출 없이)
        /// </summary>
        public void ShowImmediate()
        {
            _animationHelper?.ShowImmediate();
            UpdateLineRenderer();
        }

        /// <summary>
        /// LineRenderer 페이드 아웃 전환 시작 (HomingArrow 전환 연출용)
        /// </summary>
        /// <summary>
        /// 힌트 하이라이트 연출 (DOTween 색상 펄스 루프)
        /// </summary>
public void SetHighlight(bool enabled)
        {
            _highlightTween?.Kill();
            _highlightTween = null;

            if (enabled)
            {
                UnityEngine.Color baseColor = ArrowVisualRenderer.GetUnityColor(_color);
                UnityEngine.Color brightColor = UnityEngine.Color.Lerp(baseColor, UnityEngine.Color.white, 0.6f);
                _highlightTween = DOVirtual.Float(0f, 1f, 0.4f, t =>
                {
                    _visualRenderer?.SetColor(UnityEngine.Color.Lerp(baseColor, brightColor, t));
                })
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
            }
            else
            {
                _visualRenderer?.SetColor(_color);
            }
        }

        
public void StartFadeOutTransition(float duration, Action onComplete)
        {
            if (_animationHelper != null)
            {
                _animationHelper.StartFadeOutTransition(
                    _visualRenderer?.LineWidth ?? 0.3f,
                    duration,
                    onComplete);
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        // ========== 내부 유틸리티 ==========
        private void BackupLaunchPosition()
        {
            CopyList(_occupiedCells, ref _launchOccupiedCells);
            CopyList(_cellWorldPositions, ref _launchWorldPositions);
            _launchHeadPosition = _headPosition;

            if (_movementHistory == null)
                _movementHistory = new List<List<Vector2Int>>();
            else
                _movementHistory.Clear();
            _movementHistory.Add(new List<Vector2Int>(_occupiedCells));
        }

        private void RecordMovementSnapshot()
        {
            _movementHistory?.Add(new List<Vector2Int>(_occupiedCells));
        }

        private void StartReverseReturn()
        {
            if (_movementHistory == null || _movementHistory.Count <= 1)
            {
                _animationHelper?.ApplyMistakeVisual(_color);
                SetState(ArrowState.Idle);
                OnStopped?.Invoke(this);
                return;
            }

            CopyList(_cellWorldPositions, ref _previousWorldPositions);
            _currentHistoryIndex = _movementHistory.Count - 1;
            _returnStepsRemaining = _movementHistory.Count - 1;
            _isReturning = true;
            SetState(ArrowState.Moving);

            StartReverseMoveTween();
        }

        private void StartReverseMoveTween()
        {
            _moveTween?.Kill();

            float duration = 1f / _moveSpeed;

            _moveProgress = 0f;
            _moveTween = DOTween.To(
                () => _moveProgress,
                x =>
                {
                    _moveProgress = x;
                    UpdateReverseReturnAnimation(_moveProgress);
                },
                1f,
                duration
            )
            .SetEase(_moveEase)
            .OnComplete(CompleteReverseStep);
        }

        private void UpdateReverseReturnAnimation(float t)
        {
            if (_previousWorldPositions == null || _currentHistoryIndex <= 0)
                return;

            List<Vector2Int> targetCells = _movementHistory[_currentHistoryIndex - 1];
            _tempTargetPositions.Clear();
            foreach (var cell in targetCells)
            {
                _tempTargetPositions.Add(GridSystem.Instance.GridToWorld(cell));
            }

            _tempAnimatedPositions.Clear();
            int cellCount = Mathf.Max(_previousWorldPositions.Count, _tempTargetPositions.Count);

            for (int i = 0; i < cellCount; i++)
            {
                Vector2 startPos = i < _previousWorldPositions.Count
                    ? _previousWorldPositions[i]
                    : _tempTargetPositions[i];

                Vector2 targetPos = i < _tempTargetPositions.Count
                    ? _tempTargetPositions[i]
                    : startPos;

                _tempAnimatedPositions.Add(Vector2.Lerp(startPos, targetPos, t));
            }

            UpdateLineRendererWithPositions(_tempAnimatedPositions);
        }

        private void CompleteReverseStep()
        {
            _currentHistoryIndex--;
            if (_currentHistoryIndex < 0)
                _currentHistoryIndex = 0;

            List<Vector2Int> targetCells = _movementHistory[_currentHistoryIndex];

            RegisterOccupiedCells(false);
            CopyList(targetCells, ref _occupiedCells);
            CacheWorldPositions();
            RegisterOccupiedCells(true);

            if (_occupiedCells.Count > 0)
            {
                _headPosition = _occupiedCells[_occupiedCells.Count - 1];
            }

            _returnStepsRemaining--;
            CopyList(_cellWorldPositions, ref _previousWorldPositions);

            UpdateLineRenderer();

            if (_currentHistoryIndex <= 0 || _returnStepsRemaining <= 0)
            {
                RegisterOccupiedCells(false);
                CopyList(_launchOccupiedCells, ref _occupiedCells);
                CopyList(_launchWorldPositions, ref _cellWorldPositions);
                _headPosition = _launchHeadPosition;
                RegisterOccupiedCells(true);

                _isReturning = false;
                _movementHistory = null;
                UpdateLineRenderer();

                _animationHelper?.ApplyMistakeVisual(_color);

                SetState(ArrowState.Idle);
                OnStopped?.Invoke(this);
            }
            else
            {
                StartReverseMoveTween();
            }
        }

        private void CalculateOccupiedCells(Vector2Int startPos)
        {
            _occupiedCells = new List<Vector2Int>();
            Vector2Int currentPos = startPos;

            _occupiedCells.Add(currentPos);

            foreach (var segment in _segments)
            {
                Vector2Int dir = GridSystem.Instance.GetDirectionVector(segment.direction);

                for (int i = 0; i < segment.length; i++)
                {
                    currentPos += dir;
                    _occupiedCells.Add(currentPos);
                }
            }

            _headPosition = currentPos;
        }

        private void CacheWorldPositions()
        {
            if (_cellWorldPositions == null)
                _cellWorldPositions = new List<Vector2>();
            else
                _cellWorldPositions.Clear();

            foreach (var cell in _occupiedCells)
            {
                _cellWorldPositions.Add(GridSystem.Instance.GridToWorld(cell));
            }
        }

        /// <summary>
        /// src 리스트 내용을 dst에 복사 (GC 할당 방지)
        /// </summary>
        private static void CopyList<T>(List<T> src, ref List<T> dst)
        {
            if (dst == null)
                dst = new List<T>(src);
            else
            {
                dst.Clear();
                dst.AddRange(src);
            }
        }

        private void SetState(ArrowState newState)
        {
            if (_state == newState)
                return;

            _state = newState;
            OnStateChanged?.Invoke(this);
        }

        private void TryMoveToNext()
        {
            Vector2Int nextHeadPos = _headPosition + _moveDirection;

            if (GridSystem.Instance.IsOutOfWorldBounds(nextHeadPos))
            {
                if (!_isExtracting)
                {
                    _isExtracting = true;
                    RegisterOccupiedCells(false);
                    Debug.Log($"[ArrowController] Starting extraction at {nextHeadPos}");

                    Vector2 headWorldPos = GetHeadWorldPosition();
                    OnExtractionStarted?.Invoke(this, headWorldPos, _headDirection);
                }

                StartSnakeExtract(nextHeadPos);
                return;
            }

            if (!CanMoveTo(nextHeadPos))
            {
                bool isArrowCollision = GridSystem.Instance.IsOccupied(nextHeadPos);

                if (isArrowCollision)
                {
                    OnCollided?.Invoke(this);
                }
                else
                {
                    OnWallHit?.Invoke(this);
                }

                StartReverseReturn();
                return;
            }

            StartSnakeMove(nextHeadPos);
        }

        private bool AreAllCellsOutOfCameraBounds()
        {
            var cameraController = Camera.main?.GetComponent<CameraController>();
            if (cameraController == null || !cameraController.HasBounds)
                return false;

            foreach (var worldPos in _cellWorldPositions)
            {
                if (!cameraController.IsOutOfWorldBounds(worldPos))
                    return false;
            }

            return true;
        }

        private void StartSnakeMove(Vector2Int nextHeadPos)
        {
            CopyList(_cellWorldPositions, ref _previousWorldPositions);

            Vector2Int tailPos = _occupiedCells[0];
            if (GridSystem.Instance.IsValidPosition(tailPos))
            {
                GridSystem.Instance.SetOccupied(tailPos, false);
            }

            if (GridSystem.Instance.IsValidPosition(nextHeadPos))
            {
                GridSystem.Instance.SetOccupied(nextHeadPos, true);
            }

            SetState(ArrowState.Moving);
            StartMoveTween();
        }

        private void StartSnakeExtract(Vector2Int nextHeadPos)
        {
            CopyList(_cellWorldPositions, ref _previousWorldPositions);
            SetState(ArrowState.Moving);
            StartMoveTween();
        }

        private void StartMoveTween()
        {
            _moveTween?.Kill();
            _highlightTween?.Kill();

            float duration = 1f / _moveSpeed;

            _moveProgress = 0f;
            _moveTween = DOTween.To(
                () => _moveProgress,
                x =>
                {
                    _moveProgress = x;
                    UpdateSnakeAnimation(_moveProgress);
                },
                1f,
                duration
            )
            .SetEase(_moveEase)
            .OnComplete(CompleteOneStep);
        }

        /// <summary>
        /// Snake 애니메이션 업데이트 - 경로 기반 슬라이딩 (개선됨)
        /// 각 셀이 경로를 따라 부드럽게 이동합니다.
        /// </summary>
        private void UpdateSnakeAnimation(float t)
        {
            if (_previousWorldPositions == null || _previousWorldPositions.Count == 0)
                return;

            _tempAnimatedPositions.Clear();
            float cellSize = GridSystem.Instance.CellSize;

            Vector2 headStartPos = _previousWorldPositions[_previousWorldPositions.Count - 1];
            Vector2 headTargetPos = headStartPos + (Vector2)_moveDirection * cellSize;

            if (_isExtracting)
            {
                // 탈출 중: 꼬리가 수축하면서 경로를 따라 이동
                Vector2 tailTargetPos = _previousWorldPositions.Count > 1
                    ? _previousWorldPositions[1]
                    : headTargetPos;

                Vector2 shrinkingTailPos = Vector2.Lerp(_previousWorldPositions[0], tailTargetPos, t);
                _tempAnimatedPositions.Add(shrinkingTailPos);

                for (int i = 1; i < _previousWorldPositions.Count; i++)
                {
                    Vector2 startPos = _previousWorldPositions[i];
                    Vector2 targetPos = (i == _previousWorldPositions.Count - 1)
                        ? headTargetPos
                        : _previousWorldPositions[i + 1];

                    _tempAnimatedPositions.Add(Vector2.Lerp(startPos, targetPos, t));
                }
            }
            else
            {
                // 전체 경로 구성: [이전 위치들] + [새 머리 위치]
                _tempFullPath.Clear();
                _tempFullPath.AddRange(_previousWorldPositions);
                _tempFullPath.Add(headTargetPos);

                // 각 셀의 새 위치 계산 (경로 위에서 t만큼 앞으로 슬라이딩)
                int cellCount = _cellWorldPositions.Count;

                for (int i = 0; i < cellCount; i++)
                {
                    float virtualIndex = i + t;
                    Vector2 pos = GetPointOnPath(_tempFullPath, virtualIndex);
                    _tempAnimatedPositions.Add(pos);
                }
            }

            UpdateLineRendererWithPositions(_tempAnimatedPositions);
        }

        /// <summary>
        /// 경로 상의 특정 위치(index) 좌표를 반환합니다.
        /// index가 정수가 아니면 두 점 사이를 보간합니다.
        /// </summary>
        private Vector2 GetPointOnPath(List<Vector2> path, float index)
        {
            if (path == null || path.Count == 0)
                return Vector2.zero;

            // 경로 범위 내로 클램프
            if (index <= 0)
                return path[0];
            if (index >= path.Count - 1)
                return path[path.Count - 1];

            // 정수 부분과 소수 부분 분리
            int floorIndex = Mathf.FloorToInt(index);
            float fraction = index - floorIndex;

            // 두 점 사이를 선형 보간
            Vector2 p0 = path[floorIndex];
            Vector2 p1 = path[floorIndex + 1];

            return Vector2.Lerp(p0, p1, fraction);
        }

        private void CompleteOneStep()
        {
            Vector2Int nextHeadPos = _headPosition + _moveDirection;

            if (_isExtracting)
            {
                if (_occupiedCells.Count > 0)
                {
                    _occupiedCells.RemoveAt(0);
                    _cellWorldPositions.RemoveAt(0);
                }

                _occupiedCells.Add(nextHeadPos);
                _cellWorldPositions.Add(GridSystem.Instance.GridToWorld(nextHeadPos));
                _headPosition = nextHeadPos;

                if (_occupiedCells.Count == 0 || AreAllCellsOutOfCameraBounds())
                {
                    SetState(ArrowState.Extracted);
                    Debug.Log($"[ArrowController] Extraction complete (out of camera bounds), color: {_color}");
                    OnExtracted?.Invoke(this);
                    Destroy(gameObject);
                    return;
                }

                CopyList(_cellWorldPositions, ref _previousWorldPositions);
            }
            else
            {
                _occupiedCells.RemoveAt(0);
                _occupiedCells.Add(nextHeadPos);
                _headPosition = nextHeadPos;

                CacheWorldPositions();
                RecordMovementSnapshot();
                CopyList(_cellWorldPositions, ref _previousWorldPositions);
            }

            UpdateLineRenderer();
            TryMoveToNext();
        }

        private bool CanMoveTo(Vector2Int nextHeadPos)
        {
            // Arrow Dash 충돌 무시 모드 - 다른 화살표와 충돌해도 이동 허용
            if (_ignoreCollision)
                return true;

            if (_occupiedCells.Count > 0 && nextHeadPos == _occupiedCells[0])
                return true;

            for (int i = 1; i < _occupiedCells.Count; i++)
            {
                if (nextHeadPos == _occupiedCells[i])
                    return false;
            }

            if (GridSystem.Instance.IsOutOfBounds(nextHeadPos) &&
                !GridSystem.Instance.IsOutOfWorldBounds(nextHeadPos))
            {
                return true;
            }

            if (GridSystem.Instance.IsOccupied(nextHeadPos))
                return false;

            return true;
        }

        private void RegisterOccupiedCells(bool occupied)
        {
            foreach (var pos in _occupiedCells)
            {
                if (GridSystem.Instance.IsValidPosition(pos))
                {
                    GridSystem.Instance.SetOccupied(pos, occupied);
                }
            }
        }

        private void UpdateLineRenderer()
        {
            if (_occupiedCells == null || _occupiedCells.Count == 0)
                return;

            CacheWorldPositions();
            _visualRenderer?.UpdateLineRenderer(_cellWorldPositions, _moveDirection);
            _colliderManager?.UpdateColliders(_occupiedCells);
        }

        private void UpdateLineRendererWithPositions(List<Vector2> worldPositions)
        {
            _visualRenderer?.UpdateLineRenderer(worldPositions, _moveDirection);
        }

        // ========== 에디터 전용 ==========
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_occupiedCells == null || GridSystem.Instance == null)
                return;

            Gizmos.color = UnityEngine.Color.cyan;
            foreach (var pos in _occupiedCells)
            {
                Vector2 worldPos = GridSystem.Instance.GridToWorld(pos);
                Gizmos.DrawWireCube(worldPos, Vector3.one * 0.8f);
            }

            Gizmos.color = UnityEngine.Color.yellow;
            Vector2 headWorld = GridSystem.Instance.GridToWorld(_headPosition);
            Gizmos.DrawWireSphere(headWorld, 0.3f);
            Gizmos.DrawLine(headWorld, headWorld + (Vector2)_moveDirection * 1.5f);
        }
#endif
    }
}