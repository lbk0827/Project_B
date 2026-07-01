using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Data;

namespace Game.Editor.LevelEditor
{
    /// <summary>
    /// 패턴 스타일
    /// </summary>
    public enum PatternStyle
    {
        Dense,       // 밀집형: 짧은 화살표, 빽빽함
        Structured,  // 정돈형: 긴 직선, 규칙적 꺾임
        Mixed        // 혼합형: 밀집 + 정돈 혼합
    }

    /// <summary>
    /// 화살표 패턴 종류
    /// </summary>
    public enum ArrowPattern
    {
        Straight,    // 직선: →→→→→
        LShape,      // ㄱ/ㄴ: →→↓↓
        UShape,      // ㄷ: →→↓↓←←
        Box,         // ㅁ 3면: →↓←↑
        Spiral       // 나선형: 4+ 꺾임
    }

    /// <summary>
    /// Maze-based Dependency Chain Generation 알고리즘
    ///
    /// 5단계 파이프라인:
    /// Phase 1: 미로 경로 생성 (100% Fill Rate)
    /// Phase 2: 경로를 화살표로 분할 (정돈된 패턴)
    /// Phase 3: Head 방향 결정 (의존성 생성)
    /// Phase 4: 의존성 그래프 구축 및 검증
    /// Phase 5: 위상 정렬로 해답 순서 계산
    ///
    /// 핵심 버그 방지 규칙:
    /// - HEAD 정렬: cells[^2] → cells[^1] = headDirection
    /// - 자기 참조 금지: Head 탈출 경로에 자신의 Body 없음
    /// </summary>
    public class MazePuzzleGenerator
    {
        // ========== 컨텍스트 ==========
        private int _gridWidth;
        private int _gridHeight;
        private bool[,] _shapeMask;
        private bool _useShapeMask;
        private GameColor[,] _colorMap;
        private bool _useColorMapping;

        // ========== 파라미터 ==========
        private int _minArrowLength = 3;  // 분할 전략 호환성 (최소 3셀 필요)
        private int _maxArrowLength = 6;
        private int _difficulty = 5;  // 1~10
        private PatternStyle _patternStyle = PatternStyle.Structured;
        private int _colorCount = 4;
        private List<GameColor> _availableColors;

        // ========== 의존성 ==========
        private MazeGenerator _mazeGenerator;
        private DependencyGraph _dependencyGraph;
        private System.Random _random;

        // ========== 통계 ==========
        public int TotalCells { get; private set; }
        public int FilledCells { get; private set; }
        public float FillRate => TotalCells > 0 ? (float)FilledCells / TotalCells * 100f : 0f;
        public int ArrowCount { get; private set; }
        public bool HasCycle { get; private set; }

        // ========== 초기화 ==========

        public MazePuzzleGenerator(int seed = -1)
        {
            _random = seed >= 0 ? new System.Random(seed) : new System.Random();
            _mazeGenerator = new MazeGenerator(seed);
        }

        /// <summary>
        /// 컨텍스트 설정
        /// </summary>
        public void SetContext(
            int gridWidth, int gridHeight,
            bool[,] shapeMask, bool useShapeMask,
            GameColor[,] colorMap, bool useColorMapping)
        {
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            _shapeMask = shapeMask;
            _useShapeMask = useShapeMask;
            _colorMap = colorMap;
            _useColorMapping = useColorMapping;

            _mazeGenerator.SetContext(gridWidth, gridHeight, shapeMask, useShapeMask);
            _dependencyGraph = new DependencyGraph(gridWidth, gridHeight);
        }

        /// <summary>
        /// 파라미터 설정
        /// </summary>
        public void SetParameters(
            int minArrowLength, int maxArrowLength,
            int difficulty, PatternStyle patternStyle,
            List<GameColor> availableColors)
        {
            _minArrowLength = Mathf.Max(3, minArrowLength);  // 최소 3셀 강제 (분할 전략 호환성)
            _maxArrowLength = Mathf.Max(_minArrowLength + 2, maxArrowLength);  // 최소 min+2 보장
            _difficulty = Mathf.Clamp(difficulty, 1, 10);
            _patternStyle = patternStyle;
            _availableColors = availableColors ?? new List<GameColor> { GameColor.Red, GameColor.Blue, GameColor.Green, GameColor.Yellow };
            _colorCount = _availableColors.Count;
        }

        // ========== 메인 생성 함수 ==========

