using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Data;

namespace Game.Editor.LevelEditor
{
    /// <summary>
    /// Maze-guided ReverseFilling 퍼즐 생성기
    ///
    /// 핵심 개념:
    /// 1. 미로 경로를 "가이드라인"으로 사용
    /// 2. 경계에서 경로를 따라 화살표 "투입" 시뮬레이션
    /// 3. 투입 역순 = 해답 순서 (DAG 보장, 사이클 불가)
    ///
    /// HEAD 정렬 규칙 (대각선 방지):
    /// - cells[^2] → cells[^1] 방향 = headDirection
    /// - 경계 진입점에서 시작하면 HEAD 정렬 자동 충족
    /// </summary>
    public class MazeGuidedGenerator
    {
        // ========== 설정 ==========
        private int _gridWidth;
        private int _gridHeight;
        private bool[,] _shapeMask;
        private bool _useShapeMask;
        private int _minArrowLength = 2;
        private int _maxArrowLength = 8;
        private int _colorCount = 6;
        private System.Random _random;

        // ========== 생성 결과 ==========
        private List<EditorArrow> _arrows;
        private List<int> _solutionOrder;
        private bool[,] _occupied;

        // ========== 초기화 ==========

        public MazeGuidedGenerator(int seed = -1)
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

        // ========== 메인 생성 ==========

        /// <summary>
        /// Maze-guided ReverseFilling으로 퍼즐 생성
        /// </summary>
        public (List<EditorArrow> arrows, List<int> solutionOrder) Generate()
        {
            _arrows = new List<EditorArrow>();
            _solutionOrder = new List<int>();
            _occupied = new bool[_gridWidth, _gridHeight];

            // Phase 1: 미로 경로 생성
            Debug.Log("[MazeGuidedGenerator] Phase 1: Generating maze path...");
            var mazeGenerator = new MazeGenerator(_random.Next());
            mazeGenerator.SetContext(_gridWidth, _gridHeight, _shapeMask, _useShapeMask);
            List<Vector2Int> mazePath = mazeGenerator.GeneratePath();

            if (mazePath == null || mazePath.Count == 0)
            {
                Debug.LogError("[MazeGuidedGenerator] Failed to generate maze path");
                return (null, null);
            }
            Debug.Log($"[MazeGuidedGenerator] Maze path generated: {mazePath.Count} cells");

            // Phase 2: 경계 진입점에서 화살표 투입
            Debug.Log("[MazeGuidedGenerator] Phase 2: Creating arrows from boundary...");
            var insertionOrders = new Dictionary<int, int>();
            int arrowId = 1;
            int insertionOrder = 0;

            // 미로 경로를 인접 셀 집합으로 변환
            var pathSet = new HashSet<Vector2Int>(mazePath);

            int maxIterations = _gridWidth * _gridHeight * 2;
            int iteration = 0;

            while (HasUnoccupiedCells(pathSet) && iteration < maxIterations)
            {
                iteration++;

                // 경계 진입점 찾기
                var entryPoint = FindBoundaryEntryPoint(pathSet);

                if (entryPoint.HasValue)
                {
                    var (startCell, escapeDir) = entryPoint.Value;

                    // 진입점에서 화살표 생성
                    var arrow = CreateArrowFromBoundary(startCell, escapeDir, pathSet, arrowId);

                    if (arrow != null && arrow.cells.Count >= 2)
                    {
                        // 검증: HEAD 정렬 규칙 확인
                        if (ValidateHeadAlignment(arrow))
                        {
                            _arrows.Add(arrow);
                            insertionOrders[arrow.id] = insertionOrder++;
                            MarkOccupied(arrow.cells);
                            arrowId++;
                            continue;
                        }
                        else
                        {
                            Debug.LogWarning($"[MazeGuidedGenerator] Arrow {arrowId} failed HEAD alignment check");
                        }
                    }
                }

                // 경계 진입점 실패 또는 화살표 생성 실패 → 내부 셀에서 시도
                var internalArrow = TryCreateInternalArrow(pathSet, arrowId);
                if (internalArrow != null)
                {
                    _arrows.Add(internalArrow);
                    insertionOrders[internalArrow.id] = insertionOrder++;
                    MarkOccupied(internalArrow.cells);
                    arrowId++;
                }
                else
                {
                    // 1칸짜리라도 만들어서 Fill Rate 보장
                    var singleCell = FindAnySingleUnoccupiedCell(pathSet);
                    if (singleCell.HasValue)
                    {
                        var singleArrow = CreateSingleCellArrow(singleCell.Value, arrowId);
                        if (singleArrow != null)
                        {
                            _arrows.Add(singleArrow);
                            insertionOrders[singleArrow.id] = insertionOrder++;
                            MarkOccupied(singleArrow.cells);
                            arrowId++;
                        }
                    }
                }
            }

            Debug.Log($"[MazeGuidedGenerator] Phase 2 complete: {_arrows.Count} arrows created");

            // Phase 3: 해답 순서 결정 (투입 역순)
            Debug.Log("[MazeGuidedGenerator] Phase 3: Determining solution order...");
            _solutionOrder = _arrows
                .OrderByDescending(a => insertionOrders.GetValueOrDefault(a.id, 0))
                .Select(a => a.id)
                .ToList();

            // Phase 4: 색상 할당
            Debug.Log("[MazeGuidedGenerator] Phase 4: Assigning colors...");
            AssignColors();

            // Phase 5: 검증
            Debug.Log("[MazeGuidedGenerator] Phase 5: Validating...");
            var (valid, errors) = ValidateResult();

            if (!valid)
            {
                Debug.LogWarning($"[MazeGuidedGenerator] Validation issues ({errors.Count}):");
                foreach (var error in errors)
                {
                    Debug.LogWarning($"  - {error}");
                }
            }
            else
            {
                Debug.Log($"[MazeGuidedGenerator] Success! {_arrows.Count} arrows, 100% fill rate, solvable");
            }

            return (_arrows, _solutionOrder);
        }

