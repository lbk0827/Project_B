using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Data;

namespace Game.Editor.LevelEditor
{
    /// <summary>
    /// Reverse Growth 퍼즐 생성기
    ///
    /// 핵심 개념: ValidTargets
    /// - ValidTargets = 경계 셀 + 기존 화살표 Body 셀
    /// - Head가 ValidTarget을 향하면 탈출 가능
    /// - 나중에 생성된 화살표가 먼저 생성된 화살표에 의존
    ///
    /// 장점:
    /// 1. 내부 Head 허용 (경계 + 기존 화살표 Body가 ValidTarget)
    /// 2. 의존성 자동 생성 (DAG 보장)
    /// 3. 해답 순서 = 생성 역순
    /// 4. HEAD 정렬 규칙 자동 충족
    /// </summary>
    public class ReverseGrowthGenerator
    {
        // ========== 설정 ==========
        private int _gridWidth;
        private int _gridHeight;
        private bool[,] _shapeMask;
        private bool _useShapeMask;
        private int _minArrowLength = 2;
        private int _maxArrowLength = 8;
        private int _colorCount = 6;
        private int _maxFreeArrows = 0;  // 동시 탈출 가능 화살표 제한 (0 = 제한 없음)
        private System.Random _random;

        // ========== Geometric 패턴 설정 ==========
        private bool _useGeometricPatterns = false;
        private GeometricPatternType _enabledPatterns = GeometricPatternType.All;

        // ========== 색상 매핑 ==========
        private GameColor[,] _colorMap;
        private bool _useColorMapping;

        // ========== 생성 상태 ==========
        private HashSet<Vector2Int> _validTargets;      // 탈출 가능 타겟 (경계 + 기존 화살표 Body)
        private HashSet<Vector2Int> _occupied;          // 점유된 셀
        private List<EditorArrow> _arrows;
        private List<int> _creationOrder;               // 생성 순서 (ID 리스트)

        // ========== 의존성 그래프 (Gemini/GPT 권장) ==========
        private Dictionary<int, int> _dependsOn;        // arrowId → 의존하는 arrowId (-1 = 경계 직접 탈출)

        // ========== 초기화 ==========

        public ReverseGrowthGenerator(int seed = -1)
        {
            _random = seed >= 0 ? new System.Random(seed) : new System.Random();
        }

        public void SetContext(int gridWidth, int gridHeight, bool[,] shapeMask, bool useShapeMask)
        {
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            _shapeMask = shapeMask;
            _useShapeMask = useShapeMask;
        }

        public void SetParameters(int minArrowLength, int maxArrowLength, int colorCount)
        {
            _minArrowLength = Mathf.Max(2, minArrowLength);
            _maxArrowLength = Mathf.Max(_minArrowLength, maxArrowLength);
            _colorCount = Mathf.Clamp(colorCount, 1, 12);
        }

        public void SetDifficultyParameters(int maxFreeArrows)
        {
            _maxFreeArrows = Mathf.Max(0, maxFreeArrows);
        }

        /// <summary>
        /// 색상 매핑 설정 (Image Import 시 PNG 색상 기반 화살표 색상 결정)
        /// </summary>
        public void SetColorMap(GameColor[,] colorMap, bool useColorMapping)
        {
            _colorMap = colorMap;
            _useColorMapping = useColorMapping;
        }

        public void SetGeometricPatterns(bool useGeometric, GeometricPatternType enabledPatterns)
        {
            _useGeometricPatterns = useGeometric;
            _enabledPatterns = enabledPatterns;
        }

        // ========== Progress 콜백 ==========
        private System.Func<string, float, bool> _onProgress;

        /// <summary>
        /// 생성 진행 상황 콜백 설정
        /// callback(message, progress) → bool: true면 취소 요청
        /// </summary>
        public void SetProgressCallback(System.Func<string, float, bool> callback)
        {
            _onProgress = callback;
        }

        private bool _cancelRequested;

        private void ReportProgress(string message, float progress)
        {
            if (_cancelRequested) return;
            if (_onProgress != null && _onProgress.Invoke(message, progress))
                _cancelRequested = true;
        }

        // ========== 메인 생성 ==========

        public (List<EditorArrow> arrows, List<int> solutionOrder) Generate()
        {
            _cancelRequested = false;
            _arrows = new List<EditorArrow>();
            _creationOrder = new List<int>();
            _occupied = new HashSet<Vector2Int>();
            _validTargets = new HashSet<Vector2Int>();
            _dependsOn = new Dictionary<int, int>();  // 의존성 그래프 초기화

            Debug.Log("[ReverseGrowthGenerator] ========== 생성 시작 ==========");

            // Phase 1: ValidTargets 초기화 (경계 셀)
            InitializeValidTargets();
            Debug.Log($"[ReverseGrowthGenerator] Phase 1: ValidTargets 초기화 완료 ({_validTargets.Count} 경계 셀)");
            ReportProgress("Phase 1/6: 경계 초기화 완료", 0.05f);

            // Phase 2: 화살표 생성 루프
            int arrowId = 1;
            int maxIterations = _gridWidth * _gridHeight * 2;
            int iteration = 0;
            int consecutiveFailures = 0;
            int maxConsecutiveFailures = 50;
            int estimatedTotalCells = CountTotalActiveCells();

            while (HasEmptyCells() && iteration < maxIterations && consecutiveFailures < maxConsecutiveFailures && !_cancelRequested)
            {
                iteration++;

                var arrow = TryCreateArrow(arrowId);

                if (arrow != null)
                {
                    _arrows.Add(arrow);
                    _creationOrder.Add(arrow.id);

                    // 핵심: Body 셀을 ValidTargets에 추가
                    AddBodyToValidTargets(arrow);
                    MarkOccupied(arrow.cells);

                    // 의존성 기록 (Gemini/GPT 권장)
                    RecordDependency(arrow);

                    arrowId++;
                    consecutiveFailures = 0;

                    if (_arrows.Count % 10 == 0)
                    {
                        Debug.Log($"[ReverseGrowthGenerator] Progress: {_arrows.Count} arrows, " +
                                $"{CountEmptyCells()} empty cells remaining");
                    }

                    // Progress: Phase 2는 전체의 5%~60% 구간
                    float fillProgress = estimatedTotalCells > 0
                        ? (float)_occupied.Count / estimatedTotalCells : 0f;
                    ReportProgress($"Phase 2/6: 화살표 {_arrows.Count}개 생성 중...", 0.05f + fillProgress * 0.55f);
                }
                else
                {
                    consecutiveFailures++;
                }
            }

            Debug.Log($"[ReverseGrowthGenerator] Phase 2 완료: {_arrows.Count} arrows 생성");

            if (_cancelRequested)
            {
                Debug.Log("[ReverseGrowthGenerator] 취소됨");
                return (null, null);
            }

            // Phase 3: Gap Filling
            Debug.Log("[ReverseGrowthGenerator] Phase 3: Gap Filling 시작");
            ReportProgress("Phase 3/6: Gap Filling...", 0.65f);
            FillGaps(ref arrowId);
            Debug.Log($"[ReverseGrowthGenerator] Phase 3 완료: 총 {_arrows.Count} arrows");

            // Phase 3.5: Gap Filling 후 모든 의존성 재계산 (핵심 수정!)
            // Tail 확장으로 기존 화살표의 Body가 변경되어 의존 관계가 달라질 수 있음
            Debug.Log("[ReverseGrowthGenerator] Phase 3.5: 의존성 재계산");
            ReportProgress("Phase 4/6: 의존성 재계산...", 0.75f);
            RecalculateAllDependencies();

            // Phase 4: 색상 할당
            Debug.Log("[ReverseGrowthGenerator] Phase 4: 색상 할당");
            ReportProgress("Phase 5/6: 색상 할당...", 0.80f);
            AssignColors();

            // Phase 5: 검증
            Debug.Log("[ReverseGrowthGenerator] Phase 5: 검증");
            ReportProgress("Phase 6/6: 검증 중...", 0.90f);
            var (valid, errors) = ValidateResult();

            if (!valid)
            {
                Debug.LogWarning($"[ReverseGrowthGenerator] 검증 실패 ({errors.Count} 오류):");
                foreach (var error in errors)
                {
                    Debug.LogWarning($"  - {error}");
                }
            }

            // 해답 순서 계산 (DependencyGraph 동적 시뮬레이션 사용)
            // 핵심 변경: 정적 TopologicalSort() 대신 동적 TopologicalSortDynamic() 사용
            // 이유: 정적 의존성 그래프는 실제 탈출 시뮬레이션과 다른 결과를 줄 수 있음
            var depGraph = new DependencyGraph(_gridWidth, _gridHeight);

            // 동적 시뮬레이션으로 해답 순서 계산 (SolvabilityValidator와 동일한 로직)
            var solutionOrder = depGraph.TopologicalSortDynamic(_arrows);

            // 동적 정렬이 불완전하면 실패 (Fallback 금지)
            if (solutionOrder.Count != _arrows.Count)
            {
                Debug.LogWarning($"[ReverseGrowthGenerator] Dynamic topological sort incomplete ({solutionOrder.Count}/{_arrows.Count}). Reseed required.");
                return (null, null);
            }

            // 검증: SolvabilityValidator로 최종 확인 (동일한 로직이므로 항상 성공해야 함)
            var (solvable, solveError) = SolvabilityValidator.Validate(
                _arrows, solutionOrder, _gridWidth, _gridHeight);

            if (!solvable)
            {
                // 이 경우는 발생하면 안 됨 (동일한 로직 사용)
                Debug.LogError($"[ReverseGrowthGenerator] CRITICAL: Dynamic sort and validator mismatch! {solveError}");
                return (null, null);
            }

            Debug.Log($"[ReverseGrowthGenerator] Solution validated! Order: [{string.Join(", ", solutionOrder)}]");

            // Phase 6: 난이도 분석 (정보 로깅만 — 거부는 Caller가 결정)
            if (_maxFreeArrows > 0)
            {
                var (maxSimFree, freePerStep) = SolvabilityValidator.AnalyzeDifficulty(
                    _arrows, _gridWidth, _gridHeight);

                Debug.Log($"[ReverseGrowthGenerator] Difficulty info: maxFree={maxSimFree}, target={_maxFreeArrows}" +
                    (maxSimFree <= _maxFreeArrows ? " ✓" : $" (exceeds by {maxSimFree - _maxFreeArrows})"));
            }

            // 통계 출력
            int totalCells = CountTotalActiveCells();
            int occupiedCells = _occupied.Count;
            float fillRate = (float)occupiedCells / totalCells * 100f;
            int internalHeads = CountInternalHeads();

            Debug.Log($"[ReverseGrowthGenerator] ========== 완료 ==========");
            Debug.Log($"[ReverseGrowthGenerator] 화살표 수: {_arrows.Count}");
            Debug.Log($"[ReverseGrowthGenerator] Fill Rate: {fillRate:F1}% ({occupiedCells}/{totalCells})");
            Debug.Log($"[ReverseGrowthGenerator] 내부 Head: {internalHeads}/{_arrows.Count} ({(float)internalHeads / _arrows.Count * 100:F1}%)");

            return (_arrows, solutionOrder);
        }

