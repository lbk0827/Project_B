using System.Collections.Generic;
using UnityEngine;
using Game.Data;

namespace Game.Editor.LevelEditor
{
    /// <summary>
    /// 퍼즐 생성 알고리즘 클래스
    /// Level Editor 리팩토링 - Phase 2
    ///
    /// 핵심 규칙 (절대 위반 금지):
    /// HEAD 직전 셀(cells[^2])에서 HEAD(cells[^1])로 가는 방향이 escapeDir와 동일해야 함
    /// → EnsureHeadAlignedWithDirection() 호출 필수!
    /// </summary>
    public class PuzzleGenerator
    {
        // ========== 생성 컨텍스트 ==========
        private int _gridWidth;
        private int _gridHeight;
        private bool[,] _shapeMask;
        private bool _useShapeMask;
        private GameColor[,] _colorMap;
        private bool _useColorMapping;
        private bool _useAutoColors;
        private bool[] _selectedColors;
        private int _minArrowLength;
        private int _maxArrowLength;
        private bool _useAutoLength;

        // ========== 의존성 ==========
        private EscapeValidator _escapeValidator;

        // ========== 통계 (분석용) ==========
        public int FailReason_NoEmptyCells { get; private set; }
        public int FailReason_TooShort { get; private set; }
        public int FailReason_NoEscapeDir { get; private set; }
        public int FailReason_HeadAlignment { get; private set; }
        public int EscapableCellsTotal { get; private set; }
        public int EscapableCellsFiltered { get; private set; }

        // ========== 초기화 ==========

        public PuzzleGenerator(EscapeValidator escapeValidator)
        {
            _escapeValidator = escapeValidator;
        }

        /// <summary>
        /// 생성 컨텍스트 설정
        /// </summary>
        public void SetContext(
            int gridWidth, int gridHeight,
            bool[,] shapeMask, bool useShapeMask,
            GameColor[,] colorMap, bool useColorMapping,
            bool useAutoColors, bool[] selectedColors,
            int minArrowLength, int maxArrowLength, bool useAutoLength)
        {
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            _shapeMask = shapeMask;
            _useShapeMask = useShapeMask;
            _colorMap = colorMap;
            _useColorMapping = useColorMapping;
            _useAutoColors = useAutoColors;
            _selectedColors = selectedColors;
            _minArrowLength = minArrowLength;
            _maxArrowLength = maxArrowLength;
            _useAutoLength = useAutoLength;
        }

        /// <summary>
        /// 통계 초기화
        /// </summary>
        public void ResetStatistics()
        {
            FailReason_NoEmptyCells = 0;
            FailReason_TooShort = 0;
            FailReason_NoEscapeDir = 0;
            FailReason_HeadAlignment = 0;
            EscapableCellsTotal = 0;
            EscapableCellsFiltered = 0;
        }

        // ========== 화살표 생성 ==========