        // ========== Phase 2: 경계 진입점에서 화살표 생성 ==========

        /// <summary>
        /// 경계 진입점 찾기 - 경계에 인접한 빈 셀
        /// </summary>
        private (Vector2Int cell, ArrowDirection escapeDir)? FindBoundaryEntryPoint(HashSet<Vector2Int> pathSet)
        {
            var candidates = new List<(Vector2Int cell, ArrowDirection escapeDir)>();

            foreach (var cell in pathSet)
            {
                if (_occupied[cell.x, cell.y]) continue;

                // 이 셀이 어떤 경계에 인접해 있는지 확인
                if (cell.x == 0 && CanEscapeToBoundary(cell, ArrowDirection.Left))
                    candidates.Add((cell, ArrowDirection.Left));
                if (cell.x == _gridWidth - 1 && CanEscapeToBoundary(cell, ArrowDirection.Right))
                    candidates.Add((cell, ArrowDirection.Right));
                if (cell.y == 0 && CanEscapeToBoundary(cell, ArrowDirection.Down))
                    candidates.Add((cell, ArrowDirection.Down));
                if (cell.y == _gridHeight - 1 && CanEscapeToBoundary(cell, ArrowDirection.Up))
                    candidates.Add((cell, ArrowDirection.Up));
            }

            if (candidates.Count == 0)
                return null;

            // 랜덤 선택 (약간의 변화를 줌)
            return candidates[_random.Next(candidates.Count)];
        }

        /// <summary>
        /// 경계 진입점에서 화살표 생성
        ///
        /// 핵심 원리:
        /// 1. 경계 셀(startCell)에서 시작, escapeDir 방향으로 탈출
        /// 2. 경로 성장은 탈출 방향의 반대(entryDir)로 진행
        /// 3. 최종적으로 cells를 뒤집으면:
        ///    - cells[^1] = 경계 셀 (HEAD)
        ///    - cells[^2] → cells[^1] 방향 = entryDir의 반대 = escapeDir ✓
        /// </summary>
        private EditorArrow CreateArrowFromBoundary(
            Vector2Int startCell,
            ArrowDirection escapeDir,
            HashSet<Vector2Int> pathSet,
            int arrowId)
        {
            // 진입 방향 = 탈출 방향의 반대
            ArrowDirection entryDir = GetOppositeDirection(escapeDir);

            // 성장 경로: [startCell(HEAD후보), 다음셀, ...] 순서
            var growingCells = new List<Vector2Int> { startCell };
            int targetLength = _random.Next(_minArrowLength, _maxArrowLength + 1);

            Vector2Int current = startCell;
            ArrowDirection lastMoveDir = entryDir;

            // 진입 방향으로 성장
            while (growingCells.Count < targetLength)
            {
                // 다음 셀 후보 찾기
                Vector2Int? nextCell = FindNextGrowthCell(current, lastMoveDir, growingCells, pathSet);

                if (!nextCell.HasValue)
                    break;

                growingCells.Add(nextCell.Value);
                lastMoveDir = GetDirectionFromTo(current, nextCell.Value);
                current = nextCell.Value;
            }

            // 최소 길이 검증
            if (growingCells.Count < 2)
                return null;

            // 경로 뒤집기: [HEAD후보, ..., TAIL후보] → [TAIL, ..., HEAD]
            var cells = new List<Vector2Int>(growingCells);
            cells.Reverse();

            // 이제 cells[^1] = startCell = HEAD

            return new EditorArrow
            {
                id = arrowId,
                cells = cells,
                headDirection = escapeDir,
                color = GameColor.Red // Phase 4에서 할당
            };
        }