        // ========== 의존성 그래프 관리 ==========

        /// <summary>
        /// 새 화살표가 사이클을 만드는지 미리 검사 (동적 시뮬레이션 기반)
        ///
        /// 핵심 변경 (2026-01-20):
        /// - 정적 의존성 그래프 기반 사이클 검사 → 동적 시뮬레이션 기반으로 변경
        /// - 새 화살표를 임시로 추가한 후 TopologicalSortDynamic()으로 검증
        /// - SolvabilityValidator와 100% 동일한 로직 사용
        ///
        /// 이유:
        /// - 정적 방식은 "첫 번째 blocker"만 기록하므로 다중 간접 의존성을 놓침
        /// - 30x30+ 큰 그리드에서 복잡한 의존성 체인 때문에 사이클 감지 실패
        /// - 동적 시뮬레이션만이 정확한 사이클 감지 가능
        /// </summary>
        private bool WouldCreateCycle(EditorArrow newArrow)
        {
            // 임시로 새 화살표 추가
            _arrows.Add(newArrow);

            // 동적 시뮬레이션으로 모든 화살표가 탈출 가능한지 확인
            var depGraph = new DependencyGraph(_gridWidth, _gridHeight);
            var sortResult = depGraph.TopologicalSortDynamic(_arrows);

            // 새 화살표 제거 (원래 상태로 복원)
            _arrows.RemoveAt(_arrows.Count - 1);

            // 모든 화살표가 정렬되지 않으면 사이클 또는 막힘 존재
            bool wouldCreateCycle = sortResult.Count != _arrows.Count + 1;

            return wouldCreateCycle;
        }

        /// <summary>
        /// Tail 확장이 사이클을 만드는지 미리 검사 (동적 시뮬레이션 기반)
        ///
        /// 핵심 변경 (2026-01-20):
        /// - 정적 의존성 기반 → 동적 시뮬레이션 기반으로 변경
        /// - 임시로 셀을 추가한 후 TopologicalSortDynamic()으로 검증
        /// </summary>
        private bool WouldTailExtensionCreateCycle(EditorArrow arrowToExtend, List<Vector2Int> newCells)
        {
            // 원래 셀 저장
            var originalCells = new List<Vector2Int>(arrowToExtend.cells);

            // 임시로 새 셀 추가
            arrowToExtend.cells.InsertRange(0, newCells);

            // 동적 시뮬레이션으로 모든 화살표가 탈출 가능한지 확인
            var depGraph = new DependencyGraph(_gridWidth, _gridHeight);
            var sortResult = depGraph.TopologicalSortDynamic(_arrows);

            // 원래 셀로 복원
            arrowToExtend.cells.Clear();
            arrowToExtend.cells.AddRange(originalCells);

            // 모든 화살표가 정렬되지 않으면 사이클 또는 막힘 존재
            bool wouldCreateCycle = sortResult.Count != _arrows.Count;

            return wouldCreateCycle;
        }

        /// <summary>
        /// 화살표 생성 시 의존성 기록
        /// Head 탈출 경로에 다른 화살표의 셀(Body 또는 Head)이 있으면 의존 관계 성립
        ///
        /// 핵심 수정: Body뿐만 아니라 Head도 막힘으로 인식
        /// SolvabilityValidator와 동일한 로직 적용
        /// </summary>
        private void RecordDependency(EditorArrow arrow)
        {
            Vector2Int head = arrow.cells[arrow.cells.Count - 1];
            Vector2Int dir = GetDirectionVector(arrow.headDirection);
            Vector2Int pos = head + dir;

            int dependencyTarget = -1;  // -1 = 경계 직접 탈출

            while (IsInBounds(pos))
            {
                // 다른 화살표의 셀(Body 또는 Head)에 막히면 의존 관계 성립
                foreach (var other in _arrows)
                {
                    if (other.id == arrow.id) continue;

                    // 핵심 수정: 모든 셀 확인 (Head 포함)
                    // SolvabilityValidator의 CanEscapeNow와 동일한 로직
                    if (other.cells.Contains(pos))
                    {
                        dependencyTarget = other.id;
                        break;
                    }
                }
                if (dependencyTarget >= 0) break;
                pos += dir;
            }

            _dependsOn[arrow.id] = dependencyTarget;

            if (dependencyTarget >= 0)
            {
                Debug.Log($"[ReverseGrowthGenerator] Arrow {arrow.id} depends on Arrow {dependencyTarget}");
            }
        }

        /// <summary>
        /// Gap Filling 후 모든 화살표의 의존성을 재계산
        /// Tail 확장으로 기존 화살표의 Body가 변경되어 의존 관계가 달라질 수 있음
        ///
        /// 핵심 수정: Body뿐만 아니라 Head도 막힘으로 인식
        /// SolvabilityValidator와 동일한 로직 적용
        /// </summary>
        private void RecalculateAllDependencies()
        {
            // 이전 의존성 저장 (변경 감지용)
            var oldDependencies = new Dictionary<int, int>(_dependsOn);

            // 의존성 그래프 초기화
            _dependsOn.Clear();

            int changedCount = 0;

            // 모든 화살표에 대해 의존성 재계산
            foreach (var arrow in _arrows)
            {
                int oldDependency = oldDependencies.ContainsKey(arrow.id) ? oldDependencies[arrow.id] : -999;

                // RecordDependency와 동일한 로직
                Vector2Int head = arrow.cells[arrow.cells.Count - 1];
                Vector2Int dir = GetDirectionVector(arrow.headDirection);
                Vector2Int pos = head + dir;

                int dependencyTarget = -1;  // -1 = 경계 직접 탈출

                while (IsInBounds(pos))
                {
                    // 핵심 수정: 다른 화살표의 셀(Body 또는 Head)에 막히면 의존 관계 성립
                    // SolvabilityValidator의 CanEscapeNow와 동일한 로직
                    foreach (var other in _arrows)
                    {
                        if (other.id == arrow.id) continue;

                        // 모든 셀 확인 (Head 포함)
                        if (other.cells.Contains(pos))
                        {
                            dependencyTarget = other.id;
                            break;
                        }
                    }
                    if (dependencyTarget >= 0) break;
                    pos += dir;
                }

                _dependsOn[arrow.id] = dependencyTarget;

                // 변경 감지
                if (oldDependency != -999 && oldDependency != dependencyTarget)
                {
                    changedCount++;
                    Debug.Log($"[ReverseGrowthGenerator] Arrow {arrow.id} dependency changed: {oldDependency} → {dependencyTarget}");
                }
            }

            Debug.Log($"[ReverseGrowthGenerator] 의존성 재계산 완료: {changedCount}개 변경됨");
        }

