using UnityEngine;
using Game.Effects;
using Game.UI;
using DG.Tweening;

namespace Game.Core
{
    /// <summary>
    /// 카메라 컨트롤러 - 드래그 이동 및 핀치 줌
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("줌 설정")]
        [SerializeField] private float _minZoom = 3f;
        [SerializeField] private float _maxZoom = 20f;
        [SerializeField] private float _zoomSpeed = 0.5f;

        [Header("드래그 설정")]
        [SerializeField] private float _dragSpeed = 1f;
        [SerializeField] private float _dragThreshold = 10f;  // 드래그 인식 임계값 (픽셀)

        [Header("경계 설정")]
        [SerializeField] private float _boundaryPadding = 2f;
        [SerializeField] private float _verticalExtraPadding = 3f;  // 상하 추가 여유 (풍선 UI 영역 고려)
        [SerializeField] private float _minMoveRange = 2f;  // 경계가 카메라보다 작아도 허용되는 최소 이동 범위

        [Header("자동 크기 조절")]
        [SerializeField] private float _autoPadding = 1.5f;
        [SerializeField] private float _topMargin = 1.5f;  // 상단 풍선 영역 (줄임 - 더 많은 퍼즐 영역)
        [SerializeField] private bool _useSmoothTransition = true;
        [SerializeField] private float _smoothSpeed = 5f;

        [Header("카메라 크기 제한")]
        [SerializeField, Tooltip("자동 조절 시 최소 카메라 크기 (그리드가 작아도 이 크기 이하로 줄어들지 않음)")]
        private float _minCameraSize = 5f;

        // ========== 내부 상태 변수 ==========
        private Camera _camera;
        private Vector2 _dragStartPos;
        private Vector3 _cameraStartPos;
        private bool _isDragging;
        private bool _isPinching;
        private float _initialPinchDistance;
        private float _initialZoom;

        // 자동 크기 조절용
        private float _targetSize;
        private bool _isAutoTransitioning;

        // 드래그 판정용
        private bool _hasDragged;  // 실제로 드래그가 발생했는지 (임계값 초과)
        private float _totalDragDistance;

        // 카메라 드래그 경계 (월드 좌표)
        private Vector2 _worldBoundsMin;
        private Vector2 _worldBoundsMax;
        private bool _hasBounds;

        // Arrow Dash용 상태 저장
        private Vector3 _savedPosition;
        private float _savedOrthographicSize;
        private bool _hasSavedState;
        private bool _isArrowDashMode;  // Arrow Dash UI 활성화 상태

        // ========== 프로퍼티 ==========
        public bool IsDragging => _isDragging;
        public bool IsPinching => _isPinching;
        public bool IsInteracting => _isDragging || _isPinching;
        public bool HasDragged => _hasDragged;  // 탭과 드래그 구분용
        public Vector2 WorldBoundsMin => _worldBoundsMin;
        public Vector2 WorldBoundsMax => _worldBoundsMax;
        public bool HasBounds => _hasBounds;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        private void Update()
        {
            // 자동 크기 조절 전환
            if (_isAutoTransitioning && _camera != null)
            {
                _camera.orthographicSize = Mathf.Lerp(
                    _camera.orthographicSize,
                    _targetSize,
                    Time.deltaTime * _smoothSpeed
                );

                if (Mathf.Abs(_camera.orthographicSize - _targetSize) < 0.01f)
                {
                    _camera.orthographicSize = _targetSize;
                    _isAutoTransitioning = false;
                }
            }

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            HandleMouseInput();
#else
            HandleTouchInput();
#endif
        }

        // ========== 마우스 입력 (에디터/PC) ==========
        private void HandleMouseInput()
        {
            // 마우스 휠 줌
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                Zoom(-scroll * _zoomSpeed * 10f);
            }

            // 좌클릭(0), 우클릭(1), 중클릭(2) 모두 드래그 지원
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                StartDrag(Input.mousePosition);
            }
            else if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                UpdateDrag(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2))
            {
                EndDrag();
            }
        }

        // ========== 터치 입력 (모바일) ==========
        private void HandleTouchInput()
        {
            int touchCount = Input.touchCount;

            if (touchCount == 1)
            {
                // 핀치 중이었다면 종료
                if (_isPinching)
                {
                    EndPinch();
                }

                Touch touch = Input.GetTouch(0);

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        StartDrag(touch.position);
                        break;
                    case TouchPhase.Moved:
                        UpdateDrag(touch.position);
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        EndDrag();
                        break;
                }
            }
            else if (touchCount == 2)
            {
                // 드래그 중이었다면 종료
                if (_isDragging)
                {
                    EndDrag();
                }

                Touch touch0 = Input.GetTouch(0);
                Touch touch1 = Input.GetTouch(1);

                if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
                {
                    StartPinch(touch0.position, touch1.position);
                }
                else if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
                {
                    UpdatePinch(touch0.position, touch1.position);
                }
                else if (touch0.phase == TouchPhase.Ended || touch1.phase == TouchPhase.Ended)
                {
                    EndPinch();
                }
            }
            else
            {
                EndDrag();
                EndPinch();
            }
        }

        // ========== 드래그 처리 ==========
        private void StartDrag(Vector2 screenPos)
        {
            // Arrow Dash UI 사용 중에는 드래그 차단 (활성화 상태 또는 애니메이션 진행 중)
            if (_isArrowDashMode || (ArrowDashManager.Instance != null && ArrowDashManager.Instance.IsUIBusy))
            {
                return;
            }

            // Level Clear 연출 중에는 드래그 차단
            if (LevelClearManager.Instance != null && LevelClearManager.Instance.IsPlaying)
            {
                return;
            }

            // 실패 팝업 표시 중에는 드래그 차단
            if (PopupFailUI.Instance != null && PopupFailUI.Instance.IsShowing)
            {
                return;
            }

            _isDragging = true;
            _hasDragged = false;
            _totalDragDistance = 0f;
            _dragStartPos = screenPos;
            _cameraStartPos = transform.position;
        }

        private void UpdateDrag(Vector2 screenPos)
        {
            if (!_isDragging)
                return;

            // Level Clear 연출 중에는 드래그 차단
            if (LevelClearManager.Instance != null && LevelClearManager.Instance.IsPlaying)
            {
                EndDrag();
                return;
            }

            // 실패 팝업 표시 중에는 드래그 차단
            if (PopupFailUI.Instance != null && PopupFailUI.Instance.IsShowing)
            {
                EndDrag();
                return;
            }

            Vector2 delta = screenPos - _dragStartPos;
            _totalDragDistance = delta.magnitude;

            // 드래그 임계값 초과 시에만 실제 카메라 이동
            if (_totalDragDistance > _dragThreshold)
            {
                _hasDragged = true;
                Vector2 worldDelta = delta * _dragSpeed * _camera.orthographicSize / 500f;
                Vector3 newPos = _cameraStartPos - new Vector3(worldDelta.x, worldDelta.y, 0);
                transform.position = ClampPosition(newPos);
            }
        }

        private void EndDrag()
        {
            _isDragging = false;
            // HasDragged는 InputHandler가 체크할 때까지 유지
        }

        /// <summary>
        /// HasDragged 플래그 리셋 (InputHandler에서 호출)
        /// </summary>
        public void ResetDragState()
        {
            _hasDragged = false;
            _totalDragDistance = 0f;
        }

        // ========== 핀치 줌 처리 ==========
        private void StartPinch(Vector2 pos0, Vector2 pos1)
        {
            // Level Clear 연출 중에는 핀치 줌 차단
            if (LevelClearManager.Instance != null && LevelClearManager.Instance.IsPlaying)
            {
                return;
            }

            _isPinching = true;
            _initialPinchDistance = Vector2.Distance(pos0, pos1);
            _initialZoom = _camera.orthographicSize;
        }

        private void UpdatePinch(Vector2 pos0, Vector2 pos1)
        {
            if (!_isPinching)
                return;

            // Level Clear 연출 중에는 핀치 줌 차단
            if (LevelClearManager.Instance != null && LevelClearManager.Instance.IsPlaying)
            {
                EndPinch();
                return;
            }

            // 수동 줌 시 자동 전환 중단
            _isAutoTransitioning = false;

            float currentDistance = Vector2.Distance(pos0, pos1);
            if (_initialPinchDistance > 0)
            {
                float ratio = _initialPinchDistance / currentDistance;
                float newZoom = _initialZoom * ratio;
                _camera.orthographicSize = Mathf.Clamp(newZoom, _minZoom, _maxZoom);
            }
        }

        private void EndPinch()
        {
            _isPinching = false;
        }

        // ========== 줌 ==========
        private void Zoom(float delta)
        {
            // 수동 줌 시 자동 전환 중단
            _isAutoTransitioning = false;

            float newSize = _camera.orthographicSize + delta;
            _camera.orthographicSize = Mathf.Clamp(newSize, _minZoom, _maxZoom);
        }

        // ========== 유틸리티 ==========
        private Vector3 ClampPosition(Vector3 pos)
        {
            if (!_hasBounds || _camera == null)
                return pos;

            // 카메라 뷰포트 크기 계산
            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;

            // 경계 내에서 카메라 중심이 이동할 수 있는 범위 계산
            float minX = _worldBoundsMin.x + halfWidth;
            float maxX = _worldBoundsMax.x - halfWidth;
            float minY = _worldBoundsMin.y + halfHeight;
            float maxY = _worldBoundsMax.y - halfHeight;

            // 경계가 카메라보다 작아도 최소 이동 범위 보장
            if (minX > maxX)
            {
                float centerX = (_worldBoundsMin.x + _worldBoundsMax.x) * 0.5f;
                pos.x = Mathf.Clamp(pos.x, centerX - _minMoveRange, centerX + _minMoveRange);
            }
            else
            {
                pos.x = Mathf.Clamp(pos.x, minX, maxX);
            }

            if (minY > maxY)
            {
                float centerY = (_worldBoundsMin.y + _worldBoundsMax.y) * 0.5f;
                pos.y = Mathf.Clamp(pos.y, centerY - _minMoveRange, centerY + _minMoveRange);
            }
            else
            {
                pos.y = Mathf.Clamp(pos.y, minY, maxY);
            }

            return pos;
        }

        // ========== 공개 인터페이스 ==========
        public void ResetCamera()
        {
            transform.position = new Vector3(0, 0, transform.position.z);
            _camera.orthographicSize = 6f;
        }

        /// <summary>
        /// 그리드 크기에 맞춰 카메라 크기 자동 조절
        /// </summary>
        public void AdjustToGrid(int gridWidth, int gridHeight, float cellSize)
        {
            if (_camera == null)
                return;

            // 그리드 월드 크기 계산
            float gridWorldWidth = gridWidth * cellSize;
            float gridWorldHeight = gridHeight * cellSize;

            // 화면 비율 고려
            float aspectRatio = (float)Screen.width / Screen.height;

            // 세로 기준 필요 크기 (상단 마진 포함)
            float sizeForHeight = (gridWorldHeight / 2f) + _autoPadding + _topMargin;

            // 가로 기준 필요 크기
            float sizeForWidth = (gridWorldWidth / 2f) / aspectRatio + _autoPadding;

            // 둘 중 큰 값 선택 (최소 크기 보장)
            _targetSize = Mathf.Max(sizeForHeight, sizeForWidth, _minCameraSize);

            // 최대 줌 제한도 업데이트
            _maxZoom = Mathf.Max(_maxZoom, _targetSize + 5f);

            if (_useSmoothTransition)
            {
                _isAutoTransitioning = true;
            }
            else
            {
                _camera.orthographicSize = _targetSize;
            }

            // 카메라 위치도 중앙으로 리셋
            transform.position = new Vector3(0, 0, transform.position.z);

            // 드래그 경계 설정 (그리드 + 패딩)
            SetWorldBounds(gridWidth, gridHeight, cellSize);

            Debug.Log($"[CameraController] Adjusted to grid {gridWidth}x{gridHeight}, target size: {_targetSize:F1}");
        }

        /// <summary>
        /// 월드 경계 설정 (카메라 드래그 제한 및 화살표 탈출 기준)
        /// </summary>
        public void SetWorldBounds(int gridWidth, int gridHeight, float cellSize)
        {
            // 그리드 중심이 (0,0)이므로 경계 계산
            float halfWidth = (gridWidth * cellSize) * 0.5f;
            float halfHeight = (gridHeight * cellSize) * 0.5f;

            // 경계에 패딩 추가 (화살표가 화면 밖까지 나갈 공간)
            // 상하는 추가 여유 적용 (풍선 UI 영역 + 드래그 여유)
            float verticalPadding = _boundaryPadding + _verticalExtraPadding;
            _worldBoundsMin = new Vector2(-halfWidth - _boundaryPadding, -halfHeight - verticalPadding);
            _worldBoundsMax = new Vector2(halfWidth + _boundaryPadding, halfHeight + verticalPadding);
            _hasBounds = true;

            Debug.Log($"[CameraController] World bounds set: min={_worldBoundsMin}, max={_worldBoundsMax}");
        }

        /// <summary>
        /// 월드 좌표가 카메라 경계 밖인지 확인
        /// </summary>
        public bool IsOutOfWorldBounds(Vector2 worldPos)
        {
            if (!_hasBounds)
                return false;

            return worldPos.x < _worldBoundsMin.x || worldPos.x > _worldBoundsMax.x ||
                   worldPos.y < _worldBoundsMin.y || worldPos.y > _worldBoundsMax.y;
        }

        /// <summary>
        /// 월드 좌표가 현재 카메라 뷰포트 밖인지 확인
        /// </summary>
        public bool IsOutOfCameraView(Vector2 worldPos)
        {
            if (_camera == null)
                return false;

            Vector3 viewportPos = _camera.WorldToViewportPoint(worldPos);
            return viewportPos.x < 0f || viewportPos.x > 1f || viewportPos.y < 0f || viewportPos.y > 1f;
        }

        /// <summary>
        /// 현재 타겟 크기 반환
        /// </summary>
        public float GetTargetSize()
        {
            return _targetSize;
        }

        // ========== Arrow Dash 카메라 제어 ==========

        /// <summary>
        /// Arrow Dash 모드 여부
        /// </summary>
        public bool IsArrowDashMode => _isArrowDashMode;

        /// <summary>
        /// 현재 카메라 상태 저장
        /// </summary>
        public void SaveCurrentState()
        {
            if (_camera == null)
                return;

            _savedPosition = transform.position;
            _savedOrthographicSize = _camera.orthographicSize;
            _hasSavedState = true;

            Debug.Log($"[CameraController] State saved - Pos: {_savedPosition}, Size: {_savedOrthographicSize}");
        }

        /// <summary>
        /// 퍼즐 전체가 보이도록 줌 아웃 (Arrow Dash UI용)
        /// </summary>
        public void ZoomOutToShowAll(float duration = 0.3f)
        {
            if (_camera == null || !_hasBounds)
                return;

            // 현재 상태 저장
            SaveCurrentState();

            _isArrowDashMode = true;
            _isAutoTransitioning = false;  // 자동 전환 중단

            // 그리드 전체가 보이도록 필요한 카메라 크기 계산
            float boundsWidth = _worldBoundsMax.x - _worldBoundsMin.x;
            float boundsHeight = _worldBoundsMax.y - _worldBoundsMin.y;

            float aspectRatio = _camera.aspect;

            // 세로/가로 기준 필요 크기 중 큰 값 + 여유 공간
            float sizeForHeight = (boundsHeight / 2f) + 1f;
            float sizeForWidth = (boundsWidth / 2f) / aspectRatio + 1f;
            float targetSize = Mathf.Max(sizeForHeight, sizeForWidth);

            // 그리드 중심 위치 계산
            float centerX = (_worldBoundsMin.x + _worldBoundsMax.x) * 0.5f;
            float centerY = (_worldBoundsMin.y + _worldBoundsMax.y) * 0.5f;
            Vector3 targetPos = new Vector3(centerX, centerY, transform.position.z);

            // DOTween으로 부드럽게 전환
            transform.DOMove(targetPos, duration).SetEase(Ease.OutCubic);
            _camera.DOOrthoSize(targetSize, duration).SetEase(Ease.OutCubic);

            Debug.Log($"[CameraController] ZoomOutToShowAll - TargetSize: {targetSize}, TargetPos: {targetPos}");
        }

        /// <summary>
        /// 저장된 카메라 상태로 복원 (Arrow Dash UI 종료 시)
        /// </summary>
        public void RestoreState(float duration = 0.3f)
        {
            if (_camera == null || !_hasSavedState)
                return;

            _isArrowDashMode = false;

            // DOTween으로 부드럽게 복원
            transform.DOMove(_savedPosition, duration).SetEase(Ease.OutCubic);
            _camera.DOOrthoSize(_savedOrthographicSize, duration).SetEase(Ease.OutCubic);

            Debug.Log($"[CameraController] RestoreState - Pos: {_savedPosition}, Size: {_savedOrthographicSize}");
        }

        /// <summary>
        /// 저장된 상태가 있는지 확인
        /// </summary>
        public bool HasSavedState => _hasSavedState;
    }
}