        /// <summary>
        /// 길고 구불구불한 화살표 생성
        /// - 중앙에 가까운 시작점 우선
        /// - 꺾임 우선 성장
        /// - HEAD 정렬 보장 (EnsureHeadAlignedWithDirection 호출!)
        /// </summary>
        public EditorArrow TryCreateWindingArrow(bool[,] occupied, int id)
        {
            // 1. 빈 셀 중 탈출 가능한 셀만 필터링
            var emptyCells = GetEmptyCells(occupied);
            if (emptyCells.Count == 0)
            {
                FailReason_NoEmptyCells++;
                return null;
            }

            // 탈출 가능하고 인접 빈 셀이 있는 셀만 후보로 (성장 가능해야 함)
            var growableCells = new List<Vector2Int>();
            foreach (var cell in emptyCells)
            {
                if (_escapeValidator.HasAnyEscapeDirection(cell, occupied) && HasAdjacentEmptyCell(cell, occupied))
                {
                    growableCells.Add(cell);
                }
            }

            // 통계 기록
            EscapableCellsTotal += emptyCells.Count;
            EscapableCellsFiltered += growableCells.Count;

            // 성장 가능한 셀이 없으면 탈출 가능 셀, 그래도 없으면 일반 빈 셀 사용
            List<Vector2Int> candidateCells;
            if (growableCells.Count > 0)
            {
                candidateCells = growableCells;
            }
            else
            {
                var escapableCells = emptyCells.FindAll(c => _escapeValidator.HasAnyEscapeDirection(c, occupied));
                candidateCells = escapableCells.Count > 0 ? escapableCells : emptyCells;
            }
            Vector2Int startCell = SelectCellNearCenter(candidateCells);

            // 2. 구불구불하게 성장 (꺾임 우선)
            List<Vector2Int> cells = GrowWindingPath(startCell, occupied);

            if (cells.Count < 2)
            {
                FailReason_TooShort++;
                return null;
            }

            // 3. Head 위치 결정 - 더 내부에 있는 끝점을 Head로 선택
            Vector2Int firstCell = cells[0];
            Vector2Int lastCell = cells[^1];
            float firstDistToEdge = GridUtility.GetMinDistanceToEdge(firstCell, _gridWidth, _gridHeight, _shapeMask);
            float lastDistToEdge = GridUtility.GetMinDistanceToEdge(lastCell, _gridWidth, _gridHeight, _shapeMask);

            // 더 내부에 있는 쪽(경계에서 먼 쪽)을 Head로 시도
            if (firstDistToEdge > lastDistToEdge)
            {
                cells.Reverse();
            }

            Vector2Int head = cells[^1];
            ArrowDirection? escapeDir = _escapeValidator.FindValidEscapeDirection(head, cells, occupied);

            if (!escapeDir.HasValue)
            {
                // 탈출 방향 없음 - 반대쪽을 Head로 시도
                cells.Reverse();
                head = cells[^1];
                escapeDir = _escapeValidator.FindValidEscapeDirection(head, cells, occupied);

                if (!escapeDir.HasValue)
                {
                    FailReason_NoEscapeDir++;
                    return null;
                }
            }

            // 5. HEAD 직전 셀이 탈출 방향과 정렬되도록 보정 ★핵심★
            if (!EnsureHeadAlignedWithDirection(cells, escapeDir.Value, occupied))
            {
                // 정렬 실패 - 반대쪽(Tail)을 Head로 다시 시도
                cells.Reverse();
                head = cells[^1];
                escapeDir = _escapeValidator.FindValidEscapeDirection(head, cells, occupied);

                if (!escapeDir.HasValue || !EnsureHeadAlignedWithDirection(cells, escapeDir.Value, occupied))
                {
                    FailReason_HeadAlignment++;
                    return null;
                }
            }

            // 색상 결정
            GameColor arrowColor = DetermineArrowColor(cells);

            return new EditorArrow
            {
                id = id,
                cells = cells,
                headDirection = escapeDir.Value,
                color = arrowColor
            };
        }

        /// <summary>
        /// 그룹 내에서 긴 화살표 생성 시도
        /// </summary>
        public EditorArrow TryCreateArrowFromGroup(List<Vector2Int> group, bool[,] occupied, int id)
        {
            if (group.Count < _minArrowLength)
                return null;

            // 그룹 내에서 랜덤 시작점
            Vector2Int start = group[Random.Range(0, group.Count)];

            // 그룹 내에서만 성장하도록 임시 occupied 마스크 생성
            bool[,] groupMask = new bool[_gridWidth, _gridHeight];
            for (int x = 0; x < _gridWidth; x++)
                for (int y = 0; y < _gridHeight; y++)
                    groupMask[x, y] = occupied[x, y];

            // 그룹 외의 셀은 점유된 것으로 처리 (그룹 내에서만 이동)
            var groupSet = new HashSet<Vector2Int>(group);
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    if (!groupSet.Contains(new Vector2Int(x, y)))
                        groupMask[x, y] = true;
                }
            }

            // 그룹 내에서 성장
            List<Vector2Int> cells = GrowWindingPath(start, groupMask);

            if (cells.Count < 2)
                return null;

            // Head 결정 및 탈출 방향 찾기 (원본 occupied 기준)
            Vector2Int head = cells[^1];
            ArrowDirection? escapeDir = _escapeValidator.FindValidEscapeDirection(head, cells, occupied);

            if (!escapeDir.HasValue)
            {
                cells.Reverse();
                head = cells[^1];
                escapeDir = _escapeValidator.FindValidEscapeDirection(head, cells, occupied);
                if (!escapeDir.HasValue)
                    return null;
            }