        /// <summary>
        /// Tail 확장 후 영향받는 화살표의 의존성 업데이트
        /// 새 Body 셀로 인해 의존 관계가 변경될 수 있음
        /// </summary>
        private void UpdateDependenciesAfterTailExtension(EditorArrow extendedArrow, List<Vector2Int> newCells)
        {
            var newBodyCells = new HashSet<Vector2Int>(newCells);

            foreach (var other in _arrows)
            {
                if (other.id == extendedArrow.id)
                    continue;

                // other의 탈출 경로가 새 Body 셀을 지나는지 확인
                Vector2Int otherHead = other.cells[other.cells.Count - 1];
                Vector2Int otherDir = GetDirectionVector(other.headDirection);
                Vector2Int otherPos = otherHead + otherDir;

                while (IsInBounds(otherPos))
                {
                    if (newBodyCells.Contains(otherPos))
                    {
                        // other가 extendedArrow에 의존하게 됨
                        int oldDep = _dependsOn.ContainsKey(other.id) ? _dependsOn[other.id] : -1;
                        if (oldDep != extendedArrow.id)
                        {
                            _dependsOn[other.id] = extendedArrow.id;
                            Debug.Log($"[ReverseGrowthGenerator] Tail extension: Arrow {other.id} now depends on Arrow {extendedArrow.id}");
                        }
                        break;
                    }

                    // 기존 화살표에 막히면 중단
                    bool blocked = false;
                    foreach (var existing in _arrows)
                    {
                        if (existing.id != other.id && existing.cells.Contains(otherPos))
                        {
                            blocked = true;
                            break;
                        }
                    }
                    if (blocked) break;

                    otherPos += otherDir;
                }
            }
        }

        /// <summary>
        /// 의존성 그래프를 기반으로 해답 순서 계산 (Topological Sort - Kahn's Algorithm)
        /// </summary>
        private List<int> ComputeSolutionOrder()
        {
            var result = new List<int>();
            var inDegree = new Dictionary<int, int>();
            var graph = new Dictionary<int, List<int>>();  // A → B: A가 탈출해야 B가 탈출 가능

            // 그래프 초기화
            foreach (var arrow in _arrows)
            {
                inDegree[arrow.id] = 0;
                graph[arrow.id] = new List<int>();
            }

            // 의존성 → 그래프 변환
            // A가 B에 의존 → B가 먼저 탈출해야 함 → graph[B].Add(A)
            foreach (var kvp in _dependsOn)
            {
                int arrowId = kvp.Key;
                int dependsOnId = kvp.Value;

                if (dependsOnId >= 0 && graph.ContainsKey(dependsOnId))
                {
                    graph[dependsOnId].Add(arrowId);
                    inDegree[arrowId]++;
                }
            }

            // Kahn's Algorithm
            var queue = new Queue<int>();
            foreach (var kvp in inDegree)
            {
                if (kvp.Value == 0)
                    queue.Enqueue(kvp.Key);
            }

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                result.Add(current);

                foreach (int next in graph[current])
                {
                    inDegree[next]--;
                    if (inDegree[next] == 0)
                        queue.Enqueue(next);
                }
            }

            // 사이클 감지
            if (result.Count != _arrows.Count)
            {
                Debug.LogError($"[ReverseGrowthGenerator] Cycle detected! Only {result.Count}/{_arrows.Count} arrows in solution");
                // Fallback: creationOrder 역순 (기존 방식)
                var fallback = new List<int>(_creationOrder);
                fallback.Reverse();
                return fallback;
            }

            Debug.Log($"[ReverseGrowthGenerator] Topological sort successful: [{string.Join(", ", result)}]");
            return result;
        }

        // ========== Phase 1: ValidTargets 초기화 ==========

        /// <summary>
        /// 경계 셀을 ValidTargets로 초기화
        /// 경계 = 그리드 외부 바로 옆 셀 (가상의 탈출 지점)
        ///
        /// Shape Mask 사용 시: Shape 경계도 ValidTarget으로 추가
        /// - 활성 셀에 인접한 비활성 셀 = 탈출 가능 지점
        /// </summary>
        private void InitializeValidTargets()
        {
            _validTargets.Clear();

            // 1. 그리드 경계 외부 셀을 ValidTarget으로 추가 (항상 실행)
            AddGridBoundaryTargets();

            // 2. Shape Mask 사용 시: Shape 경계도 ValidTarget으로 추가
            if (_useShapeMask && _shapeMask != null)
            {
                AddShapeBoundaryTargets();
            }
        }

        /// <summary>
        /// 그리드 경계 외부를 ValidTarget으로 추가
        /// </summary>
        private void AddGridBoundaryTargets()
        {
            // 상단 경계 (y = _gridHeight)
            for (int x = 0; x < _gridWidth; x++)
            {
                _validTargets.Add(new Vector2Int(x, _gridHeight));
            }

            // 하단 경계 (y = -1)
            for (int x = 0; x < _gridWidth; x++)
            {
                _validTargets.Add(new Vector2Int(x, -1));
            }

            // 좌측 경계 (x = -1)
            for (int y = 0; y < _gridHeight; y++)
            {
                _validTargets.Add(new Vector2Int(-1, y));
            }

            // 우측 경계 (x = _gridWidth)
            for (int y = 0; y < _gridHeight; y++)
            {
                _validTargets.Add(new Vector2Int(_gridWidth, y));
            }
        }

