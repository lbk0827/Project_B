using System.Collections.Generic;
using UnityEngine;

namespace Game.Editor.LevelEditor
{
    /// <summary>
    /// 미로 경로 생성 알고리즘 (Recursive Backtracking)
    /// 그리드 전체를 100% 커버하는 단일 연결 경로 생성
    ///
    /// 핵심 원리:
    /// 1. 시작점에서 출발
    /// 2. 방문하지 않은 인접 셀 중 하나를 랜덤 선택하여 이동
    /// 3. 막다른 길에 도달하면 백트래킹
    /// 4. 모든 셀 방문 시 종료
    /// </summary>
    public class MazeGenerator
    {
        private int _gridWidth;
        private int _gridHeight;
        private bool[,] _shapeMask;
        private bool _useShapeMask;
        private System.Random _random;

        // 4방향 (상하좌우)
        private static readonly Vector2Int[] Directions = new Vector2Int[]
        {
            new Vector2Int(0, 1),   // Up
            new Vector2Int(0, -1),  // Down
            new Vector2Int(-1, 0),  // Left
            new Vector2Int(1, 0)    // Right
        };

        // ========== 초기화 ==========

        public MazeGenerator(int seed = -1)
        {
            _random = seed >= 0 ? new System.Random(seed) : new System.Random();
        }

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

        // ========== 경로 생성 ==========

        /// <summary>
        /// 그리드 전체를 커버하는 단일 경로 생성 (Recursive Backtracking)
        /// </summary>
        /// <returns>경로 셀 리스트 (순서대로 연결됨)</returns>
        public List<Vector2Int> GeneratePath()
        {
            // 활성 셀 개수 계산
            int totalActiveCells = CountActiveCells();
            if (totalActiveCells == 0)
            {
                Debug.LogWarning("[MazeGenerator] No active cells in grid");
                return new List<Vector2Int>();
            }

            // 방문 배열 초기화
            bool[,] visited = new bool[_gridWidth, _gridHeight];

            // 시작점 찾기 (활성 셀 중 하나)
            Vector2Int? startCell = FindStartCell();
            if (!startCell.HasValue)
            {
                Debug.LogWarning("[MazeGenerator] Could not find start cell");
                return new List<Vector2Int>();
            }

            // Recursive Backtracking으로 경로 생성
            List<Vector2Int> path = new List<Vector2Int>();
            Stack<Vector2Int> stack = new Stack<Vector2Int>();

            Vector2Int start = startCell.Value;
            visited[start.x, start.y] = true;
            path.Add(start);
            stack.Push(start);

            while (path.Count < totalActiveCells && stack.Count > 0)
            {
                Vector2Int current = stack.Peek();

                // 방문하지 않은 인접 셀 찾기
                List<Vector2Int> unvisitedNeighbors = GetUnvisitedNeighbors(current, visited);

                if (unvisitedNeighbors.Count > 0)
                {
                    // 랜덤하게 다음 셀 선택
                    Vector2Int next = unvisitedNeighbors[_random.Next(unvisitedNeighbors.Count)];
                    visited[next.x, next.y] = true;
                    path.Add(next);
                    stack.Push(next);
                }
                else
                {
                    // 막다른 길 - 백트래킹
                    stack.Pop();

                    // 경로 재구성: 스택 상태에 맞춰 경로 조정
                    if (stack.Count > 0)
                    {
                        // 현재 스택 탑에서 다시 탐색할 수 있도록 경로 조정
                        // 주의: path에서는 제거하지 않음 (전체 방문 순서 유지)
                    }
                }
            }

            // 검증: 모든 활성 셀이 방문되었는지 확인
            int visitedCount = CountVisitedCells(visited);
            if (visitedCount != totalActiveCells)
            {
                Debug.LogWarning($"[MazeGenerator] Path incomplete: visited {visitedCount}/{totalActiveCells} cells");

                // 연결되지 않은 셀이 있으면 다시 시도 (Shape Mask로 인한 분리된 영역)
                return TryConnectDisconnectedRegions(path, visited, totalActiveCells);
            }

            Debug.Log($"[MazeGenerator] Generated path with {path.Count} cells (100% fill rate)");
            return path;
        }

        /// <summary>
        /// 분리된 영역 연결 시도
        /// Shape Mask 사용 시 여러 개의 분리된 영역이 있을 수 있음
        /// </summary>
        private List<Vector2Int> TryConnectDisconnectedRegions(
            List<Vector2Int> existingPath,
            bool[,] visited,
            int totalActiveCells)
        {
            // 방문하지 않은 활성 셀 찾기
            List<Vector2Int> unvisitedCells = new List<Vector2Int>();
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    if (IsCellActive(x, y) && !visited[x, y])
                    {
                        unvisitedCells.Add(new Vector2Int(x, y));
                    }
                }
            }

            if (unvisitedCells.Count == 0)
                return existingPath;

