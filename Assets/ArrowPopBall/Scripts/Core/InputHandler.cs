using System;
using UnityEngine;
using Game.Arrow;
using Game.Effects;
using Game.UI;

namespace Game.Core
{
    /// <summary>
    /// 입력 핸들러 - 탭으로 화살표 발사
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("설정값")]
        [SerializeField] private float _tapThreshold = 10f;  // 탭 판정 거리 (픽셀)
        [SerializeField] private float _tapTimeThreshold = 0.3f;  // 탭 판정 시간 (초)
        [SerializeField] private LayerMask _arrowLayer;

        [Header("참조")]
        [SerializeField] private CameraController _cameraController;

        // ========== 내부 상태 변수 ==========
        private Camera _mainCamera;
        private Vector2 _pointerDownPos;
        private float _pointerDownTime;
        private bool _isPointerDown;
        private ArrowController _pendingArrow;  // Down 시점에 감지된 화살표

        // ========== 이벤트 ==========
        public event Action<ArrowController> OnArrowTapped;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        private void Update()
        {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            HandleMouseInput();
#else
            HandleTouchInput();
#endif
        }

        // ========== 마우스 입력 ==========
        private void HandleMouseInput()
        {
            // 좌클릭만 탭으로 처리 (우클릭은 카메라 이동)
            if (Input.GetMouseButtonDown(0))
            {
                OnPointerDown(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                OnPointerUp(Input.mousePosition);
            }
        }

        // ========== 터치 입력 ==========
        private void HandleTouchInput()
        {
            // 한 손가락 터치만 처리 (두 손가락은 카메라 줌)
            if (Input.touchCount != 1)
            {
                _isPointerDown = false;
                return;
            }

            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    OnPointerDown(touch.position);
                    break;
                case TouchPhase.Ended:
                    OnPointerUp(touch.position);
                    break;
                case TouchPhase.Canceled:
                    _isPointerDown = false;
                    break;
            }
        }

        // ========== 포인터 처리 ==========
        private void OnPointerDown(Vector2 screenPos)
        {
            // Arrow Dash UI 사용 중에는 입력 차단 (활성화 상태 또는 애니메이션 진행 중)
            if (ArrowDashManager.Instance != null && ArrowDashManager.Instance.IsUIBusy)
            {
                return;
            }

            // Level Clear 연출 중에는 입력 차단
            if (LevelClearManager.Instance != null && LevelClearManager.Instance.IsPlaying)
            {
                return;
            }

            // 실패 팝업 표시 중에는 입력 차단
            if (PopupFailUI.Instance != null && PopupFailUI.Instance.IsShowing)
            {
                return;
            }

            _isPointerDown = true;
            _pointerDownPos = screenPos;
            _pointerDownTime = Time.time;
            _pendingArrow = null;

            // Down 시점에 화살표 위인지 체크
            Vector2 worldPos = _mainCamera.ScreenToWorldPoint(screenPos);
            RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero, 0f, _arrowLayer);

            if (hit.collider != null)
            {
                ArrowController arrow = hit.collider.GetComponent<ArrowController>();
                if (arrow == null)
                {
                    arrow = hit.collider.GetComponentInParent<ArrowController>();
                }

                if (arrow != null && arrow.CanLaunch)
                {
                    _pendingArrow = arrow;
                }
            }
        }

private void OnPointerUp(Vector2 screenPos)
        {
            if (!_isPointerDown)
                return;

            _isPointerDown = false;

            // 드래그가 발생했으면 화살표 발사 취소
            if (_cameraController != null)
            {
                if (_cameraController.HasDragged)
                {
                    _cameraController.ResetDragState();
                    _pendingArrow = null;
                    return;
                }
                _cameraController.ResetDragState();
            }

            // 드래그 없이 뗐고, Down 시점에 화살표가 있었으면 발사
            if (_pendingArrow != null && _pendingArrow.CanLaunch)
            {
                // Eraser 선택 모드 중이면 강제 탈출 실행
                if (EraserManager.Instance != null && EraserManager.Instance.IsSelectMode)
                {
                    EraserManager.Instance.ExecuteErase(_pendingArrow);
                }
                else
                {
                    _pendingArrow.Launch();
                    SFXManager.Instance?.PlayBalloonPick();
                    OnArrowTapped?.Invoke(_pendingArrow);
                }
            }
            else if (_pendingArrow == null)
            {
                // 화살표 없는 곳 탭 → Eraser 선택 모드 취소
                if (EraserManager.Instance != null && EraserManager.Instance.IsSelectMode)
                {
                    EraserManager.Instance.CancelSelectMode();
                }
            }

            _pendingArrow = null;
        }
    }
}