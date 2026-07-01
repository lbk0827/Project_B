using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Utilities;

namespace Game.Grid
{
    /// <summary>
    /// 그리드 시스템 - 점(Dot) 기반 좌표 관리
    /// 씬 종속적 매니저 (DontDestroyOnLoad 사용 안 함)
    /// </summary>
    public class GridSystem : MonoBehaviour
    {
        // ========== 싱글톤 (씬 종속적) ==========
        private static GridSystem _instance;
        public static GridSystem Instance => _instance;

        // ========== 인스펙터 노출 변수 ==========
        [Header("그리드 설정")]
        [SerializeField] private int _gridWidth = 7;
        [SerializeField] private int _gridHeight = 9;
        [SerializeField] private float _cellSize = 1f;

        [Header("비주얼")]
        [SerializeField] private GameObject _dotPrefab;
        [SerializeField] private Color _dotColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);

        [Header("탈출 판정")]
        [SerializeField, Tooltip("바운딩 박스 외부로 확장되는 패딩 (칸 단위). 화살표가 이 영역을 벗어나야 탈출로 판정")]
        private int _boundingBoxPadding = 2;

        // ========== 내부 상태 변수 ==========
        private Vector2 _gridOrigin;
        private GameObject[,] _dots;
        private bool[,] _occupiedCells;
        private bool[,] _validCells;  // 유효한 셀 (레벨 데이터 기반)

        // Dot 풀링
        private List<GameObject> _dotPool = new List<GameObject>();
        private Dictionary<GameObject, SpriteRenderer> _dotSpriteCache = new Dictionary<GameObject, SpriteRenderer>();
        private Dictionary<GameObject, Vector3> _dotScaleCache = new Dictionary<GameObject, Vector3>();

        // 월드 바운딩 박스 (탈출 경계)
        private Vector2Int _boundingMin;
        private Vector2Int _boundingMax;
        private bool _hasBoundingBox;

