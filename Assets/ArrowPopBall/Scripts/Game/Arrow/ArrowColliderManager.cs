using System.Collections.Generic;
using UnityEngine;
using Game.Grid;

namespace Game.Arrow
{
    /// <summary>
    /// 화살표 셀별 콜라이더 관리 - 풀링 기반
    /// ArrowController에서 분리됨
    /// </summary>
    public class ArrowColliderManager : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("필수 참조")]
        [SerializeField] private BoxCollider2D _mainCollider;

        // ========== 내부 상태 변수 ==========
        private List<BoxCollider2D> _cellColliders = new List<BoxCollider2D>();
        private List<BoxCollider2D> _colliderPool = new List<BoxCollider2D>();

        // ========== 유니티 라이프사이클 ==========
        private void OnDestroy()
        {
            foreach (var col in _cellColliders)
            {
                if (col != null) Destroy(col.gameObject);
            }
            _cellColliders.Clear();

            foreach (var col in _colliderPool)
            {
                if (col != null) Destroy(col.gameObject);
            }
            _colliderPool.Clear();
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 점유 셀 기반으로 콜라이더 배치 (풀링)
        /// </summary>
        public void UpdateColliders(List<Vector2Int> occupiedCells)
        {
            if (occupiedCells == null || occupiedCells.Count == 0)
                return;

            if (_mainCollider != null)
                _mainCollider.enabled = false;

            float cellSize = GridSystem.Instance.CellSize;
            Vector2 colliderSize = Vector2.one * cellSize * 0.9f;
            int needed = occupiedCells.Count;

            // 사용 중인 콜라이더를 풀로 반환
            for (int i = 0; i < _cellColliders.Count; i++)
            {
                if (_cellColliders[i] != null)
                {
                    _cellColliders[i].gameObject.SetActive(false);
                    _colliderPool.Add(_cellColliders[i]);
                }
            }
            _cellColliders.Clear();

            // 필요한 만큼 풀에서 가져오거나 새로 생성
            for (int i = 0; i < needed; i++)
            {
                Vector2 worldPos = GridSystem.Instance.GridToWorld(occupiedCells[i]);
                BoxCollider2D boxCol;

                if (_colliderPool.Count > 0)
                {
                    boxCol = _colliderPool[_colliderPool.Count - 1];
                    _colliderPool.RemoveAt(_colliderPool.Count - 1);
                    boxCol.gameObject.SetActive(true);
                    boxCol.transform.position = worldPos;
                    boxCol.size = colliderSize;
                }
                else
                {
                    var colliderObj = new GameObject("CellCollider");
                    colliderObj.transform.SetParent(transform);
                    colliderObj.transform.position = worldPos;
                    colliderObj.layer = gameObject.layer;
                    boxCol = colliderObj.AddComponent<BoxCollider2D>();
                    boxCol.size = colliderSize;
                }

                _cellColliders.Add(boxCol);
            }
        }

        /// <summary>
        /// 모든 셀 콜라이더를 풀로 반환 (비활성화)
        /// </summary>
        public void ClearCellColliders()
        {
            for (int i = 0; i < _cellColliders.Count; i++)
            {
                if (_cellColliders[i] != null)
                {
                    _cellColliders[i].gameObject.SetActive(false);
                    _colliderPool.Add(_cellColliders[i]);
                }
            }
            _cellColliders.Clear();
        }
    }
}