        /// <summary>
        /// Shape Mask 경계를 ValidTarget으로 추가
        /// 활성 셀에 인접한 비활성 셀 = 탈출 가능 지점
        ///
        /// 이를 통해 Image Import 시 Shape 가장자리에서도 화살표가 탈출할 수 있음
        /// </summary>
        private void AddShapeBoundaryTargets()
        {
            int addedCount = 0;

            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    // 활성 셀만 검사
                    if (!_shapeMask[x, y]) continue;

                    Vector2Int activeCell = new Vector2Int(x, y);

                    // 4방향 검사
                    foreach (var dir in GetAllDirections())
                    {
                        Vector2Int neighbor = activeCell + GetDirectionVector(dir);

                        // 인접 셀이 비활성이거나 그리드 외부면 = Shape 경계
                        bool isShapeBoundary = false;

                        if (!IsInBounds(neighbor))
                        {
                            // 그리드 외부 (이미 AddGridBoundaryTargets에서 추가됨)
                            continue;
                        }
                        else if (!_shapeMask[neighbor.x, neighbor.y])
                        {
                            // 그리드 내부지만 비활성 셀 = Shape 내부 경계
                            isShapeBoundary = true;
                        }

                        if (isShapeBoundary && !_validTargets.Contains(neighbor))
                        {
                            _validTargets.Add(neighbor);
                            addedCount++;
                        }
                    }
                }
            }

            if (addedCount > 0)
            {
                Debug.Log($"[ReverseGrowthGenerator] Shape 경계 ValidTargets 추가: {addedCount}개");
            }
        }

        // ========== Phase 2: 화살표 생성 ==========

        /// <summary>
        /// ValidTarget에 인접한 빈 셀에서 화살표 생성 시도
        /// </summary>
        private EditorArrow TryCreateArrow(int arrowId)
        {
            // 2a. Head 후보 찾기
            var headCandidates = FindHeadCandidates();

            if (headCandidates.Count == 0)
                return null;

            // 셔플하여 다양성 확보
            ShuffleList(headCandidates);

            // 각 후보에서 화살표 생성 시도
            foreach (var (headCell, headDir) in headCandidates)
            {
                var arrow = CreateArrowFromHead(headCell, headDir, arrowId);

                if (arrow != null)
                    return arrow;
            }

            return null;
        }

        /// <summary>
        /// ValidTarget에 인접한 빈 셀 찾기 = Head 후보
        /// </summary>
        private List<(Vector2Int cell, ArrowDirection dir)> FindHeadCandidates()
        {
            var candidates = new List<(Vector2Int, ArrowDirection)>();
            var seen = new HashSet<(Vector2Int, ArrowDirection)>();

            foreach (var target in _validTargets)
            {
                // 4방향 탐색
                foreach (var dir in GetAllDirections())
                {
                    // target에서 dir의 반대 방향 = Head 후보 위치
                    Vector2Int headCell = target + GetDirectionVector(GetOppositeDirection(dir));

                    // 조건 체크
                    if (!IsValidCell(headCell)) continue;
                    if (_occupied.Contains(headCell)) continue;
                    if (_validTargets.Contains(headCell)) continue;  // ValidTarget 자체는 제외

                    // 중복 방지
                    var key = (headCell, dir);
                    if (seen.Contains(key)) continue;
                    seen.Add(key);

                    // headDir = Head에서 Target으로 가는 방향 = 탈출 방향
                    candidates.Add((headCell, dir));
                }
            }

            return candidates;
        }

        /// <summary>
        /// Head에서 시작하여 역방향으로 Body 성장
        /// </summary>
        private EditorArrow CreateArrowFromHead(Vector2Int headCell, ArrowDirection headDir, int arrowId)
        {
            // 탈출 경로 검증: Head → headDir 방향으로 탈출 가능한지 확인
            if (!CanEscapeInDirection(headCell, headDir))
                return null;

            // cells는 [Head, Body1, Body2, ..., Tail] 순서로 성장
            var cells = new List<Vector2Int> { headCell };
            int targetLen = _random.Next(_minArrowLength, _maxArrowLength + 1);

            ArrowDirection growDir = GetOppositeDirection(headDir);
            Vector2Int current = headCell;

            // Geometric 패턴: 방향 시퀀스 미리 생성
            List<ArrowDirection> directionSequence = null;
            int sequenceIndex = 0;
            GeometricPatternType chosenPattern = GeometricPatternType.None;

            if (_useGeometricPatterns && _enabledPatterns != GeometricPatternType.None)
            {
                chosenPattern = GeometricPatternGenerator.PickRandomEnabled(
                    _enabledPatterns, targetLen, _random);
                directionSequence = GeometricPatternGenerator.GenerateDirectionSequence(
                    chosenPattern, growDir, targetLen, _random);
            }

            int patternFollowed = 0;
            int patternFallback = 0;

            while (cells.Count < targetLen)
            {
                Vector2Int? nextCell;

                if (directionSequence != null && sequenceIndex < directionSequence.Count)
                {
                    // Geometric 모드: 시퀀스의 방향으로 이동 시도
                    nextCell = TryMoveInDirection(current, directionSequence[sequenceIndex], cells);

                    if (nextCell.HasValue)
                    {
                        sequenceIndex++;
                        patternFollowed++;
                    }
                    else
                    {
                        // 계획된 방향이 막혀있으면 → 기존 랜덤 fallback
                        nextCell = FindNextBodyCell(current, growDir, cells);
                        sequenceIndex++; // 시퀀스도 진행 (다음 스텝은 다시 패턴 시도)
                        patternFallback++;
                    }
                }
                else
                {
                    // 시퀀스 소진 또는 비활성 → 기존 방식
                    nextCell = FindNextBodyCell(current, growDir, cells);
                }

                if (!nextCell.HasValue)
                    break;

                cells.Add(nextCell.Value);
                growDir = GetDirectionFromTo(current, nextCell.Value);
                current = nextCell.Value;
            }

            if (chosenPattern != GeometricPatternType.None && patternFollowed + patternFallback > 0)
            {
                float successRate = (float)patternFollowed / (patternFollowed + patternFallback) * 100f;
                if (successRate < 50f)
                    Debug.Log($"[ReverseGrowth] Pattern {chosenPattern} arrow {arrowId}: {successRate:F0}% followed ({patternFollowed}/{patternFollowed + patternFallback})");
            }

            // 최소 길이 검증
            if (cells.Count < _minArrowLength)
                return null;

            // 뒤집기: [Head, Body1, ..., Tail] → [Tail, ..., Body1, Head]
            cells.Reverse();

            // 검증: HEAD 정렬 규칙
            if (!ValidateHeadAlignment(cells, headDir))
            {
                return null;  // 경고 없이 조용히 실패
            }

            // 검증: 자기참조 금지
            if (!ValidateNoSelfReference(cells, headDir))
            {
                Debug.LogWarning($"[ReverseGrowthGenerator] Self-reference detected for arrow {arrowId}");
                return null;
            }

            // Phase 2 핵심: 사이클 방지 검사
            var tempArrow = new EditorArrow
            {
                id = arrowId,
                cells = cells,
                headDirection = headDir,
                color = GameColor.Red
            };

            if (WouldCreateCycle(tempArrow))
            {
                // 사이클이 발생할 것으로 예상되면 이 화살표 생성 거부
                return null;
            }

            return tempArrow;
        }

        /// <summary>
        /// Body 성장을 위한 다음 셀 찾기
        /// 핵심: 새 셀이 기존 화살표의 탈출 경로를 막지 않아야 함
        /// </summary>
        private Vector2Int? FindNextBodyCell(Vector2Int current, ArrowDirection preferDir, List<Vector2Int> currentCells)
        {
            var candidates = new List<(Vector2Int cell, int priority)>();

            foreach (var dir in GetShuffledDirections())
            {
                // 역방향은 가급적 피함 (지그재그 방지)
                if (dir == GetOppositeDirection(preferDir) && currentCells.Count > 1)
                    continue;

                Vector2Int next = current + GetDirectionVector(dir);

                if (!IsValidCell(next)) continue;
                if (_occupied.Contains(next)) continue;
                if (currentCells.Contains(next)) continue;

                // 핵심 추가: 이 셀이 기존 화살표의 탈출 경로를 막는지 확인
                if (BlocksExistingArrowEscape(next))
                    continue;

                // 우선순위: 선호 방향 우선
                int priority = (dir == preferDir) ? 0 : 1;
                candidates.Add((next, priority));
            }

            if (candidates.Count == 0)
                return null;

            // 우선순위 정렬 후 상위에서 랜덤 선택
            candidates.Sort((a, b) => a.priority.CompareTo(b.priority));
            int topPriority = candidates[0].priority;
            var topCandidates = candidates.Where(c => c.priority == topPriority).ToList();

            return topCandidates[_random.Next(topCandidates.Count)].cell;
        }

        /// <summary>
        /// 지정된 방향으로 이동 시도 (Geometric 패턴용)
        /// FindNextBodyCell과 동일한 제약 조건 적용, 단 반대 방향 제한 없음
        /// (패턴이 의도적으로 반대 방향을 지시할 수 있으므로)
        /// </summary>
        private Vector2Int? TryMoveInDirection(
            Vector2Int current, ArrowDirection dir, List<Vector2Int> currentCells)
        {
            Vector2Int next = current + GetDirectionVector(dir);

            if (!IsValidCell(next)) return null;
            if (_occupied.Contains(next)) return null;
            if (currentCells.Contains(next)) return null;
            if (BlocksExistingArrowEscape(next)) return null;

            return next;
        }

        /// <summary>
        /// 새 셀이 기존 화살표의 탈출 경로를 막는지 확인
        /// Phase 2에서 사용 - 새 화살표 Body가 기존 화살표를 막으면 안 됨
        ///
        /// 핵심 규칙:
        /// - 기존 화살표(arrow)가 탈출하려면, 그 탈출 경로에 "의존할 수 있는 화살표"가 있어야 함
        /// - 의존 가능 = 경계 또는 기존에 생성된(= ID가 더 작은) 화살표 Body
        /// - 새 셀(newCell)이 탈출 경로에 있어도, 첫 번째 탈출 셀이 의존 가능하면 OK
        /// - 새 셀이 첫 번째 탈출 셀이면 → 새 화살표에 의존하게 됨 → OK (DAG 유지)
        /// </summary>
        private bool BlocksExistingArrowEscape(Vector2Int newCell)
        {
            foreach (var arrow in _arrows)
            {
                // 이 셀이 arrow의 탈출 경로에 있는지 확인
                if (!IsOnHeadEscapePath(arrow, newCell))
                    continue;  // 탈출 경로에 없으면 영향 없음

                // arrow의 탈출 경로 분석
                Vector2Int head = arrow.cells[arrow.cells.Count - 1];
                Vector2Int dir = GetDirectionVector(arrow.headDirection);
                Vector2Int firstEscapeCell = head + dir;

                // Case 1: 첫 번째 탈출 셀이 경계 밖 → 직접 탈출 가능 → OK
                if (!IsInBounds(firstEscapeCell))
                    continue;

                // Case 2: 첫 번째 탈출 셀이 newCell → 새 화살표에 의존 → OK (DAG: arrow → new arrow)
                if (firstEscapeCell == newCell)
                    continue;

                // Case 3: 첫 번째 탈출 셀이 이미 점유됨 (기존 화살표) → 기존 의존 관계 유지 → OK
                if (_occupied.Contains(firstEscapeCell))
                    continue;

                // Case 4: 첫 번째 탈출 셀이 빈 셀 → 아직 의존 대상 없음
                // 이 경우 newCell이 탈출 경로에 있으면 막힘으로 판정
                // (나중에 firstEscapeCell에 화살표가 생겨도, newCell 때문에 막힐 수 있음)
                return true;
            }
            return false;
        }

        /// <summary>
        /// Body 셀을 ValidTargets에 추가
        /// </summary>
        private void AddBodyToValidTargets(EditorArrow arrow)
        {
            // Head를 제외한 Body 셀만 추가
            // (Head는 다른 화살표의 Target이 되면 안 됨 - 자기 참조 문제 방지)
            for (int i = 0; i < arrow.cells.Count - 1; i++)
            {
                _validTargets.Add(arrow.cells[i]);
            }
        }

        // ========== Phase 3: Gap Filling ==========

        /// <summary>
        /// 남은 빈 셀을 채우기 (꼬리 연장 또는 새 화살표)
        /// </summary>
        private void FillGaps(ref int arrowId)
        {
            int maxIterations = _gridWidth * _gridHeight * 2;
            int iteration = 0;

            // 실패한 셀 추적 (무한 루프 방지)
            var failedCells = new HashSet<Vector2Int>();

            while (HasEmptyCells() && iteration < maxIterations)
            {
                iteration++;

                // 빈 셀 찾기 (실패한 셀 제외)
                var emptyCell = FindAnyEmptyCellExcluding(failedCells);
                if (!emptyCell.HasValue)
                {
                    // 모든 빈 셀이 실패 목록에 있으면 → 다시 시도
                    if (failedCells.Count > 0 && HasEmptyCells())
                    {
                        Debug.Log($"[ReverseGrowthGenerator] Retrying {failedCells.Count} failed cells...");
                        failedCells.Clear();
                        continue;
                    }
                    break;
                }

                // 방법 1: 인접 화살표의 Tail 연장
                if (TryExtendNearbyTail(emptyCell.Value))
                {
                    // 성공 시 인접 실패 셀들도 다시 시도 가능하도록 제거
                    RemoveAdjacentFromFailedCells(emptyCell.Value, failedCells);
                    continue;
                }

                // 방법 2: 새 화살표 생성 (ValidTarget 근처)
                var newArrow = TryCreateSmallArrow(emptyCell.Value, arrowId);
                if (newArrow != null)
                {
                    _arrows.Add(newArrow);
                    _creationOrder.Add(newArrow.id);
                    AddBodyToValidTargets(newArrow);
                    MarkOccupied(newArrow.cells);

                    // Gap Filling에서도 의존성 기록 (Gemini/GPT 권장)
                    RecordDependency(newArrow);

                    arrowId++;

                    // 성공 시 인접 실패 셀들도 다시 시도 가능
                    foreach (var cell in newArrow.cells)
                    {
                        RemoveAdjacentFromFailedCells(cell, failedCells);
                    }
                    continue;
                }

                // 방법 3: 마지막 수단 - 연결된 빈 셀로 Tail 연장 (우회)
                if (TryExtendTailToCell(emptyCell.Value))
                {
                    RemoveAdjacentFromFailedCells(emptyCell.Value, failedCells);
                    continue;
                }

                // 실패 - 이 셀을 실패 목록에 추가하고 건���뜀
                failedCells.Add(emptyCell.Value);
            }

            // 마지막 수단: 남은 빈 셀을 강제 병합
            var finalRemainingCells = GetAllEmptyCells();
            if (finalRemainingCells.Count > 0)
            {
                Debug.Log($"[ReverseGrowthGenerator] Attempting forced merge for {finalRemainingCells.Count} remaining cells...");

                foreach (var emptyCell in finalRemainingCells.ToList())
                {
                    if (TryForceMergeToNearestArrow(emptyCell))
                    {
                        Debug.Log($"[ReverseGrowthGenerator] Forced merge successful for {emptyCell}");
                    }
                }
            }

            // 최최후의 수단: 남은 빈 셀에 1칸짜리 화살표 생성
            var afterForceMerge = GetAllEmptyCells();
            if (afterForceMerge.Count > 0)
            {
                Debug.Log($"[ReverseGrowthGenerator] Creating single-cell arrows for {afterForceMerge.Count} remaining cells...");

                foreach (var emptyCell in afterForceMerge.ToList())
                {
                    // 이 셀에서 탈출 가능한 방향 찾기
                    var singleArrow = TryCreateSingleCellArrow(emptyCell, arrowId);
                    if (singleArrow != null)
                    {
                        _arrows.Add(singleArrow);
                        _creationOrder.Add(singleArrow.id);
                        _occupied.Add(emptyCell);
                        _validTargets.Add(emptyCell);
                        RecordDependency(singleArrow);
                        arrowId++;

                        Debug.Log($"[ReverseGrowthGenerator] Single-cell arrow created at {emptyCell}, Dir={singleArrow.headDirection}");
                    }
                }
            }

            // 최종 실패 셀 보고
            var stillRemaining = GetAllEmptyCells();
            if (stillRemaining.Count > 0)
            {
                Debug.LogWarning($"[ReverseGrowthGenerator] Gap Filling failed for {stillRemaining.Count} cells: " +
                    string.Join(", ", stillRemaining.Take(5)) + (stillRemaining.Count > 5 ? "..." : ""));
            }
        }

        /// <summary>
        /// 실패 목록에서 특정 셀의 인접 셀들을 제거 (다시 시도 가능하게)
        /// </summary>
        private void RemoveAdjacentFromFailedCells(Vector2Int cell, HashSet<Vector2Int> failedCells)
        {
            foreach (var dir in GetAllDirections())
            {
                Vector2Int adjacent = cell + GetDirectionVector(dir);
                failedCells.Remove(adjacent);
            }
        }

        /// <summary>
        /// 실패 목록을 제외한 빈 셀 찾기
        /// </summary>
        private Vector2Int? FindAnyEmptyCellExcluding(HashSet<Vector2Int> excludeCells)
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!_occupied.Contains(cell) && IsCellActive(x, y) && !excludeCells.Contains(cell))
                    {
                        return cell;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 모든 빈 셀 목록 반환
        /// </summary>
        private List<Vector2Int> GetAllEmptyCells()
        {
            var emptyCells = new List<Vector2Int>();
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!_occupied.Contains(cell) && IsCellActive(x, y))
                    {
                        emptyCells.Add(cell);
                    }
                }
            }
            return emptyCells;
        }

        /// <summary>
        /// 인접 화살표의 Tail을 연장하여 빈 셀 채우기
        /// </summary>
        private bool TryExtendNearbyTail(Vector2Int targetCell)
        {
            // 이 셀에 인접한 화살표의 Tail 찾기
            foreach (var arrow in _arrows)
            {
                Vector2Int tail = arrow.cells[0];

                // Tail이 targetCell에 인접한지 확인
                Vector2Int diff = targetCell - tail;
                if (Mathf.Abs(diff.x) + Mathf.Abs(diff.y) != 1)
                    continue;

                // 자기참조 검증 (강화됨): 새 셀이 이 화살표의 Head 탈출 Ray 위에 있는지 확인
                // IsOnHeadEscapePath 대신 IsOnHeadEscapeRay 사용 (occupied 무시, 전체 경로 검사)
                if (IsOnHeadEscapeRay(arrow, targetCell))
                    continue;

                // 핵심 추가: 새 셀이 **다른 화살표**의 탈출 경로를 막는지 확인
                if (BlocksAnyArrowEscape(targetCell, arrow))
                    continue;

                // 핵심 추가 (Phase 2.5): Tail 확장이 사이클을 만드는지 미리 검사
                var newCells = new List<Vector2Int> { targetCell };
                if (WouldTailExtensionCreateCycle(arrow, newCells))
                    continue;

                // Tail 연장 (cells[0]에 새 셀 삽입)
                arrow.cells.Insert(0, targetCell);
                _occupied.Add(targetCell);

                // ValidTargets 업데이트 (새 Tail은 ValidTarget이 됨)
                _validTargets.Add(targetCell);

                // 의존성 그래프 업데이트 (새 Body로 인해 의존 관계 변경될 수 있음)
                UpdateDependenciesAfterTailExtension(arrow, newCells);

                return true;
            }

            return false;
        }

        /// <summary>
        /// 새 셀이 다른 화살표의 탈출 경로를 막는지 확인
        ///
        /// 핵심 규칙:
        /// - 새 셀(cell)이 다른 arrow의 탈출 경로에 있으면 막힘 가능
        /// - 단, 새 셀이 excludeArrow의 Body가 되면 → arrow가 excludeArrow에 의존하게 됨
        /// - 따라서 새 셀이 추가된 후 arrow의 첫 탈출 셀이 새 셀이면 → 의존성 생성 → OK
        /// </summary>
        private bool BlocksAnyArrowEscape(Vector2Int newCell, EditorArrow excludeArrow)
        {
            foreach (var arrow in _arrows)
            {
                if (arrow == excludeArrow)
                    continue;

                // 이 셀이 arrow의 탈출 경로에 있는지 확인
                if (IsOnHeadEscapePath(arrow, newCell))
                {
                    // arrow의 첫 번째 탈출 셀 계산
                    Vector2Int head = arrow.cells[arrow.cells.Count - 1];
                    Vector2Int dir = GetDirectionVector(arrow.headDirection);
                    Vector2Int firstEscapeCell = head + dir;

                    // Case 1: 새 셀이 첫 번째 탈출 셀이면 → 의존성 생성 → OK
                    if (firstEscapeCell == newCell)
                        continue;  // 이 arrow는 excludeArrow에 의존하게 됨

                    // Case 2: 첫 번째 탈출 셀이 경계 밖이면 → 직접 탈출 가능
                    if (!IsInBounds(firstEscapeCell))
                        continue;  // arrow는 경계로 탈출 가능

                    // Case 3: 첫 번째 탈출 셀이 기존 excludeArrow의 Body에 있으면 → 이미 의존 중
                    bool alreadyDependsOnExclude = false;
                    for (int i = 0; i < excludeArrow.cells.Count - 1; i++)  // Body only
                    {
                        if (excludeArrow.cells[i] == firstEscapeCell)
                        {
                            alreadyDependsOnExclude = true;
                            break;
                        }
                    }
                    if (alreadyDependsOnExclude)
                        continue;

                    // Case 4: 첫 번째 탈출 셀이 다른 화살표에 막혀 있으면 → 그 화살표에 의존
                    // (newCell과 무관하게 탈출 가능)
                    if (_occupied.Contains(firstEscapeCell) && firstEscapeCell != newCell)
                        continue;

                    // 위 조건 모두 불충족 → 막힘으로 판정
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 셀이 화살표의 Head 탈출 경로에 있는지 확인
        /// </summary>
        private bool IsOnHeadEscapePath(EditorArrow arrow, Vector2Int cell)
        {
            Vector2Int head = arrow.cells[arrow.cells.Count - 1];
            Vector2Int dir = GetDirectionVector(arrow.headDirection);
            Vector2Int pos = head + dir;

            while (IsInBounds(pos))
            {
                if (pos == cell)
                    return true;  // 탈출 경로에 있음!

                // 다른 화살표에 막히면 더 이상 확인 불필요
                if (_occupied.Contains(pos))
                    break;

                pos += dir;
            }

            return false;
        }

        /// <summary>
        /// 빈 셀 근처에 작은 화살표 생성
        ///
        /// 핵심: 새 화살표의 Body가 기존 화살표의 탈출 경로를 막으면 안 됨
        /// </summary>
        private EditorArrow TryCreateSmallArrow(Vector2Int targetCell, int arrowId)
        {
            // 이 셀이 ValidTarget에 인접한지 확인
            foreach (var dir in GetAllDirections())
            {
                Vector2Int targetPos = targetCell + GetDirectionVector(dir);

                if (_validTargets.Contains(targetPos))
                {
                    // targetCell을 Head로, dir을 headDirection으로 2셀 화살표 생성
                    ArrowDirection headDir = dir;
                    ArrowDirection growDir = GetOppositeDirection(headDir);
                    Vector2Int tailCell = targetCell + GetDirectionVector(growDir);

                    if (IsValidCell(tailCell) && !_occupied.Contains(tailCell))
                    {
                        var cells = new List<Vector2Int> { tailCell, targetCell };

                        // 검증: HEAD 정렬 + 자기참조 + 탈출 가능성
                        if (!ValidateHeadAlignment(cells, headDir))
                            continue;
                        if (!ValidateNoSelfReference(cells, headDir))
                            continue;
                        if (!CanEscapeInDirection(targetCell, headDir))
                            continue;

                        // 핵심 추가: 새 화살표의 Body(tailCell)가 기존 화살표의 탈출을 막는지 확인
                        if (BlocksExistingArrowEscape(tailCell))
                            continue;

                        // 추가: headCell도 다른 화살표의 탈출 경로를 막는지 확인
                        // (headCell = targetCell이지만, 다른 화살표에 대해서는 Body로 작용)
                        if (BlocksExistingArrowEscapeAsHead(targetCell, headDir))
                            continue;

                        // Phase 2 핵심: 사이클 방지 검사
                        var tempArrow = new EditorArrow
                        {
                            id = arrowId,
                            cells = cells,
                            headDirection = headDir,
                            color = GameColor.Red
                        };

                        if (WouldCreateCycle(tempArrow))
                            continue;

                        return tempArrow;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Head 셀이 기존 화살표의 탈출 경로를 막는지 확인
        /// Head는 다른 화살표의 의존 대상이 될 수 없음 (Head는 ValidTarget이 아님)
        /// </summary>
        private bool BlocksExistingArrowEscapeAsHead(Vector2Int headCell, ArrowDirection headDir)
        {
            foreach (var arrow in _arrows)
            {
                // 이 셀이 arrow의 탈출 경로에 있는지 확인
                if (!IsOnHeadEscapePath(arrow, headCell))
                    continue;

                // arrow의 탈출 경로 분석
                Vector2Int head = arrow.cells[arrow.cells.Count - 1];
                Vector2Int dir = GetDirectionVector(arrow.headDirection);
                Vector2Int firstEscapeCell = head + dir;

                // 첫 번째 탈출 셀이 경계 밖 → 직접 탈출 가능 → OK
                if (!IsInBounds(firstEscapeCell))
                    continue;

                // 첫 번째 탈출 셀이 headCell이면 → 막힘 (Head는 의존 대상이 될 수 없음)
                if (firstEscapeCell == headCell)
                    return true;

                // 첫 번째 탈출 셀이 이미 점유됨 → 기존 의존 관계 유지 → OK
                if (_occupied.Contains(firstEscapeCell))
                    continue;

                // 첫 번째 탈출 셀이 빈 셀이고 headCell이 경로에 있으면 → 막힘
                return true;
            }
            return false;
        }

        /// <summary>
        /// Tail을 우회하여 특정 셀까지 연장
        ///
        /// 핵심: BFS 경로는 반드시 인접 셀로만 구성되어야 함 (distance=1)
        /// </summary>
        private bool TryExtendTailToCell(Vector2Int targetCell)
        {
            // 모든 화살표를 거리순으로 정렬하여 시도
            var arrowsByDistance = _arrows
                .Select(a => (arrow: a, dist: Mathf.Abs(targetCell.x - a.cells[0].x) + Mathf.Abs(targetCell.y - a.cells[0].y)))
                .Where(x => x.dist <= 10)
                .OrderBy(x => x.dist)
                .ToList();

            foreach (var (arrow, dist) in arrowsByDistance)
            {
                // BFS로 Tail에서 targetCell까지 경로 찾기
                var path = FindPathBFSSafe(arrow.cells[0], targetCell, arrow);

                if (path == null || path.Count == 0)
                    continue;

                // 핵심 검증 1: 경로의 모든 셀이 이 화살표의 탈출 경로에 없는지 확인
                // (Self-reference 방지 - 강화된 버전)
                bool pathSafe = true;
                foreach (var cell in path)
                {
                    // 화살표의 Head 탈출 경로 전체 계산 (occupied 무시하고 전체 Ray 검사)
                    if (IsOnHeadEscapeRay(arrow, cell))
                    {
                        pathSafe = false;
                        break;
                    }

                    // 다른 화살표의 탈출 경로도 체크
                    if (BlocksAnyArrowEscape(cell, arrow))
                    {
                        pathSafe = false;
                        break;
                    }
                }

                if (!pathSafe)
                    continue;

                // 핵심 검증 2: 경로 연속성 확인 (모든 셀이 인접해야 함)
                bool pathContiguous = true;
                Vector2Int prevCell = arrow.cells[0];  // Tail에서 시작

                for (int i = 0; i < path.Count; i++)
                {
                    Vector2Int currCell = path[i];
                    int distance = Mathf.Abs(currCell.x - prevCell.x) + Mathf.Abs(currCell.y - prevCell.y);

                    if (distance != 1)
                    {
                        pathContiguous = false;
                        Debug.LogWarning($"[ReverseGrowthGenerator] Non-contiguous path: {prevCell} → {currCell} (distance={distance})");
                        break;
                    }

                    prevCell = currCell;
                }

                if (!pathContiguous)
                    continue;

                // 경로를 Tail에 추가
                //
                // BFS 상황:
                // - start = arrow.cells[0] (기존 Tail)
                // - end = targetCell
                // - FindPathBFSSafe 반환값: [neighbor_of_start, ..., targetCell] (start 제외)
                // - path[0]은 start(기존 Tail)과 인접
                // - path[n-1]은 targetCell
                //
                // 원하는 결과:
                // - 새 cells = [targetCell, ..., path[0], 기존Tail, 기존Body..., Head]
                // - 즉, targetCell이 새 Tail이 됨
                //
                // 삽입 방법:
                // - path를 역순으로 cells[0]에 Insert하면:
                //   Insert(0, path[n-1]) → cells = [targetCell, 기존Tail, ...]
                //   Insert(0, path[n-2]) → cells = [path[n-2], targetCell, 기존Tail, ...]
                //   ...
                //   Insert(0, path[0]) → cells = [path[0], ..., targetCell, 기존Tail, ...]
                //
                //   결과: [path[0], path[1], ..., targetCell, 기존Tail, ...]
                //
                //   문제: path[0]이 새 Tail이 되는데, 우리는 targetCell이 새 Tail이 되길 원함!
                //
                // 올바른 방법:
                // - path를 정순으로 cells[0] 앞에 InsertRange
                //   결과: [path[0], path[1], ..., path[n-1](=targetCell), 기존Tail, ...]
                //
                //   문제: path[n-1](targetCell)과 기존Tail이 인접하지 않을 수 있음!
                //
                // 핵심 깨달음:
                // - BFS 경로가 start → ... → end 인데, start를 기존 Tail로 설정함
                // - 따라서 path = [start의 neighbor, ..., end]
                // - path[0]은 기존 Tail과 인접하고, path[n-1]은 targetCell
                // - path 내부는 연속적 (BFS가 보장)
                // - 하지만 path[n-1]과 기존 Tail은 인접하지 않음!
                //
                // 해결책:
                // - cells 앞에 path를 역순으로 붙이되, 기존 Tail은 유지
                // - 새 cells = [path[n-1], path[n-2], ..., path[0], 기존Tail, ...]
                // - 이때 path[0]과 기존Tail이 인접 (BFS가 보장)
                // - 그리고 path 내부도 역순이지만 연속적
                //
                // Insert 순서:
                // Insert(0, path[0]) → [path[0], 기존Tail, ...]
                // Insert(0, path[1]) → [path[1], path[0], 기존Tail, ...]
                // ...
                // Insert(0, path[n-1]) → [path[n-1], ..., path[0], 기존Tail, ...]
                //
                // 결과: [targetCell, ..., path[0], 기존Tail, ...] - 정확히 원하는 결과!

                // Phase 2.5: Tail 확장이 사이클을 만드는지 미리 검사
                if (WouldTailExtensionCreateCycle(arrow, path))
                {
                    Debug.Log($"[ReverseGrowthGenerator] TryExtendTailToCell: Path extension would create cycle for Arrow {arrow.id}, skipping.");
                    continue;
                }

                for (int i = 0; i < path.Count; i++)
                {
                    arrow.cells.Insert(0, path[i]);
                    _occupied.Add(path[i]);
                    _validTargets.Add(path[i]);
                }

                // Phase 2.5: 의존성 그래프 업데이트
                UpdateDependenciesAfterTailExtension(arrow, path);

                return true;
            }

            return false;
        }

        /// <summary>
        /// 셀이 화살표의 Head 탈출 Ray 위에 있는지 확인 (Self-reference 방지용)
        /// IsOnHeadEscapePath와 달리, occupied를 무시하고 경계까지 전체 Ray를 검사
        /// </summary>
        private bool IsOnHeadEscapeRay(EditorArrow arrow, Vector2Int cell)
        {
            Vector2Int head = arrow.cells[arrow.cells.Count - 1];
            Vector2Int dir = GetDirectionVector(arrow.headDirection);
            Vector2Int pos = head + dir;

            // 경계까지 전체 Ray 검사 (occupied 무시)
            while (IsInBounds(pos))
            {
                if (pos == cell)
                    return true;  // 탈출 Ray 위에 있음 → Self-reference 위험!

                pos += dir;
            }

            return false;
        }

        /// <summary>
        /// BFS로 경로 찾기 (자기참조 안전)
        /// </summary>
        private List<Vector2Int> FindPathBFSSafe(Vector2Int start, Vector2Int end, EditorArrow arrow)
        {
            var queue = new Queue<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var visited = new HashSet<Vector2Int>(arrow.cells);

            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (current == end)
                {
                    // 경로 복원
                    var path = new List<Vector2Int>();
                    var node = end;

                    while (node != start)
                    {
                        path.Add(node);
                        node = cameFrom[node];
                    }

                    path.Reverse();
                    return path;
                }

                foreach (var dir in GetAllDirections())
                {
                    Vector2Int next = current + GetDirectionVector(dir);

                    if (!IsValidCell(next)) continue;
                    if (_occupied.Contains(next) && next != end) continue;
                    if (visited.Contains(next)) continue;

                    // 추가 검증: 이 셀이 현재 화살표의 탈출 경로에 있으면 건너뜀
                    if (IsOnHeadEscapePath(arrow, next))
                        continue;

                    visited.Add(next);
                    cameFrom[next] = current;
                    queue.Enqueue(next);
                }
            }

            return null;
        }

        /// <summary>
        /// 마지막 수단: 빈 셀을 가장 가까운 화살표에 강제 병합
        /// Self-reference만 피하면 됨 (다른 화살표 차단은 의존성 재계산으로 해결)
        /// </summary>
        private bool TryForceMergeToNearestArrow(Vector2Int targetCell)
        {
            // 모든 화살표를 Tail 거리순으로 정렬
            var arrowsByDistance = _arrows
                .Select(a => (arrow: a, dist: Mathf.Abs(targetCell.x - a.cells[0].x) + Mathf.Abs(targetCell.y - a.cells[0].y)))
                .OrderBy(x => x.dist)
                .ToList();

            foreach (var (arrow, dist) in arrowsByDistance)
            {
                // 1. 직접 인접한 Tail이 있는지 확인 (가장 간단한 케이스)
                Vector2Int tail = arrow.cells[0];
                Vector2Int diff = targetCell - tail;

                if (Mathf.Abs(diff.x) + Mathf.Abs(diff.y) == 1)
                {
                    // Self-reference만 확인 (다른 화살표 차단 무시)
                    if (!IsOnHeadEscapeRay(arrow, targetCell))
                    {
                        // Phase 2.5: 사이클 검사 추가
                        var newCells = new List<Vector2Int> { targetCell };
                        if (WouldTailExtensionCreateCycle(arrow, newCells))
                            continue;

                        // 강제 병합
                        arrow.cells.Insert(0, targetCell);
                        _occupied.Add(targetCell);
                        _validTargets.Add(targetCell);

                        // 의존성 그래프 업데이트
                        UpdateDependenciesAfterTailExtension(arrow, newCells);
                        return true;
                    }
                }

                // 2. BFS로 경로 찾기 (Self-reference만 피함)
                var path = FindPathBFSForce(arrow.cells[0], targetCell, arrow);

                if (path == null || path.Count == 0)
                    continue;

                // Self-reference만 검증 (다른 화살표 차단 무시)
                bool pathSafe = true;
                foreach (var cell in path)
                {
                    if (IsOnHeadEscapeRay(arrow, cell))
                    {
                        pathSafe = false;
                        break;
                    }
                }

                if (!pathSafe)
                    continue;

                // 경로 연속성 확인
                bool pathContiguous = true;
                Vector2Int prevCell = arrow.cells[0];

                for (int i = 0; i < path.Count; i++)
                {
                    Vector2Int currCell = path[i];
                    int distance = Mathf.Abs(currCell.x - prevCell.x) + Mathf.Abs(currCell.y - prevCell.y);

                    if (distance != 1)
                    {
                        pathContiguous = false;
                        break;
                    }

                    prevCell = currCell;
                }

                if (!pathContiguous)
                    continue;

                // Phase 2.5: 사이클 검사 추가
                if (WouldTailExtensionCreateCycle(arrow, path))
                {
                    Debug.Log($"[ReverseGrowthGenerator] TryForceMerge: Path extension would create cycle for Arrow {arrow.id}, skipping.");
                    continue;
                }

                // 강제 병합
                for (int i = 0; i < path.Count; i++)
                {
                    arrow.cells.Insert(0, path[i]);
                    _occupied.Add(path[i]);
                    _validTargets.Add(path[i]);
                }

                // 의존성 그래�� 업데이트
                UpdateDependenciesAfterTailExtension(arrow, path);

                return true;
            }

            return false;
        }

        /// <summary>
        /// BFS로 경로 찾기 (강제 병합용 - 다른 화살표 차단 무시)
        /// </summary>
        private List<Vector2Int> FindPathBFSForce(Vector2Int start, Vector2Int end, EditorArrow arrow)
        {
            var queue = new Queue<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var visited = new HashSet<Vector2Int>(arrow.cells);

            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (current == end)
                {
                    // 경로 복원
                    var path = new List<Vector2Int>();
                    var node = end;

                    while (node != start)
                    {
                        path.Add(node);
                        node = cameFrom[node];
                    }

                    path.Reverse();
                    return path;
                }

                foreach (var dir in GetAllDirections())
                {
                    Vector2Int next = current + GetDirectionVector(dir);

                    if (!IsValidCell(next)) continue;
                    if (_occupied.Contains(next) && next != end) continue;
                    if (visited.Contains(next)) continue;

                    // Self-reference만 체크 (다른 화살표 차단 무시)
                    if (IsOnHeadEscapeRay(arrow, next))
                        continue;

                    visited.Add(next);
                    cameFrom[next] = current;
                    queue.Enqueue(next);
                }
            }

            return null;
        }

        /// <summary>
        /// BFS로 경로 찾기
        /// </summary>
        private List<Vector2Int> FindPathBFS(Vector2Int start, Vector2Int end, List<Vector2Int> excludeCells)
        {
            var queue = new Queue<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var visited = new HashSet<Vector2Int>(excludeCells);

            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (current == end)
                {
                    // 경로 복원
                    var path = new List<Vector2Int>();
                    var node = end;

                    while (node != start)
                    {
                        path.Add(node);
                        node = cameFrom[node];
                    }

                    path.Reverse();
                    return path;
                }

                foreach (var dir in GetAllDirections())
                {
                    Vector2Int next = current + GetDirectionVector(dir);

                    if (!IsValidCell(next)) continue;
                    if (_occupied.Contains(next) && next != end) continue;
                    if (visited.Contains(next)) continue;

                    visited.Add(next);
                    cameFrom[next] = current;
                    queue.Enqueue(next);
                }
            }

            return null;
        }

        // ========== Phase 4: 색상 할당 ==========

        private void AssignColors()
        {
            var availableColors = new List<GameColor>();
            for (int i = 0; i < _colorCount && i < 12; i++)
            {
                availableColors.Add((GameColor)i);
            }

            foreach (var arrow in _arrows)
            {
                // 색상 매핑이 활성화되어 있으면 PNG 색상 기반으로 결정
                if (_useColorMapping && _colorMap != null)
                {
                    arrow.color = GetDominantColorForCells(arrow.cells, availableColors);
                }
                else
                {
                    // 랜덤 색상
                    arrow.color = availableColors[_random.Next(availableColors.Count)];
                }
            }
        }

        /// <summary>
        /// 화살표 셀들에서 가장 흔한 색상 반환 (색상 매핑용)
        /// </summary>
        private GameColor GetDominantColorForCells(List<Vector2Int> cells, List<GameColor> availableColors)
        {
            if (_colorMap == null || cells.Count == 0)
            {
                return availableColors[_random.Next(availableColors.Count)];
            }

            // 색상별 카운트
            var colorCounts = new Dictionary<GameColor, int>();

            foreach (var cell in cells)
            {
                if (cell.x >= 0 && cell.x < _colorMap.GetLength(0) &&
                    cell.y >= 0 && cell.y < _colorMap.GetLength(1))
                {
                    GameColor cellColor = _colorMap[cell.x, cell.y];

                    if (!colorCounts.ContainsKey(cellColor))
                        colorCounts[cellColor] = 0;
                    colorCounts[cellColor]++;
                }
            }

            if (colorCounts.Count == 0)
            {
                return availableColors[_random.Next(availableColors.Count)];
            }

            // 가장 흔한 색상 찾기
            GameColor dominantColor = availableColors[0];
            int maxCount = 0;

            foreach (var kvp in colorCounts)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    dominantColor = kvp.Key;
                }
            }

            return dominantColor;
        }

        // ========== Phase 5: 검증 ==========

        private (bool valid, List<string> errors) ValidateResult()
        {
            var errors = new List<string>();

            // ArrowValidator로 모든 화살표 검증
            var (arrowsValid, arrowErrors) = ArrowValidator.ValidateAll(_arrows, _gridWidth, _gridHeight);
            if (!arrowsValid)
            {
                errors.AddRange(arrowErrors);
            }

            // Fill Rate 검증
            int totalCells = CountTotalActiveCells();
            int occupiedCells = _occupied.Count;
            float fillRate = (float)occupiedCells / totalCells * 100f;

            if (fillRate < 99.9f)
            {
                errors.Add($"Fill rate is {fillRate:F1}% ({occupiedCells}/{totalCells}) - expected 100%");
            }

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// HEAD 정렬 규칙 검증
        /// </summary>
        private bool ValidateHeadAlignment(List<Vector2Int> cells, ArrowDirection headDir)
        {
            if (cells.Count < 2) return false;

            Vector2Int secondLast = cells[cells.Count - 2];
            Vector2Int last = cells[cells.Count - 1];
            ArrowDirection cellDir = GetDirectionFromTo(secondLast, last);

            return cellDir == headDir;
        }

        /// <summary>
        /// 자기참조 검증 - Head 탈출 경로에 자신의 Body가 없는지
        /// </summary>
        private bool ValidateNoSelfReference(List<Vector2Int> cells, ArrowDirection headDir)
        {
            if (cells.Count < 2) return true;

            Vector2Int head = cells[cells.Count - 1];
            Vector2Int dir = GetDirectionVector(headDir);
            Vector2Int pos = head + dir;

            // Body 셀 집합 (Head 제외)
            var bodyCells = new HashSet<Vector2Int>();
            for (int i = 0; i < cells.Count - 1; i++)
            {
                bodyCells.Add(cells[i]);
            }

            // 핵심 수정: 경계까지 전체 Ray 검사 (중간에 다른 화살표가 있어도 계속 검사)
            // 이전 버그: _occupied.Contains(pos)면 break → Body가 그 뒤에 있으면 감지 실패
            while (IsInBounds(pos))
            {
                if (bodyCells.Contains(pos))
                    return false;  // 자기 Body가 탈출 Ray 위에 있음 → Self-reference!

                pos += dir;
            }

            return true;
        }

        /// <summary>
        /// 특정 방향으로 탈출 가능한지 검증 (강화된 버전)
        ///
        /// 핵심 규칙: Head에서 ValidTarget까지 "빈 셀 없이" 직접 연결되어야 함
        /// - 첫 번째 셀이 ValidTarget이면 → 탈출 가능 (경계 또는 기존 화살표 Body)
        /// - 첫 번째 셀이 빈 셀이면 → 탈출 불가 (나중에 화살표가 생기면 막힘)
        /// - 첫 번째 셀이 기존 화살표면 → 탈출 가능 (의존성)
        /// </summary>
        private bool CanEscapeInDirection(Vector2Int head, ArrowDirection dir)
        {
            Vector2Int dirVec = GetDirectionVector(dir);
            Vector2Int firstCell = head + dirVec;

            // 첫 번째 셀이 그리드 외부 (경계) → 직접 탈출 가능
            if (!IsInBounds(firstCell))
                return true;

            // 첫 번째 셀이 ValidTarget (경계 외부 또는 기존 화살표 Body) → 탈출 가능
            if (_validTargets.Contains(firstCell))
                return true;

            // 첫 번째 셀이 기존 화살표에 점유됨 → 의존성으로 탈출 가능
            if (_occupied.Contains(firstCell))
                return true;

            // 첫 번째 셀이 빈 셀 → 나중에 화살표가 생기면 막힐 수 있음
            // 이 경우 탈출 불가로 ���정 (안전한 선택)
            return false;
        }

        // ========== 유틸리티 ==========

        private bool HasEmptyCells()
        {
            return CountEmptyCells() > 0;
        }

        private int CountEmptyCells()
        {
            int count = 0;
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (IsValidCell(cell) && !_occupied.Contains(cell))
                        count++;
                }
            }
            return count;
        }

        private int CountTotalActiveCells()
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

        private Vector2Int? FindAnyEmptyCell()
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (IsValidCell(cell) && !_occupied.Contains(cell))
                        return cell;
                }
            }
            return null;
        }

        private int CountInternalHeads()
        {
            int count = 0;
            foreach (var arrow in _arrows)
            {
                Vector2Int head = arrow.cells[arrow.cells.Count - 1];

                // 경계에 있지 않으면 내부 Head
                if (head.x > 0 && head.x < _gridWidth - 1 &&
                    head.y > 0 && head.y < _gridHeight - 1)
                {
                    count++;
                }
            }
            return count;
        }

        private void MarkOccupied(List<Vector2Int> cells)
        {
            foreach (var cell in cells)
            {
                _occupied.Add(cell);
            }
        }

        private bool IsValidCell(Vector2Int cell)
        {
            if (!IsInBounds(cell)) return false;
            if (_useShapeMask && _shapeMask != null && !_shapeMask[cell.x, cell.y])
                return false;
            return true;
        }

        private bool IsInBounds(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < _gridWidth && pos.y >= 0 && pos.y < _gridHeight;
        }

        private bool IsCellActive(int x, int y)
        {
            if (!_useShapeMask || _shapeMask == null)
                return true;
            return _shapeMask[x, y];
        }

        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private ArrowDirection[] GetAllDirections()
        {
            return new[] { ArrowDirection.Up, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Right };
        }

        private List<ArrowDirection> GetShuffledDirections()
        {
            var dirs = new List<ArrowDirection>
            {
                ArrowDirection.Up, ArrowDirection.Down,
                ArrowDirection.Left, ArrowDirection.Right
            };

            for (int i = dirs.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (dirs[i], dirs[j]) = (dirs[j], dirs[i]);
            }

            return dirs;
        }

        private Vector2Int GetDirectionVector(ArrowDirection dir)
        {
            return dir switch
            {
                ArrowDirection.Up => Vector2Int.up,
                ArrowDirection.Down => Vector2Int.down,
                ArrowDirection.Left => Vector2Int.left,
                ArrowDirection.Right => Vector2Int.right,
                _ => Vector2Int.zero
            };
        }

        private ArrowDirection GetOppositeDirection(ArrowDirection dir)
        {
            return dir switch
            {
                ArrowDirection.Up => ArrowDirection.Down,
                ArrowDirection.Down => ArrowDirection.Up,
                ArrowDirection.Left => ArrowDirection.Right,
                ArrowDirection.Right => ArrowDirection.Left,
                _ => dir
            };
        }

        private ArrowDirection GetDirectionFromTo(Vector2Int from, Vector2Int to)
        {
            Vector2Int diff = to - from;
            if (diff.y > 0) return ArrowDirection.Up;
            if (diff.y < 0) return ArrowDirection.Down;
            if (diff.x > 0) return ArrowDirection.Right;
            if (diff.x < 0) return ArrowDirection.Left;
            return ArrowDirection.Up;
        }

        /// <summary>
        /// 최후의 수단: 1칸짜리 화살표 생성
        /// 코너 셀 등 다른 방법으로 채울 수 없는 셀에 사용
        ///
        /// 조건:
        /// 1. 이 셀에서 적어도 한 방향으로 탈출 가능해야 함 (경계 또는 기존 화살표)
        /// 2. Self-reference 불가 (1칸이므로 자연스럽게 충족)
        /// </summary>
        private EditorArrow TryCreateSingleCellArrow(Vector2Int cell, int arrowId)
        {
            // 가능한 탈출 방향 찾기 (우선순위: 경계 > 기존 화살표 Body)
            var validDirections = new List<(ArrowDirection dir, int priority)>();

            foreach (var dir in GetAllDirections())
            {
                Vector2Int dirVec = GetDirectionVector(dir);
                Vector2Int firstCell = cell + dirVec;

                // 경계 밖 → 직접 탈출 가능 (최우선)
                if (!IsInBounds(firstCell))
                {
                    validDirections.Add((dir, 0));
                    continue;
                }

                // 기존 화살표의 Body에 인접 → 의존성으로 탈출 가능
                // Body인지 확인 (Head 제외)
                bool isValidTargetBody = false;
                foreach (var arrow in _arrows)
                {
                    // Body 셀인지 확인 (Head 제외)
                    for (int i = 0; i < arrow.cells.Count - 1; i++)
                    {
                        if (arrow.cells[i] == firstCell)
                        {
                            isValidTargetBody = true;
                            break;
                        }
                    }
                    if (isValidTargetBody) break;
                }

                if (isValidTargetBody)
                {
                    validDirections.Add((dir, 1));
                }
            }

            if (validDirections.Count == 0)
            {
                Debug.LogWarning($"[ReverseGrowthGenerator] Cannot create single-cell arrow at {cell}: no valid escape direction");
                return null;
            }

            // 우선순위 정렬 후 사이클 검사하면서 선택
            validDirections.Sort((a, b) => a.priority.CompareTo(b.priority));

            foreach (var (dir, priority) in validDirections)
            {
                var tempArrow = new EditorArrow
                {
                    id = arrowId,
                    cells = new List<Vector2Int> { cell },
                    headDirection = dir,
                    color = GameColor.Red
                };

                // Phase 2 핵심: 사이클 방지 검사
                if (!WouldCreateCycle(tempArrow))
                {
                    return tempArrow;
                }
            }

            // 모든 방향이 사이클을 만들면 null 반환
            Debug.LogWarning($"[ReverseGrowthGenerator] Cannot create single-cell arrow at {cell}: all directions create cycles");
            return null;
        }
    }
}