        // ========== 프로퍼티 ==========
        public int GridWidth => _gridWidth;
        public int GridHeight => _gridHeight;
        public float CellSize => _cellSize;
        public Vector2 GridOrigin => _gridOrigin;
        public Vector2Int BoundingMin => _boundingMin;
        public Vector2Int BoundingMax => _boundingMax;
        public bool HasBoundingBox => _hasBoundingBox;
        public int BoundingBoxPadding => _boundingBoxPadding;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            // 씬 종속적 싱글톤 설정
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[GridSystem] Duplicate instance found, destroying...");
                Destroy(gameObject);
                return;
            }
            _instance = this;

            CalculateGridOrigin();
        }

        private void OnDestroy()
        {
            // 싱글톤 인스턴스 정리
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ========== 공개 인터페이스 ==========
        /// <summary>
        /// 그리드 초기화
        /// </summary>
        public void Initialize(int width, int height)
        {
            _gridWidth = width;
            _gridHeight = height;
            _occupiedCells = new bool[width, height];
            _validCells = new bool[width, height];
            _hasBoundingBox = false;

            CalculateGridOrigin();
            ClearDotVisuals();
            _dots = new GameObject[width, height];

            // 기본적으로 전체 그리드를 바운딩 박스로 설정
            SetFullGridAsBoundingBox();
        }

        /// <summary>
        /// 특정 위치에 Dot 표시
        /// </summary>
        public void ShowDotAt(Vector2Int gridPos)
        {
            if (!IsValidPosition(gridPos) || _dotPrefab == null)
                return;

            // 이미 Dot이 있으면 스킵
            if (_dots != null && _dots[gridPos.x, gridPos.y] != null)
                return;

            if (_dots == null)
                _dots = new GameObject[_gridWidth, _gridHeight];

            Vector2 worldPos = GridToWorld(gridPos);
            GameObject dot;
            SpriteRenderer sr;

            // 풀에서 가져오거나 새로 생성
            if (_dotPool.Count > 0)
            {
                dot = _dotPool[_dotPool.Count - 1];
                _dotPool.RemoveAt(_dotPool.Count - 1);
                dot.transform.position = worldPos;
                dot.SetActive(true);
                _dotSpriteCache.TryGetValue(dot, out sr);
                ResetDotScale(dot);
            }
            else
            {
                dot = Instantiate(_dotPrefab, worldPos, Quaternion.identity, transform);
                sr = dot.GetComponent<SpriteRenderer>();
                if (sr != null)
                    _dotSpriteCache[dot] = sr;
                _dotScaleCache[dot] = dot.transform.localScale;
            }

            if (sr != null)
            {
                sr.color = _dotColor;
            }

            _dots[gridPos.x, gridPos.y] = dot;
        }

        /// <summary>
        /// 특정 위치 Dot 숨기기
        /// </summary>
        public void HideDotAt(Vector2Int gridPos)
        {
            if (!IsValidPosition(gridPos) || _dots == null)
                return;

            if (_dots[gridPos.x, gridPos.y] != null)
            {
                ReturnDotToPool(_dots[gridPos.x, gridPos.y]);
                _dots[gridPos.x, gridPos.y] = null;
            }
        }

        /// <summary>
        /// 여러 위치에 Dot 표시
        /// </summary>
        public void ShowDotsAt(Vector2Int[] positions)
        {
            foreach (var pos in positions)
            {
                ShowDotAt(pos);
            }
        }

        /// <summary>
        /// 그리드 좌표를 월드 좌표로 변환
        /// </summary>
        public Vector2 GridToWorld(Vector2Int gridPos)
        {
            return new Vector2(
                _gridOrigin.x + gridPos.x * _cellSize,
                _gridOrigin.y + gridPos.y * _cellSize
            );
        }

        /// <summary>
        /// 월드 좌표를 그리드 좌표로 변환
        /// </summary>
        public Vector2Int WorldToGrid(Vector2 worldPos)
        {
            int x = Mathf.RoundToInt((worldPos.x - _gridOrigin.x) / _cellSize);
            int y = Mathf.RoundToInt((worldPos.y - _gridOrigin.y) / _cellSize);
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// 그리드 좌표가 유효한지 확인
        /// </summary>
        public bool IsValidPosition(Vector2Int gridPos)
        {
            return gridPos.x >= 0 && gridPos.x < _gridWidth &&
                   gridPos.y >= 0 && gridPos.y < _gridHeight;
        }

        /// <summary>
        /// 그리드 경계 밖인지 확인 (일반 그리드 경계)
        /// </summary>
        public bool IsOutOfBounds(Vector2Int gridPos)
        {
            return gridPos.x < 0 || gridPos.x >= _gridWidth ||
                   gridPos.y < 0 || gridPos.y >= _gridHeight;
        }

        /// <summary>
        /// 월드 바운딩 박스 밖인지 확인 (진짜 탈출 판정)
        /// 바운딩 박스가 설정되지 않았으면 일반 그리드 경계 사용
        /// 패딩이 적용되어 바운딩 박스 + 패딩 영역을 벗어나야 탈출로 판정
        /// </summary>
        public bool IsOutOfWorldBounds(Vector2Int gridPos)
        {
            if (!_hasBoundingBox)
            {
                // 패딩 적용한 일반 경계 체크
                return gridPos.x < -_boundingBoxPadding || gridPos.x >= _gridWidth + _boundingBoxPadding ||
                       gridPos.y < -_boundingBoxPadding || gridPos.y >= _gridHeight + _boundingBoxPadding;
            }

            // 패딩이 적용된 확장 바운딩 박스 체크
            return gridPos.x < _boundingMin.x - _boundingBoxPadding || gridPos.x > _boundingMax.x + _boundingBoxPadding ||
                   gridPos.y < _boundingMin.y - _boundingBoxPadding || gridPos.y > _boundingMax.y + _boundingBoxPadding;
        }

        /// <summary>
        /// 유효 셀 설정 (레벨 데이터 기반)
        /// </summary>
        public void SetValidCell(Vector2Int gridPos, bool valid)
        {
            if (IsValidPosition(gridPos))
            {
                if (_validCells == null)
                    _validCells = new bool[_gridWidth, _gridHeight];

                _validCells[gridPos.x, gridPos.y] = valid;
            }
        }

        /// <summary>
        /// 셀이 유효한지 확인
        /// </summary>
        public bool IsValidCell(Vector2Int gridPos)
        {
            if (!IsValidPosition(gridPos))
                return false;

            if (_validCells == null)
                return true;  // 유효 셀이 설정되지 않았으면 모든 셀 유효

            return _validCells[gridPos.x, gridPos.y];
        }

        /// <summary>
        /// 유효 셀 목록으로 바운딩 박스 계산
        /// </summary>
        public void CalculateBoundingBox(Vector2Int[] validPositions)
        {
            if (validPositions == null || validPositions.Length == 0)
            {
                _hasBoundingBox = false;
                return;
            }

            _boundingMin = new Vector2Int(int.MaxValue, int.MaxValue);
            _boundingMax = new Vector2Int(int.MinValue, int.MinValue);

            foreach (var pos in validPositions)
            {
                _boundingMin.x = Mathf.Min(_boundingMin.x, pos.x);
                _boundingMin.y = Mathf.Min(_boundingMin.y, pos.y);
                _boundingMax.x = Mathf.Max(_boundingMax.x, pos.x);
                _boundingMax.y = Mathf.Max(_boundingMax.y, pos.y);

                SetValidCell(pos, true);
            }

            _hasBoundingBox = true;
            Debug.Log($"[GridSystem] Bounding box calculated: min={_boundingMin}, max={_boundingMax}");
        }

        /// <summary>
        /// 그리드 전체를 바운딩 박스로 설정 (기본 직사각형 그리드)
        /// </summary>
        public void SetFullGridAsBoundingBox()
        {
            _boundingMin = Vector2Int.zero;
            _boundingMax = new Vector2Int(_gridWidth - 1, _gridHeight - 1);
            _hasBoundingBox = true;

            // 모든 셀을 유효로 설정
            _validCells = new bool[_gridWidth, _gridHeight];
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    _validCells[x, y] = true;
                }
            }

            Debug.Log($"[GridSystem] Full grid bounding box: min={_boundingMin}, max={_boundingMax}");
        }

        /// <summary>
        /// 셀 점유 상태 설정
        /// </summary>
        public void SetOccupied(Vector2Int gridPos, bool occupied)
        {
            if (IsValidPosition(gridPos))
            {
                _occupiedCells[gridPos.x, gridPos.y] = occupied;
            }
        }

        /// <summary>
        /// 셀이 점유되어 있는지 확인
        /// </summary>
        public bool IsOccupied(Vector2Int gridPos)
        {
            if (!IsValidPosition(gridPos))
                return true;

            return _occupiedCells[gridPos.x, gridPos.y];
        }

        /// <summary>
        /// 모든 점유 상태 초기화
        /// </summary>
        public void ClearAllOccupied()
        {
            if (_occupiedCells != null)
            {
                for (int x = 0; x < _gridWidth; x++)
                {
                    for (int y = 0; y < _gridHeight; y++)
                    {
                        _occupiedCells[x, y] = false;
                    }
                }
            }
        }

        /// <summary>
        /// 모든 활성 Dot 객체 반환 (Level Clear 이펙트용)
        /// </summary>
        /// <returns>그리드 좌표 → Dot GameObject 딕셔너리</returns>
        public System.Collections.Generic.Dictionary<Vector2Int, GameObject> GetAllDots()
        {
            var result = new System.Collections.Generic.Dictionary<Vector2Int, GameObject>();

            if (_dots == null)
                return result;

            for (int x = 0; x < _dots.GetLength(0); x++)
            {
                for (int y = 0; y < _dots.GetLength(1); y++)
                {
                    if (_dots[x, y] != null && _dots[x, y].activeInHierarchy)
                    {
                        result[new Vector2Int(x, y)] = _dots[x, y];
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 모든 Dot을 초기 상태로 복원 (Level Clear 이펙트 후)
        /// </summary>
        public void ResetAllDots()
        {
            if (_dots == null) return;

            for (int x = 0; x < _dots.GetLength(0); x++)
            {
                for (int y = 0; y < _dots.GetLength(1); y++)
                {
                    var dot = _dots[x, y];
                    if (dot != null)
                    {
                        dot.SetActive(true);
                        ResetDotScale(dot);

                        if (_dotSpriteCache.TryGetValue(dot, out var sr) && sr != null)
                        {
                            sr.color = _dotColor;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 방향에 따른 이동 벡터 반환
        /// </summary>
        public Vector2Int GetDirectionVector(ArrowDirection direction)
        {
            return direction switch
            {
                ArrowDirection.Up => Vector2Int.up,
                ArrowDirection.Down => Vector2Int.down,
                ArrowDirection.Left => Vector2Int.left,
                ArrowDirection.Right => Vector2Int.right,
                _ => Vector2Int.zero
            };
        }

        // ========== 내부 유틸리티 ==========
        private void CalculateGridOrigin()
        {
            _gridOrigin = new Vector2(
                -(_gridWidth - 1) * _cellSize * 0.5f,
                -(_gridHeight - 1) * _cellSize * 0.5f
            );
        }

        private void CreateDotVisuals()
        {
            ClearDotVisuals();

            if (_dotPrefab == null)
                return;

            _dots = new GameObject[_gridWidth, _gridHeight];

            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    Vector2 worldPos = GridToWorld(new Vector2Int(x, y));
                    var dot = Instantiate(_dotPrefab, worldPos, Quaternion.identity, transform);
                    dot.name = $"Dot_{x}_{y}";

                    var sr = dot.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.color = _dotColor;
                    }

                    _dots[x, y] = dot;
                }
            }
        }

        private void ClearDotVisuals()
        {
            if (_dots != null)
            {
                for (int x = 0; x < _dots.GetLength(0); x++)
                {
                    for (int y = 0; y < _dots.GetLength(1); y++)
                    {
                        if (_dots[x, y] != null)
                        {
                            ReturnDotToPool(_dots[x, y]);
                        }
                    }
                }
                _dots = null;
            }
        }

        private void ReturnDotToPool(GameObject dot)
        {
            ResetDotScale(dot);
            dot.SetActive(false);
            _dotPool.Add(dot);
        }

        private void ResetDotScale(GameObject dot)
        {
            if (dot == null) return;

            if (_dotScaleCache.TryGetValue(dot, out var cachedScale))
            {
                dot.transform.localScale = cachedScale;
                return;
            }

            // 캐시가 비어있는 예외 상황에서는 프리팹 기본 스케일을 사용
            dot.transform.localScale = _dotPrefab != null ? _dotPrefab.transform.localScale : Vector3.one;
            _dotScaleCache[dot] = dot.transform.localScale;
        }

        // ========== 에디터 전용 ==========
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            CalculateGridOrigin();

            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);

            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    Vector2 pos = GridToWorld(new Vector2Int(x, y));
                    Gizmos.DrawWireSphere(pos, 0.1f);
                }
            }

            // 그리드 경계 표시
            Gizmos.color = Color.yellow;
            Vector2 bottomLeft = GridToWorld(Vector2Int.zero) - Vector2.one * _cellSize * 0.5f;
            Vector2 topRight = GridToWorld(new Vector2Int(_gridWidth - 1, _gridHeight - 1)) + Vector2.one * _cellSize * 0.5f;
            Vector2 size = topRight - bottomLeft;
            Gizmos.DrawWireCube((bottomLeft + topRight) * 0.5f, size);
        }
#endif
    }
}
