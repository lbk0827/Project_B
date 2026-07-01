using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Arrow;
using Game.Data;
using Game.Grid;

namespace Game.Core
{
    /// <summary>
    /// Hint 부스터 시스템 관리
    /// 탈출 가능 화살표 탐색 후 하이라이트 연출
    /// </summary>
    public class HintManager : MonoBehaviour
    {
        // ========== 싱글톤 ==========
        public static HintManager Instance { get; private set; }

        // ========== 인스펙터 노출 변수 ==========
        [Header("힌트 설정")]
        [SerializeField] private float _autoHideDuration = 5f;

        // ========== 내부 상태 변수 ==========
        private List<ArrowController> _arrows;
        private ArrowController _highlightedArrow;
        private Coroutine _autoHideCoroutine;

        // ========== 이벤트 ==========
        public event System.Action<int> OnItemCountChanged;

        // ========== 프로퍼티 ==========
        public int ItemCount => BoosterData.GetCount("Hint");
        public bool CanUseItem => BoosterData.HasItem("Hint");

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
            // 기존 화살표 이벤트 해제
            if (_arrows != null)
            {
                foreach (var arrow in _arrows)
                {
                    if (arrow != null)
                        arrow.OnMoveStarted -= OnAnyArrowMoveStarted;
                }
            }

            _arrows = arrows;
            ClearHighlight();

            // 화살표 이동 시작 이벤트 구독 (하이라이트 자동 해제)
            if (_arrows != null)
            {
                foreach (var arrow in _arrows)
                {
                    if (arrow != null)
                        arrow.OnMoveStarted += OnAnyArrowMoveStarted;
                }
            }

            Debug.Log($"[HintManager] Initialized with {arrows?.Count ?? 0} arrows");
        }

        /// <summary>
        /// 힌트 실행 - 탈출 가능 화살표 탐색 → 하이라이트
        /// </summary>
        /// <returns>실행 성공 여부</returns>
        public bool ExecuteHint()
        {
            if (!CanUseItem)
            {
                Debug.Log("[HintManager] No hint items available");
                return false;
            }

            var target = FindBestEscapableArrow();
            if (target == null)
            {
                Debug.Log("[HintManager] No escapable arrow found");
                return false;
            }

            if (!BoosterData.UseItem("Hint"))
                return false;

            OnItemCountChanged?.Invoke(ItemCount);

            ClearHighlight();
            _highlightedArrow = target;
            target.SetHighlight(true);

            if (_autoHideCoroutine != null)
                StopCoroutine(_autoHideCoroutine);
            _autoHideCoroutine = StartCoroutine(AutoHideCoroutine());

            Debug.Log($"[HintManager] Hint executed, highlighting arrow at {target.HeadPosition}");
            return true;
        }

        /// <summary>
        /// 하이라이트 즉시 해제 (화살표 탭 시, 레벨 종료 시 호출)
        /// </summary>
        public void ClearHighlight()
        {
            if (_highlightedArrow != null)
            {
                _highlightedArrow.SetHighlight(false);
                _highlightedArrow = null;
            }

            if (_autoHideCoroutine != null)
            {
                StopCoroutine(_autoHideCoroutine);
                _autoHideCoroutine = null;
            }
        }

        /// <summary>
        /// 아이템 추가 (테스트/보상용)
        /// </summary>
        public void AddItem(int amount)
        {
            BoosterData.AddItem("Hint", amount);
            OnItemCountChanged?.Invoke(ItemCount);
        }

        // ========== 내부 유틸리티 ==========

        /// <summary>
        /// 탈출까지 가장 거리가 짧은 화살표 반환 (없으면 null)
        /// </summary>
        private ArrowController FindBestEscapableArrow()
        {
            if (_arrows == null)
                return null;

            ArrowController best = null;
            int minSteps = int.MaxValue;

            foreach (var arrow in _arrows)
            {
                if (arrow == null || !arrow.CanLaunch)
                    continue;

                int steps = GetStepsToEscape(arrow);
                if (steps >= 0 && steps < minSteps)
                {
                    minSteps = steps;
                    best = arrow;
                }
            }

            return best;
        }

        /// <summary>
        /// 화살표가 탈출 가능하면 경계까지 걸리는 칸 수 반환, 불가능하면 -1
        /// 경로 상에 다른 화살표 셀이 없으면 탈출 가능으로 판정
        /// </summary>
        private int GetStepsToEscape(ArrowController arrow)
        {
            Vector2Int pos = arrow.HeadPosition;
            Vector2Int dir = GridSystem.Instance.GetDirectionVector(arrow.HeadDirection);

            // 자기 자신의 셀은 경로 체크에서 제외
            var ownCells = new HashSet<Vector2Int>(arrow.GetOccupiedPositions());

            int steps = 0;
            pos += dir;

            while (!GridSystem.Instance.IsOutOfWorldBounds(pos))
            {
                if (!ownCells.Contains(pos) && GridSystem.Instance.IsOccupied(pos))
                    return -1; // 다른 화살표가 경로를 막고 있음

                steps++;
                pos += dir;
            }

            return steps;
        }

        private void OnAnyArrowMoveStarted(ArrowController arrow)
        {
            ClearHighlight();
        }

        
private IEnumerator AutoHideCoroutine()
        {
            yield return new WaitForSeconds(_autoHideDuration);
            ClearHighlight();
        }
    }
}
