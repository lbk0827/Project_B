using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Arrow;
using Game.Data;
using Game.Grid;

namespace Game.Core
{
    /// <summary>
    /// Eraser 부스터 시스템 관리
    /// 선택 모드 진입 → 화살표 선택 → 강제 탈출
    /// </summary>
    public class EraserManager : MonoBehaviour
    {
        // ========== 싱글톤 ==========
        public static EraserManager Instance { get; private set; }

        // ========== 내부 상태 변수 ==========
        private List<ArrowController> _arrows;
        private bool _isSelectMode;

        // ========== 이벤트 ==========
        public event Action<int> OnItemCountChanged;
        public event Action<bool> OnSelectModeChanged;

        // ========== 프로퍼티 ==========
        public int ItemCount => BoosterData.GetCount("Eraser");
        public bool CanUseItem => BoosterData.HasItem("Eraser");
        public bool IsSelectMode => _isSelectMode;

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
                Instance = null;
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 화살표 리스트 초기화 (레벨 로드 시 호출)
        /// </summary>
        public void Initialize(List<ArrowController> arrows)
        {
            _arrows = arrows;
            CancelSelectMode();
            Debug.Log($"[EraserManager] Initialized with {arrows?.Count ?? 0} arrows");
        }

        /// <summary>
        /// 선택 모드 진입 - 화살표 탭 시 강제 탈출 실행
        /// </summary>
        public void EnterSelectMode()
        {
            if (!CanUseItem)
            {
                Debug.Log("[EraserManager] No eraser items available");
                return;
            }

            if (_isSelectMode)
            {
                CancelSelectMode();
                return;
            }

            _isSelectMode = true;
            OnSelectModeChanged?.Invoke(true);
            Debug.Log("[EraserManager] Select mode entered");
        }

        /// <summary>
        /// 선택 모드 취소
        /// </summary>
        public void CancelSelectMode()
        {
            if (!_isSelectMode)
                return;

            _isSelectMode = false;
            OnSelectModeChanged?.Invoke(false);
            Debug.Log("[EraserManager] Select mode cancelled");
        }

        /// <summary>
        /// 선택한 화살표 강제 탈출 실행
        /// </summary>
        /// <param name="arrow">대상 화살표 (Idle 상태만 허용)</param>
        /// <returns>실행 성공 여부</returns>
        public bool ExecuteErase(ArrowController arrow)
        {
            if (!_isSelectMode)
            {
                Debug.Log("[EraserManager] Not in select mode");
                return false;
            }

            if (arrow == null || !arrow.CanLaunch)
            {
                Debug.Log("[EraserManager] Arrow is not in a launchable state");
                return false;
            }

            if (!BoosterData.UseItem("Eraser"))
            {
                Debug.Log("[EraserManager] Failed to use eraser item");
                return false;
            }

            OnItemCountChanged?.Invoke(ItemCount);

            // 선택 모드 종료
            _isSelectMode = false;
            OnSelectModeChanged?.Invoke(false);

            // 충돌 무시 강제 탈출
            arrow.LaunchWithoutCollision();

            Debug.Log($"[EraserManager] Eraser executed on arrow at {arrow.HeadPosition}");
            return true;
        }

        /// <summary>
        /// 아이템 추가 (테스트/보상용)
        /// </summary>
        public void AddItem(int amount)
        {
            BoosterData.AddItem("Eraser", amount);
            OnItemCountChanged?.Invoke(ItemCount);
        }
    }
}