        /// <summary>
        /// 레벨 생성 - 5단계 파이프라인 실행
        /// 의존성 비율 자동 조정: Phase 4 실패 시 비율을 낮추고 재시도
        /// </summary>
        public List<EditorArrow> Generate()
        {
            const int MAX_ATTEMPTS = 10;
            const int MAX_RATIO_REDUCTIONS = 3; // 의존성 비율 감소 최대 횟수

            float originalDifficulty = _difficulty;
            int ratioReductionCount = 0;

            for (int attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
            {
                Debug.Log($"[MazePuzzleGenerator] Attempt {attempt + 1}/{MAX_ATTEMPTS} (difficulty: {_difficulty})");

                // ═══════════════════════════════════════════════════════
                // PHASE 1: 미로 경로 생성
                // ═══════════════════════════════════════════════════════
                var path = _mazeGenerator.GeneratePath();
                if (path == null || path.Count == 0)
                {
                    Debug.LogWarning("[MazePuzzleGenerator] Phase 1 failed: No path generated");
                    continue;
                }

                TotalCells = CountActiveCells();
                FilledCells = path.Count;
                Debug.Log($"[MazePuzzleGenerator] Phase 1: Generated path with {path.Count} cells ({FillRate:F1}% fill rate)");

                // ═══════════════════════════════════════════════════════
                // PHASE 2: 경로를 화살표로 분할
                // ═══════════════════════════════════════════════════════
                var arrows = SplitPathIntoArrows(path);
                if (arrows == null || arrows.Count == 0)
                {
                    Debug.LogWarning("[MazePuzzleGenerator] Phase 2 failed: No arrows created");
                    continue;
                }

                Debug.Log($"[MazePuzzleGenerator] Phase 2: Created {arrows.Count} arrows (before merge)");

                // ═══════════════════════════════════════════════════════
                // PHASE 2.5: 짧은 화살표 병합 (분할 전략 호환성)
                // ═══════════════════════════════════════════════════════
                int shortCount = 0;
                foreach (var a in arrows)
                {
                    if (a.cells.Count < _minArrowLength) shortCount++;
                }

                if (shortCount > 0)
                {
                    Debug.Log($"[MazePuzzleGenerator] Phase 2.5: {shortCount} short arrows detected, merging...");
                    arrows = MergeShortArrows(arrows);
                }

                ArrowCount = arrows.Count;

                // 병합 후 통계
                int finalShortCount = 0;
                foreach (var a in arrows)
                {
                    if (a.cells.Count < _minArrowLength) finalShortCount++;
                }
                if (finalShortCount > 0)
                {
                    Debug.LogWarning($"[MazePuzzleGenerator] {finalShortCount} arrows still below minimum length after merging");
                }

                Debug.Log($"[MazePuzzleGenerator] Phase 2 complete: {arrows.Count} arrows");

                // ═══════════════════════════════════════════════════════
                // PHASE 2.75: 경계 접근성 사전 보장 (사이클 방지)
                // ═══════════════════════════════════════════════════════
                // 셀 → 화살표 ID 매핑 생성 (Phase 2.75에서 필요)
                var cellToArrowIdPreCheck = new Dictionary<Vector2Int, int>();
                foreach (var arrow in arrows)
                {
                    foreach (var cell in arrow.cells)
                    {
                        cellToArrowIdPreCheck[cell] = arrow.id;
                    }
                }

                int arrowsBeforePhase275 = arrows.Count;
                arrows = EnsureBoundaryAccessibility(arrows, cellToArrowIdPreCheck);
                ArrowCount = arrows.Count;

                if (arrows.Count != arrowsBeforePhase275)
                {
                    Debug.Log($"[MazePuzzleGenerator] Phase 2.75: {arrowsBeforePhase275} → {arrows.Count} arrows after accessibility check");
                }
                else
                {
                    Debug.Log($"[MazePuzzleGenerator] Phase 2.75: All {arrows.Count} arrows already have boundary access");
                }

                // ═══════════════════════════════════════════════════════
                // PHASE 3: Head 방향 결정
                // ═══════════════════════════════════════════════════════
                bool headSuccess = DetermineHeadDirections(arrows);
                if (!headSuccess)
                {
                    Debug.LogWarning("[MazePuzzleGenerator] Phase 3 failed: Could not determine head directions");
                    continue;
                }

                Debug.Log("[MazePuzzleGenerator] Phase 3: Head directions determined");

                // ═══════════════════════════════════════════════════════
                // PHASE 4: 의존성 그래프 구축 및 검증
                // ═══════════════════════════════════════════════════════
                _dependencyGraph.Build(arrows);
                HasCycle = _dependencyGraph.HasCycle();

                if (HasCycle)
                {
                    Debug.Log("[MazePuzzleGenerator] Phase 4: Cycle detected, attempting to resolve...");
                    bool resolved = _dependencyGraph.TryResolveCycle(arrows);
                    if (!resolved)
                    {
                        Debug.LogWarning("[MazePuzzleGenerator] Phase 4 failed: Could not resolve cycle");

                        // 의존성 비율 자동 조정
                        if (ratioReductionCount < MAX_RATIO_REDUCTIONS)
                        {
                            ratioReductionCount++;
                            // 난이도를 2단계씩 낮춤 (의존성 비율 감소)
                            _difficulty = Mathf.Max(1, _difficulty - 2);
                            Debug.Log($"[MazePuzzleGenerator] Reducing dependency ratio: difficulty {originalDifficulty} → {_difficulty}");
                        }
                        continue;
                    }
                    HasCycle = false;
                }

                Debug.Log("[MazePuzzleGenerator] Phase 4: Dependency graph validated (no cycles)");

                // ═══════════════════════════════════════════════════════
                // PHASE 5: 최종 검증 (강화됨)
                // ═══════════════════════════════════════════════════════

                // 5-1: Arrow 무결성 검증
                var (arrowValid, arrowErrors) = ArrowValidator.ValidateAll(arrows, _gridWidth, _gridHeight);
                if (!arrowValid)
                {
                    Debug.LogWarning($"[MazePuzzleGenerator] Phase 5-1 Arrow validation failed:");
                    foreach (var error in arrowErrors)
                    {
                        Debug.LogWarning($"  - {error}");
                    }
                    continue;
                }

                var solutionOrder = _dependencyGraph.TopologicalSort();

                // 5-2: Fill Rate 검증
                var (fillValid, fillRate, totalCells, activeCells) = SolvabilityValidator.ValidateFillRate(
                    arrows, _gridWidth, _gridHeight, _shapeMask, _useShapeMask);
                if (!fillValid)
                {
                    Debug.LogWarning($"[MazePuzzleGenerator] Phase 5-2 Fill rate validation failed: {fillRate:F1}% ({totalCells}/{activeCells} cells)");
                    // Fill Rate가 약간 부족해도 경고만 하고 계속 진행
                    if (fillRate < 95f)
                    {
                        Debug.LogError($"[MazePuzzleGenerator] Fill rate too low ({fillRate:F1}%), retrying...");
                        continue;
                    }
                }

                // 5-3: Solvability 시뮬레이션 (실제로 풀 수 있는지 검증)
                var (solvable, solveError) = SolvabilityValidator.Validate(
                    arrows, solutionOrder, _gridWidth, _gridHeight);
                if (!solvable)
                {
                    Debug.LogWarning($"[MazePuzzleGenerator] Phase 5-3 Solvability simulation failed: {solveError}");
                    continue;
                }

                // 통계 업데이트
                FilledCells = totalCells;
                TotalCells = activeCells;

                Debug.Log($"[MazePuzzleGenerator] Phase 5: Complete!");
                Debug.Log($"  - Fill rate: {fillRate:F1}% ({totalCells}/{activeCells} cells)");
                Debug.Log($"  - Solution order: [{string.Join(", ", solutionOrder)}]");
                Debug.Log($"  - Solvability: VERIFIED");

                // 원래 난이도로 복원 (다음 생성을 위해)
                _difficulty = (int)originalDifficulty;

                return arrows;
            }

            // 원래 난이도로 복원
            _difficulty = (int)originalDifficulty;

            Debug.LogError("[MazePuzzleGenerator] Failed to generate valid puzzle after max attempts");
            return new List<EditorArrow>();
        }

        // ========== PHASE 2: 경로 분할 ==========

        /// <summary>
        /// 경로를 화살표로 분할
        /// </summary>
        private List<EditorArrow> SplitPathIntoArrows(List<Vector2Int> path)
        {
            var arrows = new List<EditorArrow>();
            int arrowId = 1;
            int pathIndex = 0;

            while (pathIndex < path.Count)
            {
                // 현재 위치에서 화살표 생성
                var arrowCells = ExtractArrowCells(path, pathIndex);

                if (arrowCells.Count >= 2)
                {
                    var arrow = CreateArrowFromCells(arrowCells, arrowId);
                    if (arrow != null)
                    {
                        arrows.Add(arrow);
                        arrowId++;
                    }
                }

                pathIndex += arrowCells.Count;
            }

            return arrows;
        }

        /// <summary>
        /// 짧은 화살표들을 인접한 화살표와 병합
        /// Split strategy 호환성을 위해 최소 길이 보장
        /// </summary>
        private List<EditorArrow> MergeShortArrows(List<EditorArrow> arrows)
        {
            if (arrows.Count <= 1) return arrows;

            var result = new List<EditorArrow>();
            int i = 0;
            int newId = 1;
            int mergedCount = 0;

            while (i < arrows.Count)
            {
                var current = arrows[i];

                // 충분히 길면 그대로
                if (current.cells.Count >= _minArrowLength)
                {
                    current.id = newId++;
                    result.Add(current);
                    i++;
                    continue;
                }

                // 짧은 화살표 → 다음과 병합 시도
                if (i + 1 < arrows.Count)
                {
                    var next = arrows[i + 1];
                    Vector2Int lastOfCurrent = current.cells[current.cells.Count - 1];
                    Vector2Int firstOfNext = next.cells[0];
                    int dist = Mathf.Abs(firstOfNext.x - lastOfCurrent.x) +
                               Mathf.Abs(firstOfNext.y - lastOfCurrent.y);

                    if (dist == 1)  // 연속적
                    {
                        var mergedCells = new List<Vector2Int>(current.cells);
                        mergedCells.AddRange(next.cells);

                        var merged = new EditorArrow
                        {
                            id = newId++,
                            cells = mergedCells,
                            headDirection = next.headDirection,
                            color = current.color
                        };

                        result.Add(merged);
                        i += 2;
                        mergedCount++;
                        continue;
                    }
                }

                // 병합 불가 → 그대로
                current.id = newId++;
                result.Add(current);
                i++;
            }

            if (mergedCount > 0)
            {
                Debug.Log($"[MazePuzzleGenerator] Merged {mergedCount} short arrow pairs: {arrows.Count} → {result.Count} arrows");
            }

            return result;
        }

        /// <summary>
        /// 모든 화살표가 최소 한 끝점에서 경계로 탈출 가능하도록 보장
        /// 불가능한 화살표는 경계 셀에서 분할
        /// Phase 2.75에서 사이클 방지를 위해 사용
        /// </summary>
        private List<EditorArrow> EnsureBoundaryAccessibility(
            List<EditorArrow> arrows,
            Dictionary<Vector2Int, int> cellToArrowId)
        {
            var result = new List<EditorArrow>();
            int nextId = arrows.Count > 0 ? arrows.Max(a => a.id) + 1 : 1;
            int splitCount = 0;
            int alreadyAccessible = 0;
            int noSplitPossible = 0;

            foreach (var arrow in arrows)
            {
                // 양 끝점 중 하나라도 경계 탈출 가능한지 확인
                bool canEscape = CanEscapeFromEitherEndpoint(arrow, cellToArrowId);

                if (canEscape)
                {
                    result.Add(arrow);
                    alreadyAccessible++;
                    continue;
                }

                // 탈출 불가 → 경계 셀에서 분할 시도
                var splitResult = FindMiddleBoundaryCellWithEscape(arrow, cellToArrowId);
                if (splitResult.HasValue)
                {
                    var (arrowA, arrowB) = SplitArrowAtIndex(
                        arrow, splitResult.Value.index, nextId++, splitResult.Value.escapeDir);

                    // v5.2: 분할 성공 시에만 분할 결과 사용, 실패 시 원본 유지
                    bool splitSuccess = false;

                    if (arrowA != null && arrowA.cells.Count >= 2)
                    {
                        result.Add(arrowA);
                        // cellToArrowId 업데이트
                        foreach (var cell in arrowA.cells)
                        {
                            cellToArrowId[cell] = arrowA.id;
                        }
                        splitSuccess = true;
                    }
                    if (arrowB != null && arrowB.cells.Count >= 2)
                    {
                        result.Add(arrowB);
                        // cellToArrowId 업데이트
                        foreach (var cell in arrowB.cells)
                        {
                            cellToArrowId[cell] = arrowB.id;
                        }
                        splitSuccess = true;
                    }

                    if (splitSuccess)
                    {
                        splitCount++;
                    }
                    else
                    {
                        // 분할 실패 (HEAD 정렬 불가 등) → 원본 유지
                        result.Add(arrow);
                        noSplitPossible++;
                        Debug.LogWarning($"[Phase 2.75] Arrow {arrow.id} split failed, keeping original");
                    }
                }
                else
                {
                    // 분할 불가 → 그대로 유지 (Phase 3에서 ForceAnyBoundaryEscape 시도)
                    result.Add(arrow);
                    noSplitPossible++;
                }
            }

            if (splitCount > 0 || noSplitPossible > 0)
            {
                Debug.Log($"[MazePuzzleGenerator] Phase 2.75 stats: accessible={alreadyAccessible}, split={splitCount}, noSplitPossible={noSplitPossible}");
            }

            return result;
        }

        /// <summary>
        /// 경로에서 화살표 셀 추출 (패턴 스타일에 따라)
        /// </summary>
        private List<Vector2Int> ExtractArrowCells(List<Vector2Int> path, int startIndex)
        {
            var cells = new List<Vector2Int>();
            int maxLength = CalculateArrowLength();

            for (int i = startIndex; i < path.Count && cells.Count < maxLength; i++)
            {
                // 연속성 확인
                if (cells.Count > 0)
                {
                    Vector2Int diff = path[i] - cells[cells.Count - 1];
                    int dist = Mathf.Abs(diff.x) + Mathf.Abs(diff.y);

                    // 인접하지 않으면 중단 (분리된 영역)
                    if (dist != 1)
                        break;
                }

                cells.Add(path[i]);

                // 패턴 완성 체크 (Structured 모드)
                // 최소 4셀 이상에서만 패턴 완성 체크 (분할 전략 호환성)
                int minPatternLength = Mathf.Max(_minArrowLength + 1, 4);
                if (_patternStyle == PatternStyle.Structured && cells.Count >= minPatternLength)
                {
                    var pattern = DetectPattern(cells);
                    if (pattern == ArrowPattern.UShape || pattern == ArrowPattern.Box)
                    {
                        break; // 정돈된 패턴 완성 시 분할
                    }
                }
            }

            // 최소 길이 미달 시 남은 셀 모두 포함
            if (cells.Count < _minArrowLength && startIndex + cells.Count < path.Count)
            {
                int remaining = _minArrowLength - cells.Count;
                for (int i = 0; i < remaining && startIndex + cells.Count < path.Count; i++)
                {
                    int idx = startIndex + cells.Count;
                    Vector2Int diff = path[idx] - cells[cells.Count - 1];
                    if (Mathf.Abs(diff.x) + Mathf.Abs(diff.y) == 1)
                    {
                        cells.Add(path[idx]);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return cells;
        }

        /// <summary>
        /// 화살표 길이 계산 (난이도 기반)
        /// </summary>
        private int CalculateArrowLength()
        {
            // 난이도가 높을수록 긴 화살표
            float t = (_difficulty - 1) / 9f;
            int length = Mathf.RoundToInt(Mathf.Lerp(_minArrowLength, _maxArrowLength, t));

            // 약간의 랜덤성 추가 (최소 3셀 강제 보장)
            int variation = _random.Next(-1, 2);
            int result = length + variation;

            // 분할 전략 호환성: 최소 3셀 강제
            return Mathf.Max(3, Mathf.Clamp(result, _minArrowLength, _maxArrowLength));
        }

        /// <summary>
        /// 패턴 감지
        /// </summary>
        private ArrowPattern DetectPattern(List<Vector2Int> cells)
        {
            if (cells.Count < 2)
                return ArrowPattern.Straight;

            int directionChanges = 0;
            ArrowDirection? prevDir = null;

            for (int i = 1; i < cells.Count; i++)
            {
                ArrowDirection dir = GridUtility.GetDirectionFromTo(cells[i - 1], cells[i]);
                if (prevDir.HasValue && dir != prevDir.Value)
                {
                    directionChanges++;
                }
                prevDir = dir;
            }

            return directionChanges switch
            {
                0 => ArrowPattern.Straight,
                1 => ArrowPattern.LShape,
                2 => ArrowPattern.UShape,
                3 => ArrowPattern.Box,
                _ => ArrowPattern.Spiral
            };
        }

        /// <summary>
        /// 셀 리스트에서 화살표 생성
        /// </summary>
        private EditorArrow CreateArrowFromCells(List<Vector2Int> cells, int id)
        {
            if (cells.Count < 2)
                return null;

            // 색상 결정
            GameColor color = DetermineColor(cells);

            // 초기 Head 방향 (마지막 이동 방향)
            ArrowDirection headDir = GridUtility.GetDirectionFromTo(cells[cells.Count - 2], cells[cells.Count - 1]);

            return new EditorArrow
            {
                id = id,
                cells = new List<Vector2Int>(cells),
                headDirection = headDir,
                color = color
            };
        }

        /// <summary>
        /// 색상 결정
        /// </summary>
        private GameColor DetermineColor(List<Vector2Int> cells)
        {
            // Color Mapping 사용 시 셀의 색상 사용
            if (_useColorMapping && _colorMap != null)
            {
                var colorCounts = new Dictionary<GameColor, int>();
                foreach (var cell in cells)
                {
                    if (cell.x >= 0 && cell.x < _colorMap.GetLength(0) &&
                        cell.y >= 0 && cell.y < _colorMap.GetLength(1))
                    {
                        var c = _colorMap[cell.x, cell.y];
                        if (!colorCounts.ContainsKey(c)) colorCounts[c] = 0;
                        colorCounts[c]++;
                    }
                }

                if (colorCounts.Count > 0)
                {
                    GameColor dominant = GameColor.Red;
                    int maxCount = 0;
                    foreach (var kvp in colorCounts)
                    {
                        if (kvp.Value > maxCount)
                        {
                            maxCount = kvp.Value;
                            dominant = kvp.Key;
                        }
                    }
                    return dominant;
                }
            }

            // 랜덤 색상
            if (_availableColors != null && _availableColors.Count > 0)
            {
                return _availableColors[_random.Next(_availableColors.Count)];
            }

            return GameColor.Red;
        }

        // ========== PHASE 3: Head 방향 결정 ==========

        /// <summary>
        /// 모든 화살표의 Head 방향 결정 (Boundary-Layered Direction Assignment)
        ///
        /// ★ 핵심 전략: "레이어 기반 방향 할당" + "실패 시 화살표 분할"
        /// - Layer 0: 경계에 인접한 화살표 → 경계로 직접 탈출
        /// - Layer N: Layer N-1 화살표에 의존
        /// - 실패한 화살표: 경계 셀에서 분할하여 재시도
        ///
        /// 수학적 보장:
        /// - Layer N 화살표는 Layer N-1 이하에만 의존
        /// - 역방향 의존 없음 → 사이클 불가능 (DAG 보장)
        /// </summary>
        private bool DetermineHeadDirections(List<EditorArrow> arrows)
        {
            Debug.Log($"[MazePuzzleGenerator] Phase 3: Boundary-Layered Direction Assignment (v3 - with split fix)");

            // 셀 → 화살표 ID 매핑
            var cellToArrowId = new Dictionary<Vector2Int, int>();
            foreach (var arrow in arrows)
            {
                foreach (var cell in arrow.cells)
                {
                    cellToArrowId[cell] = arrow.id;
                }
            }

            // ═══════════════════════════════════════════════════════════════════
            // STEP 1: 1차 방향 할당 시도
            // ═══════════════════════════════════════════════════════════════════
            var failedArrowIds = new List<int>();
            AssignDirectionsWithLayers(arrows, cellToArrowId, failedArrowIds);
            Debug.Log($"[MazePuzzleGenerator] STEP 1 complete: {failedArrowIds.Count} failed arrows");

            // ═══════════════════════════════════════════════════════════════════
            // STEP 2: 실패한 화살표 분할 및 재시도
            // ═══════════════════════════════════════════════════════════════════
            if (failedArrowIds.Count > 0)
            {
                Debug.Log($"[MazePuzzleGenerator] STEP 2: Attempting to split {failedArrowIds.Count} failed arrows...");

                // 실패한 화살표들을 경계 셀에서 분할
                var newArrows = SplitFailedArrowsAtBoundaryCells(arrows, failedArrowIds, cellToArrowId);

                // 분할이 발생했으면 전체 재시도
                if (newArrows.Count != arrows.Count)
                {
                    // arrows 리스트 갱신 (원본 리스트 수정)
                    arrows.Clear();
                    arrows.AddRange(newArrows);

                    // cellToArrowId 재구성
                    cellToArrowId.Clear();
                    foreach (var arrow in arrows)
                    {
                        foreach (var cell in arrow.cells)
                        {
                            cellToArrowId[cell] = arrow.id;
                        }
                    }

                    // 2차 방향 할당 시도
                    failedArrowIds.Clear();
                    AssignDirectionsWithLayers(arrows, cellToArrowId, failedArrowIds);

                    Debug.Log($"[MazePuzzleGenerator] After split: {arrows.Count} arrows, {failedArrowIds.Count} still failed");
                }
            }

            // ═══════════════════════════════════════════════════════════════════
            // STEP 3: 결과 요약
            // ═══════════════════════════════════════════════════════════════════
            int successCount = arrows.Count - failedArrowIds.Count;
            Debug.Log($"[MazePuzzleGenerator] Phase 3 complete: {successCount}/{arrows.Count} success");

            // 실패율이 높으면 경고
            if (failedArrowIds.Count > arrows.Count * 0.1f)
            {
                Debug.LogWarning($"[MazePuzzleGenerator] High failure rate: {failedArrowIds.Count}/{arrows.Count} arrows failed direction assignment");
            }

            return true;
        }

        /// <summary>
        /// 레이어 기반 방향 할당 수행
        /// </summary>
        private void AssignDirectionsWithLayers(
            List<EditorArrow> arrows,
            Dictionary<Vector2Int, int> cellToArrowId,
            List<int> failedArrowIds)
        {
            // 경계 레이어 계산
            var arrowLayers = ComputeArrowLayers(arrows, cellToArrowId);

            // 레이어별 화살표 그룹화
            int maxLayer = 0;
            foreach (var kvp in arrowLayers)
            {
                if (kvp.Value > maxLayer) maxLayer = kvp.Value;
            }

            var arrowsByLayer = new Dictionary<int, List<EditorArrow>>();
            for (int i = 0; i <= maxLayer; i++)
            {
                arrowsByLayer[i] = new List<EditorArrow>();
            }
            foreach (var arrow in arrows)
            {
                int layer = arrowLayers.GetValueOrDefault(arrow.id, maxLayer);
                arrowsByLayer[layer].Add(arrow);
            }

            // 레이어 순서대로 HEAD 방향 설정
            int layer0Success = 0;
            int layer0Fail = 0;
            int layerNSuccess = 0;
            int layerNFail = 0;

            for (int layer = 0; layer <= maxLayer; layer++)
            {
                var layerArrows = arrowsByLayer[layer];

                foreach (var arrow in layerArrows)
                {
                    if (layer == 0)
                    {
                        // Layer 0: 경계로 직접 탈출 - 반드시 성공해야 함
                        bool success = SetHeadToExternalDirection(arrow, cellToArrowId);

                        if (!success)
                        {
                            // 마지막 수단: 모든 4방향에서 경계 탈출 가능한 방향 강제 탐색
                            success = ForceAnyBoundaryEscape(arrow, cellToArrowId);
                        }

                        if (success)
                        {
                            layer0Success++;
                        }
                        else
                        {
                            // Layer 0이 탈출 못하면 심각한 문제
                            Debug.LogError($"[MazePuzzleGenerator] Layer 0 Arrow {arrow.id} cannot escape! Critical issue.");
                            layer0Fail++;
                            failedArrowIds.Add(arrow.id);
                        }
                    }
                    else
                    {
                        // Layer N: 반드시 Layer < N에만 의존 (v5 엄격 검증)
                        var targetInfo = FindNearestLowerLayerArrow(arrow, arrowLayers, cellToArrowId);

                        bool success = false;
                        if (targetInfo.HasValue)
                        {
                            int targetLayer = arrowLayers.GetValueOrDefault(targetInfo.Value.arrowId, int.MaxValue);

                            // 엄격한 검증: 타겟 레이어가 현재 레이어보다 낮아야 함
                            if (targetLayer < layer)
                            {
                                success = SetHeadTowardArrow(arrow, targetInfo.Value.arrowId,
                                                             targetInfo.Value.direction, cellToArrowId);
                            }
                        }

                        if (!success)
                        {
                            // Fallback 1: 경계 탈출 시도 (Layer 0처럼 동작)
                            success = SetHeadToExternalDirection(arrow, cellToArrowId);
                        }

                        if (!success)
                        {
                            // Fallback 2: 강제 경계 탈출 시도
                            success = ForceAnyBoundaryEscape(arrow, cellToArrowId);
                        }

                        if (success)
                        {
                            layerNSuccess++;
                        }
                        else
                        {
                            layerNFail++;
                            failedArrowIds.Add(arrow.id);
                        }
                    }
                }
            }

            Debug.Log($"[MazePuzzleGenerator] Direction assignment: Layer 0: {layer0Success}/{layer0Success + layer0Fail}, Layer 1+: {layerNSuccess}/{layerNSuccess + layerNFail}");
        }

        /// <summary>
        /// 화살표의 내부 방향 후보 찾기 (다른 화살표를 막는 방향)
        /// </summary>
        private (List<Vector2Int> cells, ArrowDirection direction)? FindInternalDirection(
            EditorArrow arrow,
            Dictionary<Vector2Int, int> cellToArrowId)
        {
            if (arrow.cells.Count < 2)
                return null;

            // 양쪽 끝점에서 내부 방향 후보 찾기
            var candidates = new List<(List<Vector2Int> cells, ArrowDirection direction, int blockingArrowId)>();

            // 현재 끝점(cells[^1])에서 각 방향 확인
            Vector2Int currentHead = arrow.cells[arrow.cells.Count - 1];
            foreach (var dir in new[] { ArrowDirection.Up, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Right })
            {
                var result = CheckInternalDirection(arrow.cells, currentHead, dir, arrow.id, cellToArrowId);
                if (result.HasValue)
                    candidates.Add(result.Value);
            }

            // 반대쪽 끝점(cells[0])에서 확인 (셀 순서 뒤집기)
            var reversedCells = new List<Vector2Int>(arrow.cells);
            reversedCells.Reverse();
            Vector2Int reversedHead = reversedCells[reversedCells.Count - 1];
            foreach (var dir in new[] { ArrowDirection.Up, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Right })
            {
                var result = CheckInternalDirection(reversedCells, reversedHead, dir, arrow.id, cellToArrowId);
                if (result.HasValue)
                    candidates.Add(result.Value);
            }

            if (candidates.Count == 0)
                return null;

            // 가장 좋은 후보 선택 (막는 화살표가 있는 것)
            return (candidates[0].cells, candidates[0].direction);
        }

        /// <summary>
        /// 특정 방향이 내부 방향인지 확인 (다른 화살표를 막는지)
        /// </summary>
        private (List<Vector2Int> cells, ArrowDirection direction, int blockingArrowId)? CheckInternalDirection(
            List<Vector2Int> cells,
            Vector2Int head,
            ArrowDirection dir,
            int arrowId,
            Dictionary<Vector2Int, int> cellToArrowId)
        {
            // HEAD 정렬 규칙 확인: cells[^2] → cells[^1] = dir
            if (cells.Count >= 2)
            {
                Vector2Int secondLast = cells[cells.Count - 2];
                Vector2Int last = cells[cells.Count - 1];
                ArrowDirection naturalDir = GridUtility.GetDirectionFromTo(secondLast, last);

                if (naturalDir != dir)
                    return null; // HEAD 정렬 규칙 위반
            }

            // 자기 참조 검사
            Vector2Int dirVec = GridUtility.GetDirectionVector(dir);
            Vector2Int current = head + dirVec;

            while (IsInsideGrid(current))
            {
                // 자신의 Body와 겹치면 무효
                for (int i = 0; i < cells.Count - 1; i++)
                {
                    if (cells[i] == current)
                        return null;
                }

                // 다른 화살표가 있으면 내부 방향!
                if (cellToArrowId.TryGetValue(current, out int blockingId) && blockingId != arrowId)
                {
                    return (new List<Vector2Int>(cells), dir, blockingId);
                }

                current += dirVec;
            }

            // 경계까지 도달 = 외부 방향 (내부 아님)
            return null;
        }

        /// <summary>
        /// 화살표를 중앙에서의 거리 순으로 정렬 (멀리 있는 것부터)
        /// </summary>
        private List<EditorArrow> SortArrowsByDistanceFromCenter(List<EditorArrow> arrows)
        {
            Vector2 center = new Vector2(_gridWidth / 2f, _gridHeight / 2f);

            var sorted = new List<EditorArrow>(arrows);
            sorted.Sort((a, b) =>
            {
                float distA = GetAverageDistanceFromCenter(a, center);
                float distB = GetAverageDistanceFromCenter(b, center);
                // 멀리 있는 것이 먼저 (경계에 가까운 것)
                return distB.CompareTo(distA);
            });
            return sorted;
        }

        /// <summary>
        /// 화살표의 중앙에서 평균 거리 계산
        /// </summary>
        private float GetAverageDistanceFromCenter(EditorArrow arrow, Vector2 center)
        {
            float totalDist = 0f;
            foreach (var cell in arrow.cells)
            {
                totalDist += Vector2.Distance(new Vector2(cell.x, cell.y), center);
            }
            return arrow.cells.Count > 0 ? totalDist / arrow.cells.Count : 0f;
        }

        /// <summary>
        /// 화살표를 경계 거리 순으로 정렬 (가장 가까운 것부터)
        /// 경계에 가까운 화살표가 먼저 처리되어 의존성 0으로 설정됨
        /// </summary>
        private List<EditorArrow> SortArrowsByBoundaryDistance(List<EditorArrow> arrows)
        {
            var sorted = new List<EditorArrow>(arrows);
            sorted.Sort((a, b) =>
            {
                int distA = GetMinBoundaryDistance(a);
                int distB = GetMinBoundaryDistance(b);
                return distA.CompareTo(distB);
            });
            return sorted;
        }

        /// <summary>
        /// 화살표의 최소 경계 거리 계산
        /// 화살표의 모든 셀 중 경계에 가장 가까운 거리 반환
        /// </summary>
        private int GetMinBoundaryDistance(EditorArrow arrow)
        {
            int minDist = int.MaxValue;
            foreach (var cell in arrow.cells)
            {
                int distToLeft = cell.x;
                int distToRight = _gridWidth - 1 - cell.x;
                int distToBottom = cell.y;
                int distToTop = _gridHeight - 1 - cell.y;

                int cellMinDist = Mathf.Min(distToLeft, distToRight, distToBottom, distToTop);
                minDist = Mathf.Min(minDist, cellMinDist);
            }
            return minDist;
        }

        /// <summary>
        /// 화살표를 외부 방향(경계로 탈출 가능)으로 설정
        ///
        /// 핵심: 경계에 "닿은" 셀이 아닌, 경계로 "탈출 가능한" 방향 찾기
        ///
        /// ★ 3단계 전략:
        /// 1. 자연 방향(cells[^2]→cells[^1])이 경계로 탈출 가능하면 그대로
        /// 2. 뒤집은 방향이 경계로 탈출 가능하면 셀 순서 뒤집기
        /// 3. 양 끝점에서 모든 4방향 탐색 후 HEAD 정렬 규칙에 맞는 방향 찾기
        /// </summary>
        private bool SetHeadToExternalDirection(EditorArrow arrow, Dictionary<Vector2Int, int> cellToArrowId)
        {
            if (arrow.cells.Count < 2)
                return false;

            // ═══════════════════════════════════════════════════════
            // 방법 1: 현재 자연 방향이 외부(경계)로 탈출 가능한지 확인
            // ═══════════════════════════════════════════════════════
            Vector2Int head = arrow.cells[arrow.cells.Count - 1];
            Vector2Int secondLast = arrow.cells[arrow.cells.Count - 2];
            ArrowDirection naturalDir = GridUtility.GetDirectionFromTo(secondLast, head);

            if (CanEscapeToExternal(head, naturalDir, arrow.id, cellToArrowId) &&
                !HasSelfReferenceInDirection(arrow.cells, head, naturalDir))
            {
                arrow.headDirection = naturalDir;
                return true;
            }

            // ═══════════════════════════════════════════════════════
            // 방법 2: 셀 순서 뒤집기 시도 (반대쪽 끝점의 자연 방향)
            // ═══════════════════════════════════════════════════════
            Vector2Int reversedHead = arrow.cells[0];
            Vector2Int reversedSecondLast = arrow.cells[1];
            ArrowDirection reversedDir = GridUtility.GetDirectionFromTo(reversedSecondLast, reversedHead);

            if (CanEscapeToExternal(reversedHead, reversedDir, arrow.id, cellToArrowId) &&
                !HasSelfReferenceInDirection(arrow.cells, reversedHead, reversedDir))
            {
                arrow.cells.Reverse();
                arrow.headDirection = reversedDir;
                return true;
            }

            // ═══════════════════════════════════════════════════════
            // 방법 3: 경계에 직접 닿은 셀 찾기 및 해당 셀을 HEAD로 설정
            //         화살표 내 경계 셀이 있으면, 그 셀에서 경계 방향으로 탈출
            // ═══════════════════════════════════════════════════════
            var boundaryCellCandidates = FindBoundaryCellsWithEscapeDirection(arrow, cellToArrowId);

            // 각 후보에 대해 셀 순서 재배열 시도
            foreach (var (cellIndex, escapeDir) in boundaryCellCandidates)
            {
                // 해당 셀을 HEAD로 만들기 위해 셀 순서 재배열 시도
                var reorderedCells = TryReorderCellsForBoundaryEscape(arrow.cells, cellIndex, escapeDir);
                if (reorderedCells != null)
                {
                    // 자기 참조 검사
                    Vector2Int newHead = reorderedCells[reorderedCells.Count - 1];
                    if (!HasSelfReferenceInDirection(reorderedCells, newHead, escapeDir))
                    {
                        arrow.cells = reorderedCells;
                        arrow.headDirection = escapeDir;
                        return true;
                    }
                }
            }

            // ═══════════════════════════════════════════════════════
            // 모두 실패 → 방향 설정 없이 실패 반환
            // v5: 자연 방향 폴백 제거 - 호출자가 ForceAnyBoundaryEscape 시도
            // (이전: arrow.headDirection = naturalDir; - 이것이 사이클의 원인이었음)
            // ═══════════════════════════════════════════════════════
            return false;
        }

        /// <summary>
        /// 화살표 내에서 경계에 직접 닿은 셀과 탈출 방향 찾기
        /// </summary>
        private List<(int cellIndex, ArrowDirection escapeDir)> FindBoundaryCellsWithEscapeDirection(
            EditorArrow arrow,
            Dictionary<Vector2Int, int> cellToArrowId)
        {
            var candidates = new List<(int cellIndex, ArrowDirection escapeDir, int distToBoundary)>();

            for (int i = 0; i < arrow.cells.Count; i++)
            {
                Vector2Int cell = arrow.cells[i];

                // 각 경계 방향 확인
                if (cell.x == 0 && CanEscapeToExternal(cell, ArrowDirection.Left, arrow.id, cellToArrowId))
                    candidates.Add((i, ArrowDirection.Left, 0));
                if (cell.x == _gridWidth - 1 && CanEscapeToExternal(cell, ArrowDirection.Right, arrow.id, cellToArrowId))
                    candidates.Add((i, ArrowDirection.Right, 0));
                if (cell.y == 0 && CanEscapeToExternal(cell, ArrowDirection.Down, arrow.id, cellToArrowId))
                    candidates.Add((i, ArrowDirection.Down, 0));
                if (cell.y == _gridHeight - 1 && CanEscapeToExternal(cell, ArrowDirection.Up, arrow.id, cellToArrowId))
                    candidates.Add((i, ArrowDirection.Up, 0));
            }

            // 끝점에 가까운 후보 우선 (셀 순서 재배열이 쉬움)
            candidates.Sort((a, b) =>
            {
                // 끝점(index 0 또는 마지막)에 가까운 것 우선
                int distToEndA = Mathf.Min(a.cellIndex, arrow.cells.Count - 1 - a.cellIndex);
                int distToEndB = Mathf.Min(b.cellIndex, arrow.cells.Count - 1 - b.cellIndex);
                return distToEndA.CompareTo(distToEndB);
            });

            return candidates.ConvertAll(c => (c.cellIndex, c.escapeDir));
        }

        /// <summary>
        /// 특정 셀을 HEAD로 만들기 위해 셀 순서 재배열
        /// HEAD 정렬 규칙: cells[^2] → cells[^1] = headDirection
        /// </summary>
        private List<Vector2Int> TryReorderCellsForBoundaryEscape(
            List<Vector2Int> originalCells,
            int targetCellIndex,
            ArrowDirection targetDirection)
        {
            if (originalCells.Count < 2)
                return null;

            // 케이스 1: 타겟 셀이 마지막 셀 (현재 HEAD 위치)
            if (targetCellIndex == originalCells.Count - 1)
            {
                // HEAD 정렬 규칙 확인
                Vector2Int secondLast = originalCells[originalCells.Count - 2];
                Vector2Int last = originalCells[originalCells.Count - 1];
                ArrowDirection cellDir = GridUtility.GetDirectionFromTo(secondLast, last);

                if (cellDir == targetDirection)
                    return new List<Vector2Int>(originalCells); // 그대로 사용

                return null; // HEAD 정렬 규칙 위반
            }

            // 케이스 2: 타겟 셀이 첫 번째 셀 (뒤집으면 HEAD 위치)
            if (targetCellIndex == 0)
            {
                // 뒤집은 후 HEAD 정렬 규칙 확인
                Vector2Int secondLast = originalCells[1];
                Vector2Int last = originalCells[0];
                ArrowDirection cellDir = GridUtility.GetDirectionFromTo(secondLast, last);

                if (cellDir == targetDirection)
                {
                    var reversed = new List<Vector2Int>(originalCells);
                    reversed.Reverse();
                    return reversed;
                }

                return null; // HEAD 정렬 규칙 위반
            }

            // 케이스 3: 타겟 셀이 중간에 있는 경우
            // 중간 셀을 HEAD로 만들려면 화살표를 분할해야 하는데,
            // 이는 Phase 2의 영역이므로 여기서는 처리하지 않음
            return null;
        }

        /// <summary>
        /// 특정 위치에서 특정 방향으로 경계까지의 거리 계산
        /// </summary>
        private int GetDistanceToBoundary(Vector2Int pos, ArrowDirection dir)
        {
            switch (dir)
            {
                case ArrowDirection.Up: return _gridHeight - 1 - pos.y;
                case ArrowDirection.Down: return pos.y;
                case ArrowDirection.Left: return pos.x;
                case ArrowDirection.Right: return _gridWidth - 1 - pos.x;
                default: return int.MaxValue;
            }
        }

        /// <summary>
        /// HEAD에서 해당 방향으로 다른 화살표를 만나지 않고 경계까지 탈출 가능한지 확인
        /// </summary>
        private bool CanEscapeToExternal(Vector2Int head, ArrowDirection dir, int arrowId, Dictionary<Vector2Int, int> cellToArrowId)
        {
            Vector2Int dirVec = GridUtility.GetDirectionVector(dir);
            Vector2Int current = head + dirVec;

            while (IsInsideGrid(current))
            {
                // 다른 화살표가 막고 있으면 외부 방향 아님
                if (cellToArrowId.TryGetValue(current, out int blockingId) && blockingId != arrowId)
                {
                    return false;
                }
                current += dirVec;
            }

            return true; // 경계까지 도달 가능 = 외부 방향
        }

        /// <summary>
        /// 모든 가능한 방향에서 경계 탈출 시도 (마지막 수단)
        /// 셀 재정렬, 뒤집기 등 모든 방법 시도
        /// Phase 3의 Layer 0 강제 탈출에 사용
        ///
        /// v5.2: 끝점 우선 탐색 - 경계 인접한 끝점부터 확인
        /// </summary>
        private bool ForceAnyBoundaryEscape(
            EditorArrow arrow,
            Dictionary<Vector2Int, int> cellToArrowId)
        {
            // 디버그: 화살표 정보 출력
            var cellsStr = string.Join("→", arrow.cells.ConvertAll(c => $"({c.x},{c.y})"));
            Debug.Log($"[ForceAnyBoundaryEscape] Arrow {arrow.id}: {arrow.cells.Count} cells: {cellsStr}");

            int escapableCellCount = 0;
            int headAlignFailCount = 0;
            int selfRefFailCount = 0;

            // v5.2: 끝점 우선 탐색 (중간 셀은 TryReorderCellsForHead에서 지원하지 않음)
            var endpoints = new List<(Vector2Int cell, int index)>
            {
                (arrow.cells[arrow.cells.Count - 1], arrow.cells.Count - 1),  // 마지막 셀
                (arrow.cells[0], 0)  // 첫 번째 셀
            };

            foreach (var (cell, cellIndex) in endpoints)
            {
                // 경계에 인접한 방향들 먼저 확인
                var boundaryDirs = GetBoundaryAdjacentDirections(cell);

                foreach (var dir in boundaryDirs)
                {
                    if (!CanEscapeToExternal(cell, dir, arrow.id, cellToArrowId))
                        continue;

                    escapableCellCount++;

                    var reordered = TryReorderCellsForHead(arrow.cells, cellIndex, dir);

                    if (reordered == null)
                    {
                        headAlignFailCount++;
                        continue;
                    }

                    if (HasSelfReferenceInDirection(reordered, cell, dir))
                    {
                        selfRefFailCount++;
                        continue;
                    }

                    // 성공!
                    Debug.Log($"[ForceAnyBoundaryEscape] Arrow {arrow.id}: Success! cell=({cell.x},{cell.y}), dir={dir}, newCellCount={reordered.Count}");
                    arrow.cells = reordered;
                    arrow.headDirection = dir;
                    return true;
                }
            }

            // 끝점에서 실패하면 모든 셀/방향 탐색 (fallback)
            foreach (var cell in arrow.cells)
            {
                int cellIndex = arrow.cells.IndexOf(cell);
                // 중간 셀은 TryReorderCellsForHead에서 null 반환하므로 스킵됨
                if (cellIndex != 0 && cellIndex != arrow.cells.Count - 1)
                    continue;

                foreach (var dir in new[] { ArrowDirection.Up, ArrowDirection.Down,
                                            ArrowDirection.Left, ArrowDirection.Right })
                {
                    if (!CanEscapeToExternal(cell, dir, arrow.id, cellToArrowId))
                        continue;

                    escapableCellCount++;

                    var reordered = TryReorderCellsForHead(arrow.cells, cellIndex, dir);

                    if (reordered == null)
                    {
                        headAlignFailCount++;
                        continue;
                    }

                    if (HasSelfReferenceInDirection(reordered, cell, dir))
                    {
                        selfRefFailCount++;
                        continue;
                    }

                    // 성공!
                    Debug.Log($"[ForceAnyBoundaryEscape] Arrow {arrow.id}: Success! cell=({cell.x},{cell.y}), dir={dir}, newCellCount={reordered.Count}");
                    arrow.cells = reordered;
                    arrow.headDirection = dir;
                    return true;
                }
            }

            // 실패 원인 로그
            Debug.LogWarning($"[ForceAnyBoundaryEscape] Arrow {arrow.id} FAILED: " +
                           $"escapableCells={escapableCellCount}, headAlignFail={headAlignFailCount}, selfRefFail={selfRefFailCount}");

            return false;
        }

        /// <summary>
        /// 셀이 인접한 경계 방향들 반환
        /// 예: (0, 5) → [Left], (0, 0) → [Left, Down]
        /// </summary>
        private List<ArrowDirection> GetBoundaryAdjacentDirections(Vector2Int cell)
        {
            var dirs = new List<ArrowDirection>();
            if (cell.x == 0) dirs.Add(ArrowDirection.Left);
            if (cell.x == _gridWidth - 1) dirs.Add(ArrowDirection.Right);
            if (cell.y == 0) dirs.Add(ArrowDirection.Down);
            if (cell.y == _gridHeight - 1) dirs.Add(ArrowDirection.Up);
            return dirs;
        }

        /// <summary>
        /// 특정 셀을 HEAD로 만들기 위해 셀 순서 재정렬
        /// HEAD 정렬 규칙: cells[^2] → cells[^1] = headDirection
        /// 이 규칙은 대각선 방지를 위해 반드시 지켜야 함
        ///
        /// 끝점만 HEAD로 가능 (중간 셀은 Fill Rate 저하 방지를 위해 제외)
        /// </summary>
        private List<Vector2Int> TryReorderCellsForHead(
            List<Vector2Int> cells,
            int targetIndex,
            ArrowDirection targetDir)
        {
            if (cells.Count < 2) return null;

            // 케이스 1: 이미 마지막 셀 - 방향 일치 필수
            if (targetIndex == cells.Count - 1)
            {
                var dir = GridUtility.GetDirectionFromTo(cells[cells.Count - 2], cells[cells.Count - 1]);
                if (dir == targetDir)
                    return new List<Vector2Int>(cells);
                return null;
            }

            // 케이스 2: 첫 셀을 HEAD로 → 뒤집기 후 방향 확인
            if (targetIndex == 0)
            {
                var reversed = new List<Vector2Int>(cells);
                reversed.Reverse();
                var dir = GridUtility.GetDirectionFromTo(reversed[reversed.Count - 2], reversed[reversed.Count - 1]);
                if (dir == targetDir)
                    return reversed;
                return null;
            }

            // 케이스 3: 중간 셀 - Fill Rate 유지를 위해 지원하지 않음
            return null;
        }

        /// <summary>
        /// 양 끝점 중 하나라도 경계로 탈출 가능한지 확인
        /// Phase 2.75에서 사전 검증에 사용
        /// </summary>
        private bool CanEscapeFromEitherEndpoint(
            EditorArrow arrow,
            Dictionary<Vector2Int, int> cellToArrowId)
        {
            if (arrow.cells.Count < 2) return false;

            // 끝점 1 (cells[^1]) - 현재 HEAD 위치
            Vector2Int head1 = arrow.cells[arrow.cells.Count - 1];
            Vector2Int secondLast1 = arrow.cells[arrow.cells.Count - 2];
            ArrowDirection dir1 = GridUtility.GetDirectionFromTo(secondLast1, head1);

            if (CanEscapeToExternal(head1, dir1, arrow.id, cellToArrowId) &&
                !HasSelfReferenceInDirection(arrow.cells, head1, dir1))
                return true;

            // 끝점 2 (cells[0]) - 뒤집은 경우
            Vector2Int head2 = arrow.cells[0];
            Vector2Int secondLast2 = arrow.cells[1];
            ArrowDirection dir2 = GridUtility.GetDirectionFromTo(secondLast2, head2);

            if (CanEscapeToExternal(head2, dir2, arrow.id, cellToArrowId) &&
                !HasSelfReferenceInDirection(arrow.cells, head2, dir2))
                return true;

            return false;
        }

        /// <summary>
        /// 특정 방향으로 자기 참조가 있는지 확인
        /// </summary>
        private bool HasSelfReferenceInDirection(List<Vector2Int> cells, Vector2Int head, ArrowDirection dir)
        {
            Vector2Int dirVec = GridUtility.GetDirectionVector(dir);
            Vector2Int current = head + dirVec;

            while (IsInsideGrid(current))
            {
                // Head 제외한 Body 셀 확인
                for (int i = 0; i < cells.Count - 1; i++)
                {
                    if (cells[i] == current)
                        return true; // 자기 참조!
                }
                current += dirVec;
            }

            return false;
        }

        /// <summary>
        /// 사이클을 방지하면서 Head 방향 결정
        /// </summary>
        private bool DetermineArrowHeadDirectionSafe(
            EditorArrow arrow,
            List<EditorArrow> allArrows,
            List<EditorArrow> processedArrows,
            Dictionary<Vector2Int, int> cellToArrowId,
            float dependencyRatio)
        {
            if (arrow.cells.Count < 2)
                return false;

            // 양 끝점에서 완전히 유효한 Head 후보 찾기
            var candidates = new List<(List<Vector2Int> cells, ArrowDirection dir, bool isInternal, bool wouldCreateCycle)>();

            // 끝점 0 (cells[0]이 Head인 경우) - 셀 순서 뒤집어야 함
            var reversedCells = new List<Vector2Int>(arrow.cells);
            reversedCells.Reverse();
            FindValidHeadCandidatesWithCycleCheck(reversedCells, cellToArrowId, arrow.id, processedArrows, candidates);

            // 끝점 1 (cells[^1]이 Head인 경우) - 현재 순서 유지
            var originalCells = new List<Vector2Int>(arrow.cells);
            FindValidHeadCandidatesWithCycleCheck(originalCells, cellToArrowId, arrow.id, processedArrows, candidates);

            if (candidates.Count == 0)
            {
                Debug.LogWarning($"[MazePuzzleGenerator] No valid head direction for Arrow {arrow.id}");
                return false;
            }

            // 의존성 비율에 따라 내부/경계 방향 선택
            bool preferInternal = _random.NextDouble() < dependencyRatio;

            // 후보 정렬:
            // 1. 사이클을 만들지 않는 것 우선
            // 2. 선호 타입 (내부/경계)
            // 3. 셀 수가 많은 것
            candidates.Sort((a, b) =>
            {
                // 1. 사이클을 만들지 않는 것 우선
                int cycleCompare = a.wouldCreateCycle.CompareTo(b.wouldCreateCycle);
                if (cycleCompare != 0) return cycleCompare;

                // 2. 선호 타입 우선
                int typeCompare = preferInternal
                    ? b.isInternal.CompareTo(a.isInternal)
                    : a.isInternal.CompareTo(b.isInternal);
                if (typeCompare != 0) return typeCompare;

                // 3. 셀 수가 많은 것 우선
                return b.cells.Count.CompareTo(a.cells.Count);
            });

            // 첫 번째 후보 선택
            var selected = candidates[0];

            // 셀 리스트 교체
            arrow.cells = selected.cells;
            arrow.headDirection = selected.dir;

            // cellToArrowId 업데이트 (셀이 변경되었을 수 있음)
            foreach (var cell in arrow.cells)
            {
                cellToArrowId[cell] = arrow.id;
            }

            return true;
        }

        /// <summary>
        /// 사이클 체크를 포함한 유효한 Head 후보 찾기
        /// </summary>
        private void FindValidHeadCandidatesWithCycleCheck(
            List<Vector2Int> cells,
            Dictionary<Vector2Int, int> cellToArrowId,
            int arrowId,
            List<EditorArrow> processedArrows,
            List<(List<Vector2Int> cells, ArrowDirection dir, bool isInternal, bool wouldCreateCycle)> candidates)
        {
            if (cells.Count < 2)
                return;

            // 현재 셀 구성에서 HEAD 정렬 규칙에 따른 방향
            Vector2Int head = cells[cells.Count - 1];
            Vector2Int secondLast = cells[cells.Count - 2];
            ArrowDirection naturalDir = GridUtility.GetDirectionFromTo(secondLast, head);

            // 자연 방향으로 탈출 가능하면 바로 추가
            if (CanEscapeInDirection(head, naturalDir, cells, cellToArrowId, arrowId, out bool isInternal))
            {
                bool wouldCycle = WouldCreateCycle(arrowId, head, naturalDir, cellToArrowId, processedArrows);
                candidates.Add((new List<Vector2Int>(cells), naturalDir, isInternal, wouldCycle));
            }

            // 다른 방향으로 셀 추가 시도
            foreach (ArrowDirection altDir in new[] { ArrowDirection.Up, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Right })
            {
                if (altDir == naturalDir) continue;

                if (!CanEscapeInDirection(head, altDir, cells, cellToArrowId, arrowId, out bool altInternal))
                    continue;

                Vector2Int dirVec = GridUtility.GetDirectionVector(altDir);
                Vector2Int newHead = head + dirVec;

                if (IsInsideGrid(newHead) &&
                    !cells.Contains(newHead) &&
                    !cellToArrowId.ContainsKey(newHead))
                {
                    var newCells = new List<Vector2Int>(cells) { newHead };

                    if (CanEscapeInDirection(newHead, altDir, newCells, cellToArrowId, arrowId, out bool finalInternal))
                    {
                        bool wouldCycle = WouldCreateCycle(arrowId, newHead, altDir, cellToArrowId, processedArrows);
                        candidates.Add((newCells, altDir, finalInternal, wouldCycle));
                    }
                }
            }

            // 끝 셀 제거 후 재시도
            if (cells.Count > 2)
            {
                var trimmedCells = new List<Vector2Int>(cells);
                trimmedCells.RemoveAt(trimmedCells.Count - 1);

                Vector2Int trimmedHead = trimmedCells[trimmedCells.Count - 1];
                Vector2Int trimmedSecondLast = trimmedCells[trimmedCells.Count - 2];
                ArrowDirection trimmedDir = GridUtility.GetDirectionFromTo(trimmedSecondLast, trimmedHead);

                if (CanEscapeInDirection(trimmedHead, trimmedDir, trimmedCells, cellToArrowId, arrowId, out bool trimmedInternal))
                {
                    bool wouldCycle = WouldCreateCycle(arrowId, trimmedHead, trimmedDir, cellToArrowId, processedArrows);
                    candidates.Add((trimmedCells, trimmedDir, trimmedInternal, wouldCycle));
                }
            }
        }

        /// <summary>
        /// 이 방향으로 Head를 설정하면 사이클이 발생하는지 확인
        /// </summary>
        private bool WouldCreateCycle(
            int arrowId,
            Vector2Int head,
            ArrowDirection headDir,
            Dictionary<Vector2Int, int> cellToArrowId,
            List<EditorArrow> processedArrows)
        {
            if (processedArrows.Count == 0)
                return false;

            // 이 화살표가 의존하게 될 화살표들 찾기
            var dependsOn = new HashSet<int>();
            Vector2Int dirVec = GridUtility.GetDirectionVector(headDir);
            Vector2Int current = head + dirVec;

            while (IsInsideGrid(current))
            {
                if (cellToArrowId.TryGetValue(current, out int blockingId) && blockingId != arrowId)
                {
                    dependsOn.Add(blockingId);
                    break; // 첫 번째로 막히는 화살표만
                }
                current += dirVec;
            }

            if (dependsOn.Count == 0)
                return false; // 의존하는 화살표 없음 = 사이클 불가

            // 의존하는 화살표들이 이 화살표에 의존하는지 확인 (직접 또는 간접)
            foreach (int depId in dependsOn)
            {
                if (DoesArrowDependOn(depId, arrowId, processedArrows, cellToArrowId))
                {
                    return true; // 사이클 발생!
                }
            }

            return false;
        }

        /// <summary>
        /// 화살표 A가 화살표 B에 의존하는지 확인 (직접 또는 간접)
        /// </summary>
        private bool DoesArrowDependOn(int arrowA, int arrowB, List<EditorArrow> processedArrows, Dictionary<Vector2Int, int> cellToArrowId)
        {
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(arrowA);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (visited.Contains(current))
                    continue;
                visited.Add(current);

                // current 화살표가 의존하는 화살표들 찾기
                var arrow = processedArrows.Find(a => a.id == current);
                if (arrow == null)
                    continue;

                Vector2Int head = arrow.cells[arrow.cells.Count - 1];
                Vector2Int dirVec = GridUtility.GetDirectionVector(arrow.headDirection);
                Vector2Int pos = head + dirVec;

                while (IsInsideGrid(pos))
                {
                    if (cellToArrowId.TryGetValue(pos, out int blockingId) && blockingId != current)
                    {
                        if (blockingId == arrowB)
                            return true; // A가 B에 의존함!

                        if (!visited.Contains(blockingId))
                            queue.Enqueue(blockingId);
                        break;
                    }
                    pos += dirVec;
                }
            }

            return false;
        }

        /// <summary>
        /// 특정 방향으로 탈출 가능한지 확인
        /// </summary>
        private bool CanEscapeInDirection(
            Vector2Int head,
            ArrowDirection dir,
            List<Vector2Int> arrowCells,
            Dictionary<Vector2Int, int> cellToArrowId,
            int arrowId,
            out bool isInternal)
        {
            isInternal = false;

            Vector2Int dirVec = GridUtility.GetDirectionVector(dir);
            Vector2Int current = head + dirVec;

            // 첫 번째 셀 확인
            if (!IsInsideGrid(current))
            {
                // 즉시 경계 탈출 = 외부 방향
                isInternal = false;
                return true;
            }

            // 자기 참조 검사 (Head 탈출 경로에 자신의 Body가 없어야 함)
            while (IsInsideGrid(current))
            {
                // Shape Mask 체크
                if (_useShapeMask && _shapeMask != null && !_shapeMask[current.x, current.y])
                {
                    // 마스크 외부 = 탈출 성공 (외부 방향)
                    return true;
                }

                // 자신의 Body와 겹치는지 확인
                for (int i = 0; i < arrowCells.Count - 1; i++) // Head 제외
                {
                    if (arrowCells[i] == current)
                    {
                        // 자기 참조! 이 방향은 무효
                        return false;
                    }
                }

                // 다른 화살표와 겹치는지 확인
                if (cellToArrowId.TryGetValue(current, out int blockingId) && blockingId != arrowId)
                {
                    // 다른 화살표가 막음 = 내부 방향 (의존성)
                    isInternal = true;
                    return true;
                }

                current += dirVec;
            }

            // 경계까지 도달 = 외부 방향
            isInternal = false;
            return true;
        }

        /// <summary>
        /// 난이도에 따른 의존성 비율 계산
        /// </summary>
        private float CalculateDependencyRatio()
        {
            // difficulty 1~3: 20~40%
            // difficulty 4~6: 50~70%
            // difficulty 7~10: 75~95%
            if (_difficulty <= 3)
                return 0.2f + (_difficulty - 1) * 0.1f;
            else if (_difficulty <= 6)
                return 0.4f + (_difficulty - 3) * 0.1f;
            else
                return 0.7f + (_difficulty - 6) * 0.0625f;
        }

        // ========== 유틸리티 ==========

        private bool IsInsideGrid(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < _gridWidth && pos.y >= 0 && pos.y < _gridHeight;
        }

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

        // ========== 해답 순서 ==========

        /// <summary>
        /// 해답 순서 가져오기
        /// </summary>
        public List<int> GetSolutionOrder()
        {
            return _dependencyGraph?.TopologicalSort() ?? new List<int>();
        }

        // ========== PHASE 3: 레이어 기반 방향 할당 (Boundary-Layered Direction Assignment) ==========

        /// <summary>
        /// 모든 화살표의 경계 레이어 계산
        /// Layer 0: 경계에 인접한 셀을 가진 화살표
        /// Layer N: Layer N-1 화살표와 인접한 화살표
        /// </summary>
        private Dictionary<int, int> ComputeArrowLayers(
            List<EditorArrow> arrows,
            Dictionary<Vector2Int, int> cellToArrowId)
        {
            var arrowLayers = new Dictionary<int, int>();
            var processed = new HashSet<int>();

            // Layer 0: 경계에 인접한 셀을 가진 화살표 식별
            var layer0Arrows = new List<int>();
            foreach (var arrow in arrows)
            {
                foreach (var cell in arrow.cells)
                {
                    if (IsBoundaryCell(cell))
                    {
                        layer0Arrows.Add(arrow.id);
                        arrowLayers[arrow.id] = 0;
                        processed.Add(arrow.id);
                        break;
                    }
                }
            }

            Debug.Log($"[MazePuzzleGenerator] Layer 0: {layer0Arrows.Count} arrows (boundary-adjacent)");

            // Layer 0이 없으면 경계에 가장 가까운 화살표를 Layer 0으로 지정
            if (layer0Arrows.Count == 0)
            {
                var sortedByBoundary = SortArrowsByBoundaryDistance(arrows);
                if (sortedByBoundary.Count > 0)
                {
                    var closestArrow = sortedByBoundary[0];
                    layer0Arrows.Add(closestArrow.id);
                    arrowLayers[closestArrow.id] = 0;
                    processed.Add(closestArrow.id);
                    Debug.LogWarning($"[MazePuzzleGenerator] No boundary arrows found, forced Arrow {closestArrow.id} to Layer 0");
                }
            }

            // BFS로 레이어 확장
            var currentLayerArrows = new List<int>(layer0Arrows);
            int currentLayer = 0;

            while (processed.Count < arrows.Count && currentLayerArrows.Count > 0)
            {
                var nextLayerArrows = new List<int>();

                foreach (int arrowId in currentLayerArrows)
                {
                    var arrow = arrows.Find(a => a.id == arrowId);
                    if (arrow == null) continue;

                    // 이 화살표의 인접 화살표 찾기
                    var neighbors = FindAdjacentArrows(arrow, cellToArrowId, arrows);
                    foreach (int neighborId in neighbors)
                    {
                        if (!processed.Contains(neighborId))
                        {
                            arrowLayers[neighborId] = currentLayer + 1;
                            processed.Add(neighborId);
                            nextLayerArrows.Add(neighborId);
                        }
                    }
                }

                if (nextLayerArrows.Count > 0)
                {
                    Debug.Log($"[MazePuzzleGenerator] Layer {currentLayer + 1}: {nextLayerArrows.Count} arrows");
                }

                currentLayerArrows = nextLayerArrows;
                currentLayer++;
            }

            // 연결되지 않은 화살표 처리 (고립된 영역)
            foreach (var arrow in arrows)
            {
                if (!arrowLayers.ContainsKey(arrow.id))
                {
                    // 가장 가까운 처리된 화살표의 레이어 + 1로 설정
                    int nearestLayer = FindNearestProcessedArrowLayer(arrow, arrowLayers, cellToArrowId);
                    arrowLayers[arrow.id] = nearestLayer + 1;
                    Debug.LogWarning($"[MazePuzzleGenerator] Isolated Arrow {arrow.id} assigned to Layer {nearestLayer + 1}");
                }
            }

            int maxLayer = 0;
            foreach (var kvp in arrowLayers)
            {
                if (kvp.Value > maxLayer) maxLayer = kvp.Value;
            }
            Debug.Log($"[MazePuzzleGenerator] Total layers: {maxLayer + 1}");

            return arrowLayers;
        }

        /// <summary>
        /// 셀이 경계에 인접한지 확인
        /// </summary>
        private bool IsBoundaryCell(Vector2Int cell)
        {
            return cell.x == 0 || cell.x == _gridWidth - 1 ||
                   cell.y == 0 || cell.y == _gridHeight - 1;
        }

        /// <summary>
        /// 화살표의 인접 화살표 찾기 (셀이 상하좌우로 인접한 화살표)
        /// </summary>
        private List<int> FindAdjacentArrows(
            EditorArrow arrow,
            Dictionary<Vector2Int, int> cellToArrowId,
            List<EditorArrow> allArrows)
        {
            var neighbors = new HashSet<int>();
            var directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),   // Up
                new Vector2Int(0, -1),  // Down
                new Vector2Int(-1, 0),  // Left
                new Vector2Int(1, 0)    // Right
            };

            foreach (var cell in arrow.cells)
            {
                foreach (var dir in directions)
                {
                    Vector2Int neighbor = cell + dir;
                    if (cellToArrowId.TryGetValue(neighbor, out int neighborArrowId))
                    {
                        if (neighborArrowId != arrow.id)
                        {
                            neighbors.Add(neighborArrowId);
                        }
                    }
                }
            }

            return new List<int>(neighbors);
        }

        /// <summary>
        /// 고립된 화살표에 대해 가장 가까운 처리된 화살표의 레이어 찾기
        /// </summary>
        private int FindNearestProcessedArrowLayer(
            EditorArrow arrow,
            Dictionary<int, int> arrowLayers,
            Dictionary<Vector2Int, int> cellToArrowId)
        {
            int minDistance = int.MaxValue;
            int nearestLayer = 0;

            foreach (var cell in arrow.cells)
            {
                // 4방향으로 가장 가까운 다른 화살표 찾기
                foreach (var dir in new[] { ArrowDirection.Up, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Right })
                {
                    Vector2Int dirVec = GridUtility.GetDirectionVector(dir);
                    Vector2Int current = cell + dirVec;
                    int distance = 1;

                    while (IsInsideGrid(current))
                    {
                        if (cellToArrowId.TryGetValue(current, out int foundArrowId) && foundArrowId != arrow.id)
                        {
                            if (arrowLayers.TryGetValue(foundArrowId, out int layer))
                            {
                                if (distance < minDistance)
                                {
                                    minDistance = distance;
                                    nearestLayer = layer;
                                }
                            }
                            break;
                        }
                        current += dirVec;
                        distance++;
                    }
                }
            }

            return nearestLayer;
        }

        /// <summary>
        /// 현재 화살표에서 낮은 레이어의 가장 가까운 화살표 찾기
        /// </summary>
        private (int arrowId, ArrowDirection direction, int distance)? FindNearestLowerLayerArrow(
            EditorArrow arrow,
            Dictionary<int, int> arrowLayers,
            Dictionary<Vector2Int, int> cellToArrowId)
        {
            int currentLayer = arrowLayers.GetValueOrDefault(arrow.id, int.MaxValue);
            var candidates = new List<(int arrowId, ArrowDirection direction, int distance, Vector2Int fromCell)>();

            // 양 끝점에서 4방향 탐색
            var endpoints = new[] { arrow.cells[0], arrow.cells[arrow.cells.Count - 1] };

            foreach (var endpoint in endpoints)
            {
                foreach (var dir in new[] { ArrowDirection.Up, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Right })
                {
                    Vector2Int dirVec = GridUtility.GetDirectionVector(dir);
                    Vector2Int current = endpoint + dirVec;
                    int distance = 1;

                    while (IsInsideGrid(current))
                    {
                        if (cellToArrowId.TryGetValue(current, out int foundArrowId) && foundArrowId != arrow.id)
                        {
                            // 낮은 레이어인지 확인
                            if (arrowLayers.TryGetValue(foundArrowId, out int foundLayer) && foundLayer < currentLayer)
                            {
                                candidates.Add((foundArrowId, dir, distance, endpoint));
                            }
                            break; // 첫 번째 만난 화살표에서 중단
                        }
                        current += dirVec;
                        distance++;
                    }
                }
            }

            if (candidates.Count == 0)
                return null;

            // 가장 가까운 후보 선택
            candidates.Sort((a, b) => a.distance.CompareTo(b.distance));
            return (candidates[0].arrowId, candidates[0].direction, candidates[0].distance);
        }

        /// <summary>
        /// 화살표가 타겟 화살표를 향하도록 HEAD 방향 설정
        /// </summary>
        private bool SetHeadTowardArrow(
            EditorArrow arrow,
            int targetArrowId,
            ArrowDirection targetDirection,
            Dictionary<Vector2Int, int> cellToArrowId)
        {
            if (arrow.cells.Count < 2)
                return false;

            // 현재 자연 방향 (cells[^2] → cells[^1])
            Vector2Int head = arrow.cells[arrow.cells.Count - 1];
            Vector2Int secondLast = arrow.cells[arrow.cells.Count - 2];
            ArrowDirection naturalDir = GridUtility.GetDirectionFromTo(secondLast, head);

            // 방법 1: 현재 방향이 타겟 방향과 일치하면 그대로 사용
            if (naturalDir == targetDirection)
            {
                // 자기 참조 검사
                if (!HasSelfReferenceInDirection(arrow.cells, head, naturalDir))
                {
                    arrow.headDirection = naturalDir;
                    return true;
                }
            }

            // 방법 2: 셀 순서 뒤집기
            Vector2Int reversedHead = arrow.cells[0];
            Vector2Int reversedSecondLast = arrow.cells[1];
            ArrowDirection reversedNaturalDir = GridUtility.GetDirectionFromTo(reversedSecondLast, reversedHead);

            // 뒤집은 방향의 반대가 타겟 방향인지 확인
            // (화살표 뒤집으면 이동 방향도 반대가 됨)
            ArrowDirection oppositeTarget = GetOppositeDirection(targetDirection);

            if (reversedNaturalDir == oppositeTarget || reversedNaturalDir == targetDirection)
            {
                // 뒤집은 상태에서 타겟 화살표를 만나는지 확인
                if (CanReachTargetArrow(reversedHead, reversedNaturalDir, targetArrowId, arrow.id, cellToArrowId))
                {
                    if (!HasSelfReferenceInDirection(arrow.cells, reversedHead, reversedNaturalDir))
                    {
                        arrow.cells.Reverse();
                        arrow.headDirection = reversedNaturalDir;
                        return true;
                    }
                }
            }

            // 방법 3: 어느 방향이든 타겟 화살표에 도달 가능한지 확인
            // 현재 HEAD에서 자연 방향으로
            if (CanReachTargetArrow(head, naturalDir, targetArrowId, arrow.id, cellToArrowId))
            {
                if (!HasSelfReferenceInDirection(arrow.cells, head, naturalDir))
                {
                    arrow.headDirection = naturalDir;
                    return true;
                }
            }

            // 뒤집은 HEAD에서 뒤집은 자연 방향으로
            if (CanReachTargetArrow(reversedHead, reversedNaturalDir, targetArrowId, arrow.id, cellToArrowId))
            {
                if (!HasSelfReferenceInDirection(arrow.cells, reversedHead, reversedNaturalDir))
                {
                    arrow.cells.Reverse();
                    arrow.headDirection = reversedNaturalDir;
                    return true;
                }
            }

            // 모든 방법 실패 - 자연 방향으로 설정 (fallback)
            arrow.headDirection = naturalDir;
            return false;
        }

        /// <summary>
        /// HEAD에서 특정 방향으로 타겟 화살표에 도달 가능한지 확인
        /// </summary>
        private bool CanReachTargetArrow(
            Vector2Int head,
            ArrowDirection dir,
            int targetArrowId,
            int selfArrowId,
            Dictionary<Vector2Int, int> cellToArrowId)
        {
            Vector2Int dirVec = GridUtility.GetDirectionVector(dir);
            Vector2Int current = head + dirVec;

            while (IsInsideGrid(current))
            {
                if (cellToArrowId.TryGetValue(current, out int foundArrowId))
                {
                    if (foundArrowId == selfArrowId)
                    {
                        // 자기 셀 - 계속 진행
                        current += dirVec;
                        continue;
                    }
                    // 타겟 화살표를 만남
                    return foundArrowId == targetArrowId;
                }
                current += dirVec;
            }

            return false; // 경계에 도달 (타겟 못 찾음)
        }

        /// <summary>
        /// 반대 방향 가져오기
        /// </summary>
        private ArrowDirection GetOppositeDirection(ArrowDirection dir)
        {
            switch (dir)
            {
                case ArrowDirection.Up: return ArrowDirection.Down;
                case ArrowDirection.Down: return ArrowDirection.Up;
                case ArrowDirection.Left: return ArrowDirection.Right;
                case ArrowDirection.Right: return ArrowDirection.Left;
                default: return dir;
            }
        }

        // ========== 화살표 분할 (경계 접근성 확보) ==========

        /// <summary>
        /// 화살표 중간에 있는 경계 셀 찾기
        /// 끝점(인덱스 0, 마지막)은 제외하고 탈출 가능한 경계 셀과 탈출 방향을 반환
        ///
        /// 수정됨: 조건 완화 - 경계 셀이면 탈출 방향만 확인, 이전 셀 방향 조건 제거
        /// </summary>
        private (int index, ArrowDirection escapeDir)? FindMiddleBoundaryCellWithEscape(
            EditorArrow arrow,
            Dictionary<Vector2Int, int> cellToArrowId)
        {
            // 인덱스 1부터 마지막-1까지 검색 (끝점 제외)
            for (int i = 1; i < arrow.cells.Count - 1; i++)
            {
                Vector2Int cell = arrow.cells[i];

                // 경계에 닿은 셀이고, 해당 방향으로 탈출 가능한지 확인
                // 이전 셀 방향 조건 제거 - 분할 후 셀 재정렬로 HEAD 정렬 맞춤
                if (cell.x == 0 && CanEscapeToExternal(cell, ArrowDirection.Left, arrow.id, cellToArrowId))
                {
                    return (i, ArrowDirection.Left);
                }
                if (cell.x == _gridWidth - 1 && CanEscapeToExternal(cell, ArrowDirection.Right, arrow.id, cellToArrowId))
                {
                    return (i, ArrowDirection.Right);
                }
                if (cell.y == 0 && CanEscapeToExternal(cell, ArrowDirection.Down, arrow.id, cellToArrowId))
                {
                    return (i, ArrowDirection.Down);
                }
                if (cell.y == _gridHeight - 1 && CanEscapeToExternal(cell, ArrowDirection.Up, arrow.id, cellToArrowId))
                {
                    return (i, ArrowDirection.Up);
                }
            }

            return null;
        }

        /// <summary>
        /// 화살표를 특정 인덱스에서 분할
        /// 분할점 셀은 첫 번째 화살표의 HEAD가 됨
        ///
        /// 수정됨: escapeDirection 파라미터 추가, 셀 재정렬로 HEAD 정렬 규칙 맞춤
        /// </summary>
        private (EditorArrow arrowA, EditorArrow arrowB) SplitArrowAtIndex(
            EditorArrow original,
            int splitIndex,
            int newArrowId,
            ArrowDirection escapeDirection)
        {
            // 화살표 A: cells[0] ~ cells[splitIndex] (분할점 포함)
            var cellsA = new List<Vector2Int>();
            for (int i = 0; i <= splitIndex; i++)
            {
                cellsA.Add(original.cells[i]);
            }

            // 화살표 B: cells[splitIndex+1] ~ cells[마지막]
            var cellsB = new List<Vector2Int>();
            for (int i = splitIndex + 1; i < original.cells.Count; i++)
            {
                cellsB.Add(original.cells[i]);
            }

            // 화살표 A: HEAD 정렬 규칙 확인 및 조정
            // cells[^2] → cells[^1] 방향이 escapeDirection과 일치해야 함
            ArrowDirection headDirA = escapeDirection;
            if (cellsA.Count >= 2)
            {
                Vector2Int secondLast = cellsA[cellsA.Count - 2];
                Vector2Int last = cellsA[cellsA.Count - 1];
                ArrowDirection naturalDir = GridUtility.GetDirectionFromTo(secondLast, last);

                // 자연 방향이 탈출 방향과 다르면 셀 순서 뒤집기 시도
                if (naturalDir != escapeDirection)
                {
                    cellsA.Reverse();

                    // 뒤집은 후 다시 확인
                    secondLast = cellsA[cellsA.Count - 2];
                    last = cellsA[cellsA.Count - 1];
                    naturalDir = GridUtility.GetDirectionFromTo(secondLast, last);

                    // 여전히 안 맞으면 분할 불가
                    if (naturalDir != escapeDirection)
                    {
                        Debug.Log($"[MazePuzzleGenerator] Split failed: Cannot align HEAD direction to {escapeDirection}");
                        return (null, null);
                    }
                }
            }

            var arrowA = new EditorArrow
            {
                id = original.id, // 원래 ID 유지
                cells = cellsA,
                headDirection = headDirA,
                color = original.color
            };

            // 화살표 B 생성 (자연 방향 사용)
            ArrowDirection headDirB = ArrowDirection.Up;
            if (cellsB.Count >= 2)
            {
                Vector2Int secondLast = cellsB[cellsB.Count - 2];
                Vector2Int last = cellsB[cellsB.Count - 1];
                headDirB = GridUtility.GetDirectionFromTo(secondLast, last);
            }

            var arrowB = new EditorArrow
            {
                id = newArrowId, // 새 ID 부여
                cells = cellsB,
                headDirection = headDirB,
                color = original.color // 같은 색상 유지
            };

            return (arrowA, arrowB);
        }

        /// <summary>
        /// 실패한 화살표들을 경계 셀에서 분할하여 새 화살표 리스트 반환
        /// 수정됨: 변경된 FindMiddleBoundaryCellWithEscape, SplitArrowAtIndex 시그니처 반영
        /// </summary>
        private List<EditorArrow> SplitFailedArrowsAtBoundaryCells(
            List<EditorArrow> arrows,
            List<int> failedArrowIds,
            Dictionary<Vector2Int, int> cellToArrowId)
        {
            if (failedArrowIds.Count == 0)
                return arrows;

            var newArrows = new List<EditorArrow>();
            int nextId = 1;
            foreach (var a in arrows)
            {
                if (a.id >= nextId) nextId = a.id + 1;
            }

            int splitCount = 0;

            int skippedNotFailed = 0;
            int skippedTooShort = 0;
            int skippedNoBoundaryCell = 0;
            int skippedCellCountMismatch = 0;
            int skippedHeadAlignFailed = 0;

            foreach (var arrow in arrows)
            {
                if (!failedArrowIds.Contains(arrow.id))
                {
                    newArrows.Add(arrow);
                    skippedNotFailed++;
                    continue;
                }

                // 최소 3셀 이상이어야 분할 가능 (분할 후 각 화살표가 최소 2셀)
                if (arrow.cells.Count < 3)
                {
                    newArrows.Add(arrow);
                    skippedTooShort++;
                    continue;
                }

                // 경계 셀 찾기 (끝점 제외, 탈출 가능한 방향이 있는 셀)
                // 수정됨: 반환 타입이 (int index, ArrowDirection escapeDir)?
                var splitResult = FindMiddleBoundaryCellWithEscape(arrow, cellToArrowId);

                if (splitResult.HasValue)
                {
                    int splitIndex = splitResult.Value.index;
                    ArrowDirection escapeDir = splitResult.Value.escapeDir;

                    // 분할 후 양쪽 모두 최소 2셀 확보되는지 확인
                    int cellsAfterSplit = arrow.cells.Count - splitIndex - 1;
                    if (splitIndex >= 1 && cellsAfterSplit >= 2)
                    {
                        // 수정됨: escapeDirection 파라미터 추가
                        var (arrowA, arrowB) = SplitArrowAtIndex(arrow, splitIndex, nextId, escapeDir);

                        // 분할 실패 (HEAD 정렬 불가) 체크
                        if (arrowA == null || arrowB == null)
                        {
                            newArrows.Add(arrow);
                            skippedHeadAlignFailed++;
                            continue;
                        }

                        // 분할된 화살표들 추가
                        if (arrowA.cells.Count >= 2)
                        {
                            newArrows.Add(arrowA);
                        }
                        if (arrowB.cells.Count >= 2)
                        {
                            newArrows.Add(arrowB);
                            nextId++;
                        }

                        splitCount++;
                        Debug.Log($"[MazePuzzleGenerator] Split Arrow {arrow.id} at index {splitIndex} (escape: {escapeDir}): " +
                                  $"A({arrowA.cells.Count} cells, HEAD={arrowA.headDirection}) + B({arrowB.cells.Count} cells)");

                        // cellToArrowId 업데이트
                        foreach (var cell in arrowA.cells)
                        {
                            cellToArrowId[cell] = arrowA.id;
                        }
                        foreach (var cell in arrowB.cells)
                        {
                            cellToArrowId[cell] = arrowB.id;
                        }
                    }
                    else
                    {
                        // 분할 불가 (셀 수 부족) → 그대로 유지
                        newArrows.Add(arrow);
                        skippedCellCountMismatch++;
                    }
                }
                else
                {
                    // 분할할 경계 셀 없음 → 그대로 유지
                    newArrows.Add(arrow);
                    skippedNoBoundaryCell++;
                }
            }

            // 디버그 통계 출력
            Debug.Log($"[MazePuzzleGenerator] Split stats: " +
                      $"splitCount={splitCount}, " +
                      $"skippedNotFailed={skippedNotFailed}, " +
                      $"skippedTooShort={skippedTooShort}, " +
                      $"skippedNoBoundaryCell={skippedNoBoundaryCell}, " +
                      $"skippedCellCountMismatch={skippedCellCountMismatch}, " +
                      $"skippedHeadAlignFailed={skippedHeadAlignFailed}");

            if (splitCount > 0)
            {
                Debug.Log($"[MazePuzzleGenerator] Split {splitCount} arrows. Total arrows: {arrows.Count} → {newArrows.Count}");
            }

            return newArrows;
        }
    }
}