            // HEAD 정렬 보정 ★핵심★
            if (!EnsureHeadAlignedWithDirection(cells, escapeDir.Value, occupied))
            {
                cells.Reverse();
                head = cells[^1];
                escapeDir = _escapeValidator.FindValidEscapeDirection(head, cells, occupied);
                if (!escapeDir.HasValue || !EnsureHeadAlignedWithDirection(cells, escapeDir.Value, occupied))
                    return null;
            }

            GameColor arrowColor = DetermineArrowColor(cells);

            return new EditorArrow
            {
                id = id,
                cells = cells,
                headDirection = escapeDir.Value,
                color = arrowColor
            };
        }

        // ========== HEAD 정렬 (핵심 규칙) ==========

        /// <summary>
        /// Head 직전 셀이 탈출 방향과 정렬되도록 셀 추가
        /// 규칙: Head 직전 셀 → Head 연결 방향 = headDirection
        ///
        /// ★★★ 이 함수는 모든 화살표 생성 시 반드시 호출해야 함! ★★★
        /// </summary>
        public bool EnsureHeadAlignedWithDirection(List<Vector2Int> cells, ArrowDirection headDir, bool[,] occupied)
        {
            if (cells.Count < 2) return true; // 1칸짜리는 처리 불필요

            Vector2Int head = cells[^1];
            Vector2Int prevCell = cells[^2];
            ArrowDirection lastMoveDir = GridUtility.GetDirectionFromTo(prevCell, head);

            // 이미 정렬되어 있으면 OK
            if (lastMoveDir == headDir)
                return true;

            // 최대 길이에 도달했으면 셀 추가 불가
            if (cells.Count >= _maxArrowLength)
                return false;

            // 정렬되어 있지 않으면, headDir 방향으로 셀 하나 추가 시도
            Vector2Int newHead = head + GridUtility.GetDirectionVector(headDir);

            // 새 셀이 유효한지 검사 (그리드 내, 비어있음, 이미 cells에 없음)
            if (GridUtility.IsValidCell(newHead, _gridWidth, _gridHeight, _shapeMask) &&
                !occupied[newHead.x, newHead.y] &&
                !cells.Contains(newHead))
            {
                cells.Add(newHead);
                return true;
            }

            // 셀 추가 불가 - 실패
            return false;
        }

        // ========== 경로 성장 ==========

        /// <summary>
        /// 구불구불한 경로 성장 (꺾임 우선)
        /// </summary>
        public List<Vector2Int> GrowWindingPath(Vector2Int start, bool[,] occupied)
        {
            List<Vector2Int> cells = new List<Vector2Int> { start };
            Vector2Int current = start;

            int targetLength = Random.Range(_minArrowLength, _maxArrowLength + 1);

            ArrowDirection? lastDir = null;
            int bendProbability = 70; // 70% 확률로 꺾임 시도

            while (cells.Count < targetLength)
            {
                ArrowDirection nextDir;

                if (lastDir.HasValue && Random.Range(0, 100) >= bendProbability)
                {
                    nextDir = lastDir.Value;
                }
                else
                {
                    nextDir = GetRandomBendDirection(lastDir);
                }

                Vector2Int next = current + GridUtility.GetDirectionVector(nextDir);

                // 유효성 검사
                if (!GridUtility.IsValidCell(next, _gridWidth, _gridHeight, _shapeMask) ||
                    occupied[next.x, next.y] || cells.Contains(next))
                {
                    bool found = false;
                    foreach (var dir in GridUtility.GetShuffledDirections())
                    {
                        if (lastDir.HasValue && dir == GridUtility.GetOppositeDirection(lastDir.Value))
                            continue;

                        Vector2Int tryNext = current + GridUtility.GetDirectionVector(dir);
                        if (GridUtility.IsValidCell(tryNext, _gridWidth, _gridHeight, _shapeMask) &&
                            !occupied[tryNext.x, tryNext.y] && !cells.Contains(tryNext))
                        {
                            nextDir = dir;
                            next = tryNext;
                            found = true;
                            break;
                        }
                    }

                    if (!found) break;
                }

                cells.Add(next);
                current = next;
                lastDir = nextDir;
            }

            return cells;
        }

        /// <summary>
        /// 랜덤 꺾임 방향 선택 (이전 방향의 반대 제외)
        /// </summary>
        private ArrowDirection GetRandomBendDirection(ArrowDirection? lastDir)
        {
            var dirs = GridUtility.GetShuffledDirections();

            if (lastDir.HasValue)
            {
                dirs.Remove(GridUtility.GetOppositeDirection(lastDir.Value));

                var perpendicular = GridUtility.GetPerpendicularDirections(lastDir.Value);
                if (perpendicular.Count > 0 && Random.Range(0, 100) < 80)
                {
                    return perpendicular[Random.Range(0, perpendicular.Count)];
                }
            }

            return dirs.Count > 0 ? dirs[0] : ArrowDirection.Up;
        }

        // ========== 셀 그룹화 ==========

        /// <summary>
        /// 빈 셀들을 FloodFill로 연결된 그룹으로 분류
        /// </summary>
        public List<List<Vector2Int>> GroupEmptyCells(bool[,] occupied)
        {
            var groups = new List<List<Vector2Int>>();
            bool[,] visited = new bool[_gridWidth, _gridHeight];

            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    if (occupied[x, y] || visited[x, y]) continue;

                    var group = new List<Vector2Int>();
                    var queue = new Queue<Vector2Int>();
                    queue.Enqueue(new Vector2Int(x, y));
                    visited[x, y] = true;

                    while (queue.Count > 0)
                    {
                        var cell = queue.Dequeue();
                        group.Add(cell);

                        foreach (var dir in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                        {
                            var next = cell + dir;
                            if (next.x >= 0 && next.x < _gridWidth &&
                                next.y >= 0 && next.y < _gridHeight &&
                                !occupied[next.x, next.y] && !visited[next.x, next.y])
                            {
                                visited[next.x, next.y] = true;
                                queue.Enqueue(next);
                            }
                        }
                    }

                    if (group.Count > 0)
                        groups.Add(group);
                }
            }

            // 큰 그룹 먼저 처리
            groups.Sort((a, b) => b.Count.CompareTo(a.Count));
            return groups;
        }

        // ========== 유틸리티 ==========

        private List<Vector2Int> GetEmptyCells(bool[,] occupied)
        {
            var result = new List<Vector2Int>();
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    if (!occupied[x, y])
                        result.Add(new Vector2Int(x, y));
                }
            }
            return result;
        }

        private Vector2Int SelectCellNearCenter(List<Vector2Int> cells)
        {
            if (cells.Count == 0) return Vector2Int.zero;
            if (cells.Count == 1) return cells[0];

            Vector2 center = new Vector2(_gridWidth / 2f, _gridHeight / 2f);

            var sorted = new List<Vector2Int>(cells);
            sorted.Sort((a, b) =>
                Vector2.Distance(a, center).CompareTo(Vector2.Distance(b, center)));

            int topCount = Mathf.Max(1, sorted.Count / 3);
            return sorted[Random.Range(0, topCount)];
        }

        private bool HasAdjacentEmptyCell(Vector2Int cell, bool[,] occupied)
        {
            foreach (var dir in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                Vector2Int neighbor = cell + dir;
                if (GridUtility.IsValidCell(neighbor, _gridWidth, _gridHeight, _shapeMask) &&
                    !occupied[neighbor.x, neighbor.y])
                {
                    return true;
                }
            }
            return false;
        }

        private GameColor DetermineArrowColor(List<Vector2Int> cells)
        {
            var availableColors = ColorManager.GetAvailableColors(_useAutoColors, _selectedColors, _gridWidth, _gridHeight);

            if (_useColorMapping && _colorMap != null)
            {
                return ColorManager.GetDominantColorForCells(cells, _colorMap, _gridWidth, _gridHeight, availableColors);
            }

            return ColorManager.GetRandomColor(availableColors);
        }

        /// <summary>
        /// 1칸 화살표의 탈출 방향 찾기
        /// </summary>
        public ArrowDirection FindEscapeDirectionForOneCell(Vector2Int cell, bool[,] occupied)
        {
            foreach (ArrowDirection dir in new[] { ArrowDirection.Up, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Right })
            {
                if (_escapeValidator.CanEscapeToDirection(cell, dir, new List<Vector2Int> { cell }, occupied))
                {
                    return dir;
                }
            }
            return ArrowDirection.Up;
        }
    }
}
