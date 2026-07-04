using System.Collections.Generic;
using UnityEngine;
using Game.Data;

namespace Game.Editor.LevelEditor
{
    /// <summary>
    /// 화살표 의존성 그래프
    /// - A가 B에 의존 = A의 탈출 경로에 B의 셀이 있음 = B가 먼저 빠져야 A가 탈출 가능
    /// - Kahn's Algorithm으로 사이클 검사 및 위상 정렬
    /// </summary>
    public class DependencyGraph
    {
        // 의존성 그래프: dependencies[A] = {B, C} → A는 B, C에 의존
        private Dictionary<int, HashSet<int>> _dependencies;

        // 역방향 그래프: dependents[B] = {A, D} → A, D가 B에 의존
        private Dictionary<int, HashSet<int>> _dependents;

        // 그리드 크기
        private int _gridWidth;
        private int _gridHeight;

        // ========== 초기화 ==========

        public DependencyGraph(int gridWidth, int gridHeight)
        {
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            _dependencies = new Dictionary<int, HashSet<int>>();
            _dependents = new Dictionary<int, HashSet<int>>();
        }

        /// <summary>
        /// 의존성 그래프 구축
        ///
        /// 핵심 변경: 첫 번째 차단 화살표만 의존성으로 기록 (단일 의존성)
        /// ReverseGrowthGenerator의 RecordDependency()와 동일한 로직 적용
        ///
        /// 이유:
        /// - A → B → C 경로에서 A는 B에만 의존 (B가 빠지면 A는 C를 통과해서 탈출)
        /// - 다중 의존성(A → B, A → C)은 실제 게임 플레이와 맞지 않음
        /// </summary>
        public void Build(List<EditorArrow> arrows)
        {
            _dependencies.Clear();
            _dependents.Clear();

            // 모든 화살표 ID 초기화
            foreach (var arrow in arrows)
            {
                _dependencies[arrow.id] = new HashSet<int>();
                _dependents[arrow.id] = new HashSet<int>();
            }

            // 셀 → 화살표 ID 매핑 생성
            var cellToArrowId = new Dictionary<Vector2Int, int>();
            foreach (var arrow in arrows)
            {
                foreach (var cell in arrow.cells)
                {
                    cellToArrowId[cell] = arrow.id;
                }
            }

            // 각 화살표의 탈출 경로 분석
            foreach (var arrow in arrows)
            {
                var escapePath = GetEscapePath(arrow);

                // 핵심 변경: 첫 번째 차단 화살표만 의존성으로 기록
                foreach (var cell in escapePath)
                {
                    // 탈출 경로에 다른 화살표가 있는지 확인
                    if (cellToArrowId.TryGetValue(cell, out int blockingArrowId))
                    {
                        if (blockingArrowId != arrow.id)
                        {
                            // arrow는 blockingArrow에 의존 (첫 번째만!)
                            _dependencies[arrow.id].Add(blockingArrowId);
                            _dependents[blockingArrowId].Add(arrow.id);
                            break;  // 핵심: 첫 번째 차단 화살표에서 중단
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 화살표의 탈출 경로 계산 (Head에서 headDirection 방향으로 경계까지)
        /// </summary>
        private List<Vector2Int> GetEscapePath(EditorArrow arrow)
        {
            var path = new List<Vector2Int>();

            if (arrow.cells == null || arrow.cells.Count == 0)
                return path;

            Vector2Int head = arrow.cells[arrow.cells.Count - 1];
            Vector2Int dir = GridUtility.GetDirectionVector(arrow.headDirection);
            Vector2Int current = head + dir;

            while (IsInsideGrid(current))
            {
                path.Add(current);
                current += dir;
            }

            return path;
        }

        // ========== 사이클 검사 ==========

        /// <summary>
        /// 의존성 그래프에 사이클이 있는지 검사 (Kahn's Algorithm)
        /// 사이클이 있으면 해답이 존재하지 않음
        /// </summary>
        public bool HasCycle()
        {
            // In-degree 계산 (나에게 의존하는 화살표 수가 아닌, 내가 의존하는 화살표 수)
            var inDegree = new Dictionary<int, int>();
            foreach (var kvp in _dependencies)
            {
                inDegree[kvp.Key] = kvp.Value.Count;
            }

            // In-degree가 0인 노드부터 시작 (의존하는 화살표가 없음 = 먼저 탈출 가능)
            var queue = new Queue<int>();
            foreach (var kvp in inDegree)
            {
                if (kvp.Value == 0)
                {
                    queue.Enqueue(kvp.Key);
                }
            }

            int processedCount = 0;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                processedCount++;

                // current에 의존하는 노드들의 in-degree 감소
                if (_dependents.TryGetValue(current, out var dependentSet))
                {
                    foreach (int dependent in dependentSet)
                    {
                        inDegree[dependent]--;
                        if (inDegree[dependent] == 0)
                        {
                            queue.Enqueue(dependent);
                        }
                    }
                }
            }

            // 모든 노드가 처리되지 않았으면 사이클 존재
            return processedCount != _dependencies.Count;
        }

        /// <summary>
        /// 사이클에 포함된 화살표 ID 찾기
        /// </summary>
        public List<int> FindCycleArrows()
        {
            var cycleArrows = new List<int>();

            // In-degree 계산
            var inDegree = new Dictionary<int, int>();
            foreach (var kvp in _dependencies)
            {
                inDegree[kvp.Key] = kvp.Value.Count;
            }

            // In-degree가 0인 노드 처리
            var queue = new Queue<int>();
            foreach (var kvp in inDegree)
            {
                if (kvp.Value == 0)
                {
                    queue.Enqueue(kvp.Key);
                }
            }

            var processed = new HashSet<int>();

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                processed.Add(current);

                if (_dependents.TryGetValue(current, out var dependentSet))
                {
                    foreach (int dependent in dependentSet)
                    {
                        inDegree[dependent]--;
                        if (inDegree[dependent] == 0)
                        {
                            queue.Enqueue(dependent);
                        }
                    }
                }
            }

            // 처리되지 않은 노드 = 사이클에 포함
            foreach (var kvp in _dependencies)
            {
                if (!processed.Contains(kvp.Key))
                {
                    cycleArrows.Add(kvp.Key);
                }
            }

            return cycleArrows;
        }

        // ========== 위상 정렬 ==========

        /// <summary>
        /// 위상 정렬로 해답 순서 계산
        /// </summary>
        /// <returns>화살표 ID 순서 (이 순서로 탈출하면 클리어)</returns>
        public List<int> TopologicalSort()
        {
            var result = new List<int>();

            // In-degree 계산
            var inDegree = new Dictionary<int, int>();
            foreach (var kvp in _dependencies)
            {
                inDegree[kvp.Key] = kvp.Value.Count;
            }

            // In-degree가 0인 노드부터 시작
            var queue = new Queue<int>();
            foreach (var kvp in inDegree)
            {
                if (kvp.Value == 0)
                {
                    queue.Enqueue(kvp.Key);
                }
            }

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                result.Add(current);

                if (_dependents.TryGetValue(current, out var dependentSet))
                {
                    foreach (int dependent in dependentSet)
                    {
                        inDegree[dependent]--;
                        if (inDegree[dependent] == 0)
                        {
                            queue.Enqueue(dependent);
                        }
                    }
                }
            }

            // 사이클이 있으면 모든 노드가 결과에 포함되지 않음
            if (result.Count != _dependencies.Count)
            {
                Debug.LogWarning($"[DependencyGraph] Topological sort incomplete: {result.Count}/{_dependencies.Count}");
            }

            return result;
        }

        /// <summary>
        /// 동적 시뮬레이션 기반 위상 정렬 (SolvabilityValidator와 동일한 로직)
        ///
        /// 핵심 차이점:
        /// - 정적 Build() 기반 TopologicalSort()는 모든 화살표가 존재하는 상태에서 의존성 계산
        /// - 이 메서드는 화살표가 실제로 탈출하면서 의존성을 동적으로 재계산
        ///
        /// 알고리즘:
        /// 1. 현재 탈출 가능한 화살표 찾기 (탈출 경로에 아무도 없는 화살표)
        /// 2. 해당 화살표 탈출 (occupied에서 제거)
        /// 3. 반복하여 모든 화살표 탈출 순서 계산
        /// </summary>
        public List<int> TopologicalSortDynamic(List<EditorArrow> arrows)
        {
            var result = new List<int>();

            if (arrows == null || arrows.Count == 0)
                return result;

            // 1. 화살표별 셀 집합 + 전체 점유 맵 구성
            var occupiedCells = new HashSet<Vector2Int>();
            var ownCells = new Dictionary<int, HashSet<Vector2Int>>();
            foreach (var arrow in arrows)
            {
                var set = new HashSet<Vector2Int>(arrow.cells);
                ownCells[arrow.id] = set;
                foreach (var cell in arrow.cells)
                    occupiedCells.Add(cell);
            }

            // 2. 각 화살표의 탈출 경로(자기 셀 제외)를 훑어 blockCount 계산
            //    cellToBlockers[c] = 셀 c를 탈출 경로에 포함하는 화살표 목록
            //    blockCount[id]   = 해당 화살표 경로 상 '다른 화살표가 점유한 셀' 수
            //    → blockCount==0 이면 지금 탈출 가능 (CanEscapeNow와 동일 판정)
            var cellToBlockers = new Dictionary<Vector2Int, List<int>>();
            var blockCount = new Dictionary<int, int>();

            foreach (var arrow in arrows)
            {
                var own = ownCells[arrow.id];
                Vector2Int head = arrow.cells[arrow.cells.Count - 1];
                Vector2Int dir = GridUtility.GetDirectionVector(arrow.headDirection);
                Vector2Int pos = head + dir;
                int blocked = 0;

                while (IsInsideGrid(pos))
                {
                    if (!own.Contains(pos))
                    {
                        if (!cellToBlockers.TryGetValue(pos, out var list))
                        {
                            list = new List<int>();
                            cellToBlockers[pos] = list;
                        }
                        list.Add(arrow.id);

                        // 자기 셀을 제외했으므로 점유 셀 = 다른 화살표가 막고 있음
                        if (occupiedCells.Contains(pos))
                            blocked++;
                    }
                    pos += dir;
                }

                blockCount[arrow.id] = blocked;
            }

            // 3. Kahn 방식 동적 정렬
            //    blockCount==0 화살표부터 탈출 → 비운 셀로 인해 풀리는 화살표를 큐에 추가.
            //    입력 순서로 초기 큐를 채워 결과 순서를 결정적으로 유지.
            var queue = new Queue<int>();
            foreach (var arrow in arrows)
                if (blockCount[arrow.id] == 0)
                    queue.Enqueue(arrow.id);

            var escaped = new HashSet<int>();

            while (queue.Count > 0)
            {
                int id = queue.Dequeue();
                if (!escaped.Add(id))
                    continue;

                result.Add(id);

                // 탈출한 화살표의 셀을 비우고, 그 셀을 경로로 가진 화살표들의 blockCount 감소
                foreach (var cell in ownCells[id])
                {
                    if (!occupiedCells.Remove(cell))
                        continue;

                    if (cellToBlockers.TryGetValue(cell, out var blockedArrows))
                    {
                        foreach (int other in blockedArrows)
                        {
                            if (escaped.Contains(other))
                                continue;

                            if (--blockCount[other] == 0)
                                queue.Enqueue(other);
                        }
                    }
                }
            }

            // result.Count != arrows.Count 이면 사이클/막힘 존재 (호출부에서 판정)
            return result;
        }

        // ========== 사이클 해소 ==========

        /// <summary>
        /// 사이클 해소 시도 - 다양한 전략 사용 (개선됨)
        ///
        /// 핵심 개선:
        /// - 이미 수정한 화살표 추적하여 무한 반복 방지
        /// - 전략 4, 5로 바로 넘어가는 빠른 실패 처리
        ///
        /// 전략:
        /// 1. 셀 순서 뒤집기 (양 끝점 교환)
        /// 2. Head 방향으로 셀 추가
        /// 3. 끝 셀 제거 후 방향 변경
        /// 4. 강제 경계 방향 설정 (최후의 수단)
        /// 5. 반복적 강제 경계 방향 (여러 화살표 동시 처리)
        /// </summary>
        /// <returns>해소 성공 여부</returns>
        public bool TryResolveCycle(List<EditorArrow> arrows)
        {
            const int MAX_CYCLE_RESOLUTION_ATTEMPTS = 15;
            const int QUICK_FAIL_THRESHOLD = 3; // 전략 1~3이 이만큼 실패하면 바로 전략 4~5로

            // ★ 이미 수정한 화살표 추적 (무한 반복 방지)
            var modifiedArrows = new HashSet<int>();
            int quickFailCount = 0;

            for (int attempt = 0; attempt < MAX_CYCLE_RESOLUTION_ATTEMPTS; attempt++)
            {
                var cycleArrows = FindCycleArrows();
                if (cycleArrows.Count == 0)
                    return true; // 사이클 해소 완료!

                Debug.Log($"[DependencyGraph] Cycle resolution attempt {attempt + 1}: {cycleArrows.Count} arrows in cycle: [{string.Join(", ", cycleArrows)}]");

                // 셀 → 화살표 ID 매핑 생성
                var cellToArrowId = new Dictionary<Vector2Int, int>();
                foreach (var arrow in arrows)
                {
                    foreach (var cell in arrow.cells)
                    {
                        cellToArrowId[cell] = arrow.id;
                    }
                }

                bool resolvedThisAttempt = false;

                // ★ 전략 1~3은 수정하지 않은 화살표에 대해서만 시도
                if (quickFailCount < QUICK_FAIL_THRESHOLD)
                {
                    foreach (int arrowId in cycleArrows)
                    {
                        // ★ 이미 수정한 화살표는 건너뛰기
                        if (modifiedArrows.Contains(arrowId))
                            continue;

                        var arrow = arrows.Find(a => a.id == arrowId);
                        if (arrow == null) continue;

                        // 전략 1: 셀 순서 뒤집어서 반대 끝점을 Head로
                        if (TryReverseArrow(arrow, arrows, cellToArrowId))
                        {
                            Debug.Log($"[DependencyGraph] Modified Arrow {arrowId} by reversing");
                            modifiedArrows.Add(arrowId);
                            resolvedThisAttempt = true;
                            break;
                        }

                        // 전략 2: 경계 방향으로 셀 1개 추가
                        if (TryAddCellToBoundary(arrow, arrows, cellToArrowId))
                        {
                            Debug.Log($"[DependencyGraph] Modified Arrow {arrowId} by adding cell");
                            modifiedArrows.Add(arrowId);
                            resolvedThisAttempt = true;
                            break;
                        }

                        // 전략 3: 끝 셀 제거 후 새 방향 확인
                        if (TryTrimAndReorient(arrow, arrows, cellToArrowId))
                        {
                            Debug.Log($"[DependencyGraph] Modified Arrow {arrowId} by trimming");
                            modifiedArrows.Add(arrowId);
                            resolvedThisAttempt = true;
                            break;
                        }
                    }
                }

                if (resolvedThisAttempt)
                {
                    Build(arrows);
                    quickFailCount = 0; // 성공 시 카운터 리셋
                    continue;
                }

                // 전략 1~3 실패 카운트 증가
                quickFailCount++;
                Debug.Log($"[DependencyGraph] Strategies 1-3 failed, quick fail count: {quickFailCount}/{QUICK_FAIL_THRESHOLD}");

                // ═══════════════════════════════════════════════════════════════════
                // 전략 4: 강제 경계 방향 설정 (최후의 수단)
                // ═══════════════════════════════════════════════════════════════════
                Debug.Log("[DependencyGraph] Trying forced boundary direction strategy...");

                if (TryForceBoundaryDirection(cycleArrows, arrows, cellToArrowId))
                {
                    Debug.Log("[DependencyGraph] Modified arrow by forcing boundary direction");
                    Build(arrows);
                    continue;
                }

                // ═══════════════════════════════════════════════════════════════════
                // 전략 5: 모든 사이클 화살표를 경계 방향으로 강제 설정 (최최후 수단)
                // ═══════════════════════════════════════════════════════════════════
                Debug.Log("[DependencyGraph] Trying to force ALL cycle arrows to boundary...");

                if (TryForceAllCycleArrowsToBoundary(cycleArrows, arrows, cellToArrowId))
                {
                    Debug.Log("[DependencyGraph] Forced multiple arrows to boundary");
                    Build(arrows);
                    continue;
                }

                // 모든 전략 실패
                Debug.LogWarning($"[DependencyGraph] All strategies failed at attempt {attempt + 1}");
            }

            Debug.LogWarning("[DependencyGraph] Failed to resolve cycle after max attempts");
            return false;
        }

        /// <summary>
        /// 전략 5: 사이클 내 모든 화살표를 경계 방향으로 강제
        /// 사이클 내 경계와 가장 가까운 화살표부터 순차적으로 처리
        /// </summary>
        private bool TryForceAllCycleArrowsToBoundary(
            List<int> cycleArrows,
            List<EditorArrow> allArrows,
            Dictionary<Vector2Int, int> cellToArrowId)
        {
            // 각 화살표별로 경계와의 최소 거리 계산
            var arrowDistances = new List<(int arrowId, int minDistance, ArrowDirection bestDir, Vector2Int bestCell)>();

            foreach (int arrowId in cycleArrows)
            {
                var arrow = allArrows.Find(a => a.id == arrowId);
                if (arrow == null || arrow.cells.Count < 2) continue;

                int minDist = int.MaxValue;
                ArrowDirection bestDir = ArrowDirection.Up;
                Vector2Int bestCell = arrow.cells[0];

                foreach (var cell in arrow.cells)
                {
                    int distUp = _gridHeight - 1 - cell.y;
                    int distDown = cell.y;
                    int distLeft = cell.x;
                    int distRight = _gridWidth - 1 - cell.x;

                    if (distUp < minDist) { minDist = distUp; bestDir = ArrowDirection.Up; bestCell = cell; }
                    if (distDown < minDist) { minDist = distDown; bestDir = ArrowDirection.Down; bestCell = cell; }
                    if (distLeft < minDist) { minDist = distLeft; bestDir = ArrowDirection.Left; bestCell = cell; }
                    if (distRight < minDist) { minDist = distRight; bestDir = ArrowDirection.Right; bestCell = cell; }
                }

                arrowDistances.Add((arrowId, minDist, bestDir, bestCell));
            }

            // 거리 순 정렬 (가장 가까운 것부터)
            arrowDistances.Sort((a, b) => a.minDistance.CompareTo(b.minDistance));

            // 가장 가까운 화살표에 대해 완전 경계 강제 (더 공격적인 전략)
            foreach (var (arrowId, minDist, bestDir, bestCell) in arrowDistances)
            {
                var arrow = allArrows.Find(a => a.id == arrowId);
                if (arrow == null) continue;

                if (TryForceArrowToBoundaryAggressive(arrow, allArrows, cellToArrowId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 더 공격적인 경계 강제 전략
        /// 필요한 경우 셀을 대폭 수정하여라도 경계 방향으로 설정
        /// </summary>
        private bool TryForceArrowToBoundaryAggressive(
            EditorArrow arrow,
            List<EditorArrow> allArrows,
            Dictionary<Vector2Int, int> cellToArrowId)
        {
            var originalCells = new List<Vector2Int>(arrow.cells);
            var originalDir = arrow.headDirection;

            // 각 방향별로 시도
            var directions = new[] { ArrowDirection.Up, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Right };

            foreach (var targetDir in directions)
            {
                Vector2Int dirVec = GridUtility.GetDirectionVector(targetDir);

                // 현재 화살표의 모든 셀에서 경계까지의 빈 경로 찾기
                foreach (var startCell in arrow.cells)
                {
                    // startCell에서 targetDir로 경계까지 도달 가능한지 확인
                    var pathToBoundary = new List<Vector2Int>();
                    Vector2Int pos = startCell + dirVec;
                    bool reachesBoundary = false;
                    bool blocked = false;

                    while (IsInsideGrid(pos))
                    {
                        // 자기 셀이면 건너뛰기
                        if (arrow.cells.Contains(pos))
                        {
                            pos += dirVec;
                            continue;
                        }

                        // 다른 화살표면 차단
                        if (cellToArrowId.ContainsKey(pos) && cellToArrowId[pos] != arrow.id)
                        {
                            blocked = true;
                            break;
                        }

                        pathToBoundary.Add(pos);
                        pos += dirVec;
                    }

                    if (!blocked && !IsInsideGrid(pos))
                    {
                        reachesBoundary = true;
                    }

                    if (reachesBoundary)
                    {
                        // startCell 이후의 셀들만 유지하고 경계까지 확장
                        int startIdx = arrow.cells.IndexOf(startCell);

                        // 새 셀 리스트: 처음부터 startCell까지 + 경계까지 경로
                        var newCells = new List<Vector2Int>();
                        for (int i = 0; i <= startIdx; i++)
                        {
                            newCells.Add(arrow.cells[i]);
                        }
                        newCells.AddRange(pathToBoundary);

                        if (newCells.Count >= 2)
                        {
                            // HEAD 정렬 규칙 확인
                            Vector2Int newHead = newCells[newCells.Count - 1];
                            Vector2Int newSecondLast = newCells[newCells.Count - 2];
                            ArrowDirection newDir = GridUtility.GetDirectionFromTo(newSecondLast, newHead);

                            if (newDir == targetDir && !HasSelfReference(arrow, newCells, targetDir))
                            {
                                // 적용
                                // 기존 셀 제거
                                foreach (var cell in originalCells)
                                {
                                    if (!newCells.Contains(cell))
                                    {
                                        cellToArrowId.Remove(cell);
                                    }
                                }

                                // 새 셀 추가
                                foreach (var cell in pathToBoundary)
                                {
                                    cellToArrowId[cell] = arrow.id;
                                }

                                arrow.cells = newCells;
                                arrow.headDirection = targetDir;

                                Build(allArrows);
                                if (!HasCycle())
                                {
                                    Debug.Log($"[DependencyGraph] Aggressively forced Arrow {arrow.id} to {targetDir} from cell {startCell}");
                                    return true;
                                }

                                // 실패 시 원복
                                foreach (var cell in pathToBoundary)
                                {
                                    cellToArrowId.Remove(cell);
                                }
                                foreach (var cell in originalCells)
                                {
                                    cellToArrowId[cell] = arrow.id;
                                }
                                arrow.cells = originalCells;
                                arrow.headDirection = originalDir;
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 전략 4: 강제 경계 방향 설정
        /// 사이클 내 화살표 중 경계와 가장 가까운 것을 선택하여
        /// 셀을 추가/재구성하여 경계 방향으로 HEAD 설정
        /// </summary>
        private bool TryForceBoundaryDirection(
            List<int> cycleArrows,
            List<EditorArrow> allArrows,
            Dictionary<Vector2Int, int> cellToArrowId)
        {
            // 사이클 내 화살표들을 경계와의 거리 순으로 정렬
            var arrowsByBoundaryDistance = new List<(EditorArrow arrow, int distance, ArrowDirection boundaryDir, Vector2Int closestCell)>();

            foreach (int arrowId in cycleArrows)
            {
                var arrow = allArrows.Find(a => a.id == arrowId);
                if (arrow == null || arrow.cells.Count < 2) continue;

                // 이 화살표의 모든 셀에서 경계까지의 최소 거리 계산
                foreach (var cell in arrow.cells)
                {
                    // 각 방향별 경계까지 거리
                    int distUp = _gridHeight - 1 - cell.y;
                    int distDown = cell.y;
                    int distLeft = cell.x;
                    int distRight = _gridWidth - 1 - cell.x;

                    // 최소 거리와 방향 찾기
                    int minDist = distUp;
                    ArrowDirection dir = ArrowDirection.Up;

                    if (distDown < minDist) { minDist = distDown; dir = ArrowDirection.Down; }
                    if (distLeft < minDist) { minDist = distLeft; dir = ArrowDirection.Left; }
                    if (distRight < minDist) { minDist = distRight; dir = ArrowDirection.Right; }

                    arrowsByBoundaryDistance.Add((arrow, minDist, dir, cell));
                }
            }

            // 경계와 가장 가까운 순으로 정렬
            arrowsByBoundaryDistance.Sort((a, b) => a.distance.CompareTo(b.distance));

            // 가장 가까운 것부터 시도
            var triedArrows = new HashSet<int>();
            foreach (var (arrow, distance, boundaryDir, closestCell) in arrowsByBoundaryDistance)
            {
                if (triedArrows.Contains(arrow.id)) continue;
                triedArrows.Add(arrow.id);

                // 이 화살표를 경계 방향으로 강제 설정 시도
                if (TryForceArrowToBoundary(arrow, boundaryDir, closestCell, allArrows, cellToArrowId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 화살표를 강제로 경계 방향으로 설정
        /// closestCell에서 boundaryDir 방향으로 HEAD를 설정하도록 셀을 재구성
        /// </summary>
        private bool TryForceArrowToBoundary(
            EditorArrow arrow,
            ArrowDirection boundaryDir,
            Vector2Int closestCell,
            List<EditorArrow> allArrows,
            Dictionary<Vector2Int, int> cellToArrowId)
        {
            // 원본 저장
            var originalCells = new List<Vector2Int>(arrow.cells);
            var originalDir = arrow.headDirection;

            // 전략 A: closestCell을 HEAD로 만들기 위해 셀 순서 재구성
            int closestIndex = arrow.cells.IndexOf(closestCell);
            if (closestIndex >= 0)
            {
                // closestCell이 HEAD(마지막)가 되도록 셀 재구성
                if (closestIndex < arrow.cells.Count - 1)
                {
                    // closestCell 이후의 셀들을 제거하고 HEAD로 만들기
                    var newCells = arrow.cells.GetRange(0, closestIndex + 1);

                    if (newCells.Count >= 2)
                    {
                        // HEAD 정렬 규칙: 마지막 이동 방향이 boundaryDir이 되어야 함
                        Vector2Int head = newCells[newCells.Count - 1];
                        Vector2Int secondLast = newCells[newCells.Count - 2];
                        ArrowDirection naturalDir = GridUtility.GetDirectionFromTo(secondLast, head);

                        if (naturalDir == boundaryDir)
                        {
                            // 자연스럽게 경계 방향 → 바로 적용
                            if (!HasSelfReference(arrow, newCells, boundaryDir))
                            {
                                // 제거되는 셀들 정리
                                for (int i = closestIndex + 1; i < originalCells.Count; i++)
                                {
                                    cellToArrowId.Remove(originalCells[i]);
                                }

                                arrow.cells = newCells;
                                arrow.headDirection = boundaryDir;

                                Build(allArrows);
                                if (!HasCycle())
                                {
                                    Debug.Log($"[DependencyGraph] Forced Arrow {arrow.id} to boundary direction {boundaryDir} by trimming");
                                    return true;
                                }

                                // 실패 시 원복
                                arrow.cells = originalCells;
                                arrow.headDirection = originalDir;
                                foreach (var cell in originalCells)
                                {
                                    cellToArrowId[cell] = arrow.id;
                                }
                            }
                        }
                    }
                }
            }

            // 전략 B: HEAD에 셀을 추가하여 경계 방향으로 설정
            Vector2Int currentHead = arrow.cells[arrow.cells.Count - 1];
            Vector2Int dirVec = GridUtility.GetDirectionVector(boundaryDir);

            // 경계까지 셀 추가 경로 계산
            var addPath = new List<Vector2Int>();
            Vector2Int pos = currentHead + dirVec;

            while (IsInsideGrid(pos))
            {
                // 자신의 셀이면 건너뛰기 (나중에 제거)
                if (arrow.cells.Contains(pos))
                {
                    pos += dirVec;
                    continue;
                }

                // 다른 화살표 셀이면 중단
                if (cellToArrowId.ContainsKey(pos) && cellToArrowId[pos] != arrow.id)
                {
                    break;
                }

                addPath.Add(pos);
                pos += dirVec;
            }

            // 경계에 도달했는지 확인
            if (!IsInsideGrid(pos) && addPath.Count > 0)
            {
                // addPath의 마지막 셀이 HEAD가 됨
                var newCells = new List<Vector2Int>(arrow.cells);
                newCells.AddRange(addPath);

                // HEAD 정렬 규칙 확인
                Vector2Int newHead = newCells[newCells.Count - 1];
                Vector2Int newSecondLast = newCells[newCells.Count - 2];
                ArrowDirection newDir = GridUtility.GetDirectionFromTo(newSecondLast, newHead);

                if (newDir == boundaryDir && !HasSelfReference(arrow, newCells, boundaryDir))
                {
                    // cellToArrowId 업데이트
                    foreach (var cell in addPath)
                    {
                        cellToArrowId[cell] = arrow.id;
                    }

                    arrow.cells = newCells;
                    arrow.headDirection = boundaryDir;

                    Build(allArrows);
                    if (!HasCycle())
                    {
                        Debug.Log($"[DependencyGraph] Forced Arrow {arrow.id} to boundary direction {boundaryDir} by adding {addPath.Count} cells");
                        return true;
                    }

                    // 실패 시 원복
                    foreach (var cell in addPath)
                    {
                        cellToArrowId.Remove(cell);
                    }
                    arrow.cells = originalCells;
                    arrow.headDirection = originalDir;
                }
            }

            // 전략 C: 셀 순서를 뒤집고 새 HEAD에서 경계로 셀 추가
            var reversedCells = new List<Vector2Int>(originalCells);
            reversedCells.Reverse();

            Vector2Int reversedHead = reversedCells[reversedCells.Count - 1];
            var addPathReversed = new List<Vector2Int>();
            pos = reversedHead + dirVec;

            while (IsInsideGrid(pos))
            {
                if (reversedCells.Contains(pos))
                {
                    pos += dirVec;
                    continue;
                }
                if (cellToArrowId.ContainsKey(pos) && cellToArrowId[pos] != arrow.id)
                {
                    break;
                }
                addPathReversed.Add(pos);
                pos += dirVec;
            }

            if (!IsInsideGrid(pos) && addPathReversed.Count >= 0)
            {
                var newCells = new List<Vector2Int>(reversedCells);
                newCells.AddRange(addPathReversed);

                if (newCells.Count >= 2)
                {
                    Vector2Int newHead = newCells[newCells.Count - 1];
                    Vector2Int newSecondLast = newCells[newCells.Count - 2];
                    ArrowDirection newDir = GridUtility.GetDirectionFromTo(newSecondLast, newHead);

                    if (newDir == boundaryDir && !HasSelfReference(arrow, newCells, boundaryDir))
                    {
                        foreach (var cell in addPathReversed)
                        {
                            cellToArrowId[cell] = arrow.id;
                        }

                        arrow.cells = newCells;
                        arrow.headDirection = boundaryDir;

                        Build(allArrows);
                        if (!HasCycle())
                        {
                            Debug.Log($"[DependencyGraph] Forced Arrow {arrow.id} to boundary direction {boundaryDir} by reversing + adding cells");
                            return true;
                        }

                        foreach (var cell in addPathReversed)
                        {
                            cellToArrowId.Remove(cell);
                        }
                        arrow.cells = originalCells;
                        arrow.headDirection = originalDir;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 자기 참조 검사 (셀 리스트 버전)
        /// </summary>
        private bool HasSelfReference(EditorArrow arrow, List<Vector2Int> cells, ArrowDirection headDir)
        {
            if (cells.Count < 2) return false;

            Vector2Int head = cells[cells.Count - 1];
            Vector2Int dirVec = GridUtility.GetDirectionVector(headDir);
            Vector2Int current = head + dirVec;

            while (IsInsideGrid(current))
            {
                for (int i = 0; i < cells.Count - 1; i++)
                {
                    if (cells[i] == current)
                        return true;
                }
                current += dirVec;
            }

            return false;
        }

        /// <summary>
        /// 전략 1: 셀 순서 뒤집기
        /// </summary>
        private bool TryReverseArrow(EditorArrow arrow, List<EditorArrow> arrows, Dictionary<Vector2Int, int> cellToArrowId)
        {
            if (arrow.cells.Count < 2)
                return false;

            // 원본 저장
            var originalCells = new List<Vector2Int>(arrow.cells);
            var originalDir = arrow.headDirection;

            // 셀 뒤집기
            arrow.cells.Reverse();

            // 새 HEAD 방향 계산
            Vector2Int newHead = arrow.cells[arrow.cells.Count - 1];
            Vector2Int newSecondLast = arrow.cells[arrow.cells.Count - 2];
            ArrowDirection newDir = GridUtility.GetDirectionFromTo(newSecondLast, newHead);

            // 자기 참조 검사
            if (!HasSelfReference(arrow, newDir))
            {
                arrow.headDirection = newDir;

                // 그래프 재구축 후 사이클 검사
                Build(arrows);
                if (!HasCycle())
                {
                    return true;
                }
            }

            // 원복
            arrow.cells = originalCells;
            arrow.headDirection = originalDir;
            return false;
        }

        /// <summary>
        /// 전략 2: 경계 방향으로 셀 추가
        /// </summary>
        private bool TryAddCellToBoundary(EditorArrow arrow, List<EditorArrow> arrows, Dictionary<Vector2Int, int> cellToArrowId)
        {
            if (arrow.cells.Count < 2)
                return false;

            Vector2Int head = arrow.cells[arrow.cells.Count - 1];

            // 각 경계 방향 시도
            var directions = new[] { ArrowDirection.Up, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Right };

            foreach (var dir in directions)
            {
                Vector2Int dirVec = GridUtility.GetDirectionVector(dir);
                Vector2Int newHead = head + dirVec;

                // 새 셀이 유효한지 확인
                if (!IsInsideGrid(newHead))
                    continue; // 이미 경계 바깥

                if (arrow.cells.Contains(newHead))
                    continue; // 자기 몸통

                if (cellToArrowId.ContainsKey(newHead) && cellToArrowId[newHead] != arrow.id)
                    continue; // 다른 화살표 셀

                // 원본 저장
                var originalCells = new List<Vector2Int>(arrow.cells);
                var originalDir = arrow.headDirection;

                // 셀 추가
                arrow.cells.Add(newHead);
                arrow.headDirection = dir;

                // 자기 참조 검사
                if (!HasSelfReference(arrow, dir))
                {
                    // cellToArrowId 업데이트
                    cellToArrowId[newHead] = arrow.id;

                    // 그래프 재구축 후 사이클 검사
                    Build(arrows);
                    if (!HasCycle())
                    {
                        return true;
                    }

                    // 원복
                    cellToArrowId.Remove(newHead);
                }

                arrow.cells = originalCells;
                arrow.headDirection = originalDir;
            }

            return false;
        }

        /// <summary>
        /// 전략 3: 끝 셀 제거 후 새 방향 확인
        /// </summary>
        private bool TryTrimAndReorient(EditorArrow arrow, List<EditorArrow> arrows, Dictionary<Vector2Int, int> cellToArrowId)
        {
            if (arrow.cells.Count <= 2)
                return false; // 최소 2셀 유지

            // 원본 저장
            var originalCells = new List<Vector2Int>(arrow.cells);
            var originalDir = arrow.headDirection;

            // 마지막 셀 제거
            Vector2Int removedCell = arrow.cells[arrow.cells.Count - 1];
            arrow.cells.RemoveAt(arrow.cells.Count - 1);

            // 새 HEAD 방향 계산
            Vector2Int newHead = arrow.cells[arrow.cells.Count - 1];
            Vector2Int newSecondLast = arrow.cells[arrow.cells.Count - 2];
            ArrowDirection newDir = GridUtility.GetDirectionFromTo(newSecondLast, newHead);

            // 자기 참조 검사
            if (!HasSelfReference(arrow, newDir))
            {
                arrow.headDirection = newDir;

                // cellToArrowId 업데이트
                cellToArrowId.Remove(removedCell);

                // 그래프 재구축 후 사이클 검사
                Build(arrows);
                if (!HasCycle())
                {
                    return true;
                }

                // 원복
                cellToArrowId[removedCell] = arrow.id;
            }

            arrow.cells = originalCells;
            arrow.headDirection = originalDir;
            return false;
        }

        /// <summary>
        /// 자기 참조 검사 - Head 탈출 경로에 자신의 Body가 있는지
        /// </summary>
        private bool HasSelfReference(EditorArrow arrow, ArrowDirection headDir)
        {
            Vector2Int head = arrow.cells[arrow.cells.Count - 1];
            Vector2Int dirVec = GridUtility.GetDirectionVector(headDir);
            Vector2Int current = head + dirVec;

            while (IsInsideGrid(current))
            {
                // Head 제외한 Body 셀과 겹치는지 확인
                for (int i = 0; i < arrow.cells.Count - 1; i++)
                {
                    if (arrow.cells[i] == current)
                        return true; // 자기 참조 발생
                }
                current += dirVec;
            }

            return false;
        }

        // ========== 유틸리티 ==========

        private bool IsInsideGrid(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < _gridWidth && pos.y >= 0 && pos.y < _gridHeight;
        }

        // ========== 디버그 ==========

        /// <summary>
        /// 의존성 그래프 정보 출력
        /// </summary>
        public void LogGraphInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[DependencyGraph] Graph Info:");
            sb.AppendLine($"  Total arrows: {_dependencies.Count}");

            int totalDeps = 0;
            foreach (var kvp in _dependencies)
            {
                totalDeps += kvp.Value.Count;
                if (kvp.Value.Count > 0)
                {
                    sb.AppendLine($"  Arrow {kvp.Key} depends on: [{string.Join(", ", kvp.Value)}]");
                }
            }

            sb.AppendLine($"  Total dependencies: {totalDeps}");
            sb.AppendLine($"  Has cycle: {HasCycle()}");

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 특정 화살표의 의존성 정보
        /// </summary>
        public HashSet<int> GetDependencies(int arrowId)
        {
            return _dependencies.TryGetValue(arrowId, out var deps) ? deps : new HashSet<int>();
        }

        /// <summary>
        /// 특정 화살표에 의존하는 화살표들
        /// </summary>
        public HashSet<int> GetDependents(int arrowId)
        {
            return _dependents.TryGetValue(arrowId, out var deps) ? deps : new HashSet<int>();
        }
    }
}
