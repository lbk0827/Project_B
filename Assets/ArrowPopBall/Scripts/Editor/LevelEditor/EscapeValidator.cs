using System.Collections.Generic;
using UnityEngine;
using Game.Data;

namespace Game.Editor.LevelEditor
{
    /// <summary>
    /// 화살표 탈출 가능성 검증 클래스
    /// Level Editor 리팩토링 - Phase 2
    ///
    /// 핵심 규칙: HEAD 직전 셀(cells[^2])에서 HEAD(cells[^1])로 가는 방향이 escapeDir와 동일해야 함
    /// </summary>
    public class EscapeValidator
    {
        // ========== 그리드 컨텍스트 ==========
        private int _gridWidth;
        private int _gridHeight;
        private bool[,] _shapeMask;
        private bool _useShapeMask;

        // ========== 초기화 ==========

        /// <summary>
        /// 그리드 컨텍스트 설정
        /// </summary>
        public void SetContext(int gridWidth, int gridHeight, bool[,] shapeMask, bool useShapeMask)
        {
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            _shapeMask = shapeMask;
            _useShapeMask = useShapeMask;
        }

        // ========== 퍼즐 풀이 시뮬레이션 ==========

        /// <summary>
        /// 현재 퍼즐이 풀리는지 시뮬레이션
        /// </summary>
        /// <returns>(성공여부, 풀이순서, 막힌화살표ID)</returns>
        public (bool success, List<int> order, int blockedArrowId) SimulateSolve(List<EditorArrow> arrows)
        {
            // 화살표 복사본 생성
            var remaining = new List<EditorArrow>();
            foreach (var arrow in arrows)
            {
                remaining.Add(new EditorArrow
                {
                    id = arrow.id,
                    cells = new List<Vector2Int>(arrow.cells),
                    headDirection = arrow.headDirection,
                    color = arrow.color
                });
            }

            var solveOrder = new List<int>();
            int maxIterations = remaining.Count * remaining.Count; // 무한 루프 방지
            int iterations = 0;

            while (remaining.Count > 0 && iterations < maxIterations)
            {
                iterations++;
                bool foundEscapable = false;

                for (int i = 0; i < remaining.Count; i++)
                {
                    var arrow = remaining[i];
                    if (CanEscape(arrow, remaining))
                    {
                        solveOrder.Add(arrow.id);
                        remaining.RemoveAt(i);
                        foundEscapable = true;
                        break;
                    }
                }

                if (!foundEscapable)
                {
                    // Deadlock - 탈출 불가능한 화살표 찾기
                    int blockedId = remaining.Count > 0 ? remaining[0].id : -1;
                    return (false, solveOrder, blockedId);
                }
            }

            return (true, solveOrder, -1);
        }

        /// <summary>
        /// 화살표가 탈출 가능한지 확인
        /// </summary>
        public bool CanEscape(EditorArrow arrow, List<EditorArrow> allArrows)
        {
            if (arrow.cells.Count == 0) return true;

            Vector2Int head = arrow.cells[^1];
            Vector2Int dir = GridUtility.GetDirectionVector(arrow.headDirection);

            // Head 방향으로 이동하면서 경계까지 갈 수 있는지 체크
            Vector2Int current = head;

            while (true)
            {
                current += dir;

                // 그리드 밖으로 나가면 탈출 성공
                if (current.x < 0 || current.x >= _gridWidth ||
                    current.y < 0 || current.y >= _gridHeight)
                {
                    return true;
                }

                // 마스크 사용 시: 마스크 외부로 나가면 탈출 성공
                if (_useShapeMask && _shapeMask != null && !_shapeMask[current.x, current.y])
                {
                    return true;
                }

                // 다른 화살표에 막혀 있는지 체크
                foreach (var other in allArrows)
                {
                    if (other.id == arrow.id) continue;
                    if (other.cells.Contains(current))
                    {
                        return false; // 막혀있음
                    }
                }
            }
        }

        // ========== 탈출 방향 검증 ==========