        /// <summary>
        /// 성장 방향으로 다음 셀 찾기
        /// </summary>
        private Vector2Int? FindNextGrowthCell(
            Vector2Int current,
            ArrowDirection preferredDir,
            List<Vector2Int> currentCells,
            HashSet<Vector2Int> pathSet)
        {
            var candidates = new List<(Vector2Int cell, int priority)>();

            // 4방향 탐색
            foreach (var dir in GetShuffledDirections())
            {
                // 역방향 금지 (지그재그 방지)
                if (dir == GetOppositeDirection(preferredDir) && currentCells.Count > 1)
                    continue;

                Vector2Int next = current + GetDirectionVector(dir);

                if (!IsValidCell(next)) continue;
                if (_occupied[next.x, next.y]) continue;
                if (currentCells.Contains(next)) continue;

                // 우선순위: 미로 경로에 있는 셀 > 일반 셀
                int priority = pathSet.Contains(next) ? 0 : 1;

                // 선호 방향과 같으면 약간의 보너스
                if (dir == preferredDir)
                    priority -= 1;

                candidates.Add((next, priority));
            }

            if (candidates.Count == 0)
                return null;

            // 우선순위 정렬 후 상위에서 랜덤 선택
            candidates.Sort((a, b) => a.priority.CompareTo(b.priority));

            int topCount = Mathf.Max(1, candidates.Count / 2);
            return candidates[_random.Next(topCount)].cell;
        }

