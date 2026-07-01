using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Arrow;
using Game.Data;
using Game.UI;

namespace Game.Core
{
    /// <summary>
    /// Arrow Dash 시스템 관리
    /// 특정 방향의 모든 화살표를 충돌 무시하고 동시에 탈출시킴
    /// </summary>
    public class ArrowDashManager : MonoBehaviour
    {
        // ========== 싱글톤 ==========
        public static ArrowDashManager Instance { get; private set; }

        // ========== 인스펙터 노출 변수 ==========
        [Header("참조")]
        [SerializeField] private CameraController _cameraController;
        [SerializeField] private ArrowDashUI _arrowDashUI;

        // ========== 내부 상태 변수 ==========
        private List<ArrowController> _arrows;
        private bool _isActive;

        // ========== 이벤트 ==========
        /// <summary>
        /// 아이템 개수 변경 시 발생
        /// </summary>
        public event Action<int> OnItemCountChanged;

        /// <summary>
        /// Arrow Dash UI 열림/닫힘 시 발생
        /// </summary>
        public event Action<bool> OnUIStateChanged;

        /// <summary>
        /// Arrow Dash 실행 시 발생 (방향, 탈출한 화살표 수)
        /// </summary>
        public event Action<ArrowDirection, int> OnDashExecuted;

        // ========== 프로퍼티 ==========
        /// <summary>
        /// Arrow Dash UI가 활성화 상태인지
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// 현재 보유 아이템 개수
        /// </summary>
        public int ItemCount => ArrowDashData.GetCount();

        /// <summary>
        /// 아이템 사용 가능 여부
        /// </summary>
        public bool CanUseItem => ArrowDashData.HasItem();

        /// <summary>
        /// UI가 사용 중인지 (활성화 상태이거나 애니메이션 진행 중)
        /// 입력 차단 판단에 사용
        /// </summary>
        public bool IsUIBusy => _isActive || (_arrowDashUI != null && _arrowDashUI.IsAnimating);

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 화살표 리스트 초기화 (레벨 로드 시 호출)
        /// </summary>
        public void Initialize(List<ArrowController> arrows)
        {
            _arrows = arrows;
            Debug.Log($"[ArrowDashManager] Initialized with {arrows?.Count ?? 0} arrows");
        }

        /// <summary>
        /// Arrow Dash UI 열기
        /// </summary>
        public void OpenUI()
        {
            if (_isActive)
            {
                Debug.Log("[ArrowDashManager] UI already open");
                return;
            }

            if (!CanUseItem)
            {
                Debug.Log("[ArrowDashManager] No items available");
                // TODO: 아이템 없음 안내 UI 표시
                return;
            }

            _isActive = true;

            // 카메라 줌 아웃
            if (_cameraController != null)
            {
                _cameraController.ZoomOutToShowAll();
            }

            OnUIStateChanged?.Invoke(true);
            Debug.Log("[ArrowDashManager] UI opened");
        }

        /// <summary>
        /// Arrow Dash UI 닫기
        /// </summary>
        public void CloseUI()
        {
            if (!_isActive)
                return;

            _isActive = false;

            // 카메라 복원
            if (_cameraController != null)
            {
                _cameraController.RestoreState();
            }

            OnUIStateChanged?.Invoke(false);
            Debug.Log("[ArrowDashManager] UI closed");
        }

        /// <summary>
        /// 특정 방향의 화살표 목록 조회
        /// </summary>
        public List<ArrowController> GetArrowsByDirection(ArrowDirection direction)
        {
            if (_arrows == null)
                return new List<ArrowController>();

            return _arrows.Where(a =>
                a != null &&
                a.HeadDirection == direction &&
                a.CanLaunch
            ).ToList();
        }

        /// <summary>
        /// 특정 방향에 발사 가능한 화살표 개수
        /// </summary>
        public int GetArrowCountByDirection(ArrowDirection direction)
        {
            return GetArrowsByDirection(direction).Count;
        }

        /// <summary>
        /// Arrow Dash 실행
        /// </summary>
        /// <param name="direction">탈출시킬 화살표 방향</param>
        /// <returns>실행 성공 여부</returns>
        public bool ExecuteDash(ArrowDirection direction)
        {
            var targets = GetArrowsByDirection(direction);

            if (targets.Count == 0)
            {
                Debug.Log($"[ArrowDashManager] No arrows facing {direction}");
                return false;
            }

            // 아이템 소모
            if (!ArrowDashData.UseItem())
            {
                Debug.Log("[ArrowDashManager] Failed to use item");
                return false;
            }

            int newCount = ArrowDashData.GetCount();
            OnItemCountChanged?.Invoke(newCount);

            // 모든 대상 화살표 동시 탈출 (충돌 무시)
            Debug.Log($"[ArrowDashManager] Executing dash - Direction: {direction}, Count: {targets.Count}");

            foreach (var arrow in targets)
            {
                if (arrow != null && arrow.CanLaunch)
                {
                    arrow.LaunchWithoutCollision();
                }
            }

            OnDashExecuted?.Invoke(direction, targets.Count);

            // UI 종료
            CloseUI();

            return true;
        }

        /// <summary>
        /// 아이템 추가 (테스트/보상용)
        /// </summary>
        public void AddItem(int amount)
        {
            ArrowDashData.AddItem(amount);
            OnItemCountChanged?.Invoke(ArrowDashData.GetCount());
        }

        /// <summary>
        /// 아이템 개수 설정 (테스트/치트용)
        /// </summary>
        public void SetItemCount(int count)
        {
            ArrowDashData.SetCount(count);
            OnItemCountChanged?.Invoke(ArrowDashData.GetCount());
        }

        /// <summary>
        /// CameraController 참조 설정 (런타임 바인딩용)
        /// </summary>
        public void SetCameraController(CameraController cameraController)
        {
            _cameraController = cameraController;
        }
    }
}