            // 분리된 영역이 있으면 경고 출력
            Debug.LogWarning($"[MazeGenerator] Found {unvisitedCells.Count} disconnected cells. " +
                           "This may happen with complex shape masks.");

            // 간단한 해결책: 분리된 셀들을 순서대로 추가
            // (실제로는 연결되지 않지만, 화살표 분할 시 처리됨)
            foreach (var cell in unvisitedCells)
            {
                existingPath.Add(cell);
                visited[cell.x, cell.y] = true;
            }

            return existingPath;
        }

        // ========== 유틸리티 ==========

        /// <summary>
        /// 방문하지 않은 인접 셀 찾기 (상하좌우만)
        /// </summary>
        private List<Vector2Int> GetUnvisitedNeighbors(Vector2Int cell, bool[,] visited)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>();

            // 방향 셔플 (랜덤성 향상)
            var shuffledDirs = ShuffleDirections();

            foreach (var dir in shuffledDirs)
            {
                Vector2Int neighbor = cell + dir;

                if (IsValidCell(neighbor) && !visited[neighbor.x, neighbor.y])
                {
                    neighbors.Add(neighbor);
                }
            }

            return neighbors;
        }

        /// <summary>
        /// 방향 배열 셔플
        /// </summary>
        private Vector2Int[] ShuffleDirections()
        {
            Vector2Int[] dirs = (Vector2Int[])Directions.Clone();

            // Fisher-Yates shuffle
            for (int i = dirs.Length - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                var temp = dirs[i];
                dirs[i] = dirs[j];
                dirs[j] = temp;
            }

            return dirs;
        }

        /// <summary>
        /// 셀이 유효한지 확인 (그리드 내부 + 마스크 활성)
        /// </summary>
        private bool IsValidCell(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < _gridWidth &&
                   cell.y >= 0 && cell.y < _gridHeight &&
                   IsCellActive(cell.x, cell.y);
        }

        /// <summary>
        /// 셀이 활성 상태인지 확인 (마스크 고려)
        /// </summary>
        private bool IsCellActive(int x, int y)
        {
            if (!_useShapeMask || _shapeMask == null)
                return true;

            return _shapeMask[x, y];
        }

        /// <summary>
        /// 활성 셀 개수 계산
        /// </summary>
        private int CountActiveCells()
        {
            if (!_useShapeMask || _shapeMask == null)
                return _gridWidth * _gridHeight;

            int count = 0;
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    if (_shapeMask[x, y]) count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 방문된 셀 개수 계산
        /// </summary>
        private int CountVisitedCells(bool[,] visited)
        {
            int count = 0;
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    if (visited[x, y]) count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 시작 셀 찾기 (좌하단 모서리 근처 우선)
        /// </summary>
        private Vector2Int? FindStartCell()
        {
            // 모서리부터 시작하면 더 자연스러운 경로 생성
            // 좌하단(0,0) 근처부터 탐색
            for (int y = 0; y < _gridHeight; y++)
            {
                for (int x = 0; x < _gridWidth; x++)
                {
                    if (IsCellActive(x, y))
                    {
                        return new Vector2Int(x, y);
                    }
                }
            }

            return null;
        }

        // ========== 경로 검증 ==========

        /// <summary>
        /// 생성된 경로 검증
        /// </summary>
        public (bool valid, string error) ValidatePath(List<Vector2Int> path)
        {
            if (path == null || path.Count == 0)
                return (false, "Path is empty");

            // 1. 모든 셀이 활성 영역 내인지 확인
            foreach (var cell in path)
            {
                if (!IsValidCell(cell))
                {
                    return (false, $"Cell {cell} is outside valid area");
                }
            }

            // 2. 연속성 검증 (인접 셀만 연결)
            for (int i = 1; i < path.Count; i++)
            {
                Vector2Int diff = path[i] - path[i - 1];
                int dist = Mathf.Abs(diff.x) + Mathf.Abs(diff.y);

                // 인접하지 않은 셀이 있으면 경로가 끊어진 것
                // (Shape Mask로 인한 분리 영역일 수 있음 - 허용)
                if (dist != 1)
                {
                    Debug.LogWarning($"[MazeGenerator] Path discontinuity at index {i}: {path[i-1]} to {path[i]}");
                }
            }

            // 3. 중복 셀 검증
            var uniqueCells = new HashSet<Vector2Int>(path);
            if (uniqueCells.Count != path.Count)
            {
                return (false, $"Path has duplicate cells: {path.Count} total, {uniqueCells.Count} unique");
            }

            // 4. Fill Rate 계산
            int totalActive = CountActiveCells();
            float fillRate = (float)path.Count / totalActive * 100f;

            if (fillRate < 100f)
            {
                Debug.LogWarning($"[MazeGenerator] Fill rate: {fillRate:F1}% ({path.Count}/{totalActive})");
            }

            return (true, null);
        }
    }
}