        /// <summary>
        /// 내부 셀에서 화살표 생성 시도
        /// </summary>
        private EditorArrow TryCreateInternalArrow(HashSet<Vector2Int> pathSet, int arrowId)
        {
            // 빈 셀 중에서 탈출 가능한 셀 찾기
            var candidates = new List<(Vector2Int cell, ArrowDirection escapeDir)>();

            foreach (var cell in pathSet)
            {
                if (_occupied[cell.x, cell.y]) continue;

                // 탈출 가능한 방향 찾기
                foreach (var dir in GetShuffledDirections())
                {
                    if (CanEscapeToBoundary(cell, dir))
                    {
                        candidates.Add((cell, dir));
                        break; // 하나만 찾으면 됨
                    }
                }
            }

            if (candidates.Count == 0)
                return null;

            // 랜덤 선택
            var (startCell, escapeDir) = candidates[_random.Next(candidates.Count)];

            // 경로 성장
            var cells = new List<Vector2Int> { startCell };
            int targetLength = _random.Next(_minArrowLength, _maxArrowLength + 1);

            ArrowDirection growDir = GetOppositeDirection(escapeDir);
            Vector2Int current = startCell;

            while (cells.Count < targetLength)
            {
                Vector2Int? nextCell = FindNextGrowthCell(current, growDir, cells, pathSet);
                if (!nextCell.HasValue) break;

                cells.Add(nextCell.Value);
                growDir = GetDirectionFromTo(current, nextCell.Value);
                current = nextCell.Value;
            }

            if (cells.Count < 2)
                return null;

            // 뒤집기: cells[^1] = startCell = HEAD
            cells.Reverse();

            // HEAD 정렬 검증
            if (cells.Count >= 2)
            {
                var cellDir = GetDirectionFromTo(cells[cells.Count - 2], cells[cells.Count - 1]);
                if (cellDir != escapeDir)
                {
                    // 정렬 실패 - 정렬 셀 추가 시도
                    Vector2Int alignCell = cells[cells.Count - 1] + GetDirectionVector(escapeDir);
                    if (IsValidCell(alignCell) && !_occupied[alignCell.x, alignCell.y] && !cells.Contains(alignCell))
                    {
                        cells.Add(alignCell);
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            return new EditorArrow
            {
                id = arrowId,
                cells = cells,
                headDirection = escapeDir,
                color = GameColor.Red
            };
        }

        /// <summary>
        /// 1칸짜리 화살표 생성 (Fill Rate 보장용)
        /// </summary>
        private EditorArrow CreateSingleCellArrow(Vector2Int cell, int arrowId)
        {
            // 탈출 가능한 방향 찾기
            foreach (var dir in GetShuffledDirections())
            {
                if (CanEscapeToBoundary(cell, dir))
                {
                    // 1칸 화살표는 HEAD 정렬 규칙 적용 불가 (cells.Count < 2)
                    // 탈출 방향으로 1칸 더 추가해서 2칸 만들기 시도
                    Vector2Int nextCell = cell + GetDirectionVector(dir);
                    if (IsValidCell(nextCell) && !_occupied[nextCell.x, nextCell.y])
                    {
                        // 2칸 화살표 생성 가능
                        return new EditorArrow
                        {
                            id = arrowId,
                            cells = new List<Vector2Int> { nextCell, cell }, // [TAIL, HEAD]
                            headDirection = dir,
                            color = GameColor.Red
                        };
                    }

                    // 반대 방향으로 시도
                    Vector2Int oppositeCell = cell + GetDirectionVector(GetOppositeDirection(dir));
                    if (IsValidCell(oppositeCell) && !_occupied[oppositeCell.x, oppositeCell.y])
                    {
                        return new EditorArrow
                        {
                            id = arrowId,
                            cells = new List<Vector2Int> { oppositeCell, cell },
                            headDirection = dir,
                            color = GameColor.Red
                        };
                    }
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
                arrow.color = availableColors[_random.Next(availableColors.Count)];
            }
        }

        // ========== Phase 5: 검증 ==========

        private (bool valid, List<string> errors) ValidateResult()
        {
            var errors = new List<string>();

            // 1. Arrow 무결성 검증
            var (arrowsValid, arrowErrors) = ArrowValidator.ValidateAll(_arrows, _gridWidth, _gridHeight);
            if (!arrowsValid)
            {
                errors.AddRange(arrowErrors);
            }

            // 2. Fill Rate 검증
            var (fillValid, fillRate, totalCells, activeCells) = SolvabilityValidator.ValidateFillRate(
                _arrows, _gridWidth, _gridHeight, _shapeMask, _useShapeMask);
            if (!fillValid)
            {
                errors.Add($"Fill rate is {fillRate:F1}% ({totalCells}/{activeCells} cells) - expected 100%");
            }

            // 3. Solvability 검증
            var (solvable, solveError) = SolvabilityValidator.Validate(
                _arrows, _solutionOrder, _gridWidth, _gridHeight);
            if (!solvable)
            {
                errors.Add($"Solvability failed: {solveError}");
            }

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// HEAD 정렬 규칙 검증
        /// </summary>
        private bool ValidateHeadAlignment(EditorArrow arrow)
        {
            if (arrow.cells.Count < 2) return false;

            Vector2Int secondLast = arrow.cells[arrow.cells.Count - 2];
            Vector2Int last = arrow.cells[arrow.cells.Count - 1];
            ArrowDirection cellDir = GetDirectionFromTo(secondLast, last);

            return cellDir == arrow.headDirection;
        }

        // ========== 유틸리티 ==========

        private bool HasUnoccupiedCells(HashSet<Vector2Int> pathSet)
        {
            foreach (var cell in pathSet)
            {
                if (!_occupied[cell.x, cell.y])
                    return true;
            }
            return false;
        }

        private Vector2Int? FindAnySingleUnoccupiedCell(HashSet<Vector2Int> pathSet)
        {
            foreach (var cell in pathSet)
            {
                if (!_occupied[cell.x, cell.y])
                    return cell;
            }
            return null;
        }

        private void MarkOccupied(List<Vector2Int> cells)
        {
            foreach (var cell in cells)
            {
                if (IsInBounds(cell))
                    _occupied[cell.x, cell.y] = true;
            }
        }

        private bool CanEscapeToBoundary(Vector2Int cell, ArrowDirection dir)
        {
            Vector2Int dirVec = GetDirectionVector(dir);
            Vector2Int current = cell + dirVec;

            while (IsInBounds(current))
            {
                // 마스크 외부면 탈출 성공
                if (_useShapeMask && _shapeMask != null && !_shapeMask[current.x, current.y])
                    return true;

                // 다른 화살표에 막히면 불가
                if (_occupied[current.x, current.y])
                    return false;

                current += dirVec;
            }
            return true; // 그리드 경계 도달
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

        private List<ArrowDirection> GetShuffledDirections()
        {
            var dirs = new List<ArrowDirection>
            {
                ArrowDirection.Up, ArrowDirection.Down,
                ArrowDirection.Left, ArrowDirection.Right
            };

            // Fisher-Yates shuffle
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
    }
}