        /// <summary>
        /// 특정 셀에서 어떤 방향으로든 탈출 가능한지 확인
        /// </summary>
        public bool HasAnyEscapeDirection(Vector2Int cell, bool[,] occupied)
        {
            foreach (ArrowDirection dir in new[] { ArrowDirection.Up, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Right })
            {
                var singleCell = new List<Vector2Int> { cell };
                if (CanEscapeToDirection(cell, dir, singleCell, occupied))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 유효한 탈출 방향 찾기
        /// - 자신의 몸통에 막히지 않음
        /// - 기존 화살표에 막히지 않음
        /// 경계에서 가장 먼 방향 우선 (내부 배치 유도)
        /// </summary>
        public ArrowDirection? FindValidEscapeDirection(Vector2Int head, List<Vector2Int> arrowCells, bool[,] occupied)
        {
            var candidates = new List<(ArrowDirection dir, int distance)>();

            foreach (ArrowDirection dir in new[] { ArrowDirection.Up, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Right })
            {
                if (CanEscapeToDirection(head, dir, arrowCells, occupied))
                {
                    int distance = GridUtility.GetDistanceToBoundary(head, dir, _gridWidth, _gridHeight, _shapeMask);
                    candidates.Add((dir, distance));
                }
            }

            if (candidates.Count == 0)
                return null;

            // 경계에서 가장 먼 방향 선택 (내부 배치 유도)
            candidates.Sort((a, b) => b.distance.CompareTo(a.distance));
            return candidates[0].dir;
        }

        /// <summary>
        /// 특정 방향으로 경계까지 탈출 가능한지 확인
        /// </summary>
        public bool CanEscapeToDirection(Vector2Int head, ArrowDirection dir, List<Vector2Int> arrowCells, bool[,] occupied)
        {
            Vector2Int dirVec = GridUtility.GetDirectionVector(dir);
            Vector2Int current = head + dirVec;

            while (current.x >= 0 && current.x < _gridWidth &&
                   current.y >= 0 && current.y < _gridHeight)
            {
                // 마스크 사용 시: 마스크 외부로 나가면 탈출 성공
                if (_useShapeMask && _shapeMask != null && !_shapeMask[current.x, current.y])
                {
                    return true;
                }

                // 자신의 몸통에 막히면 불가
                if (arrowCells.Contains(current))
                    return false;

                // 다른 화살표에 막히면 불가
                if (occupied[current.x, current.y])
                    return false;

                current += dirVec;
            }

            return true; // 그리드 경계 도달 성공
        }

        /// <summary>
        /// 특정 방향으로 경계까지 이동 가능한지 확인 (자신의 몸통만 체크)
        /// </summary>
        public bool CanEscapeInDirection(Vector2Int head, ArrowDirection dir, List<Vector2Int> arrowCells)
        {
            Vector2Int dirVec = GridUtility.GetDirectionVector(dir);
            Vector2Int current = head + dirVec;

            while (current.x >= 0 && current.x < _gridWidth &&
                   current.y >= 0 && current.y < _gridHeight)
            {
                // 자신의 몸통에 막히면 탈출 불가
                if (arrowCells.Contains(current))
                    return false;
                current += dirVec;
            }

            return true;
        }

        /// <summary>
        /// Head에서 가장 가까운 경계 방향 찾기 (자신의 몸통에 막히지 않는 방향)
        /// </summary>
        public ArrowDirection GetNearestBoundaryDirection(Vector2Int head, List<Vector2Int> arrowCells)
        {
            var candidates = new List<(ArrowDirection dir, int distance)>();

            // Up: 상단 경계까지
            int distUp = _gridHeight - 1 - head.y;
            if (CanEscapeInDirection(head, ArrowDirection.Up, arrowCells))
                candidates.Add((ArrowDirection.Up, distUp));

            // Down: 하단 경계까지
            int distDown = head.y;
            if (CanEscapeInDirection(head, ArrowDirection.Down, arrowCells))
                candidates.Add((ArrowDirection.Down, distDown));

            // Right: 우측 경계까지
            int distRight = _gridWidth - 1 - head.x;
            if (CanEscapeInDirection(head, ArrowDirection.Right, arrowCells))
                candidates.Add((ArrowDirection.Right, distRight));

            // Left: 좌측 경계까지
            int distLeft = head.x;
            if (CanEscapeInDirection(head, ArrowDirection.Left, arrowCells))
                candidates.Add((ArrowDirection.Left, distLeft));

            // 가장 가까운 경계 방향 선택
            if (candidates.Count > 0)
            {
                candidates.Sort((a, b) => a.distance.CompareTo(b.distance));
                return candidates[0].dir;
            }

            // 모든 방향이 막혀있으면 기본값 (Up)
            return ArrowDirection.Up;
        }
    }
}
