using System.Collections.Generic;
using UnityEngine;
using Game.Data;

namespace Game.Editor.LevelEditor
{
    /// <summary>
    /// 퍼즐 Solvability 검증 클래스
    /// 해답 순서대로 실제 탈출 시뮬레이션을 수행하여 풀 수 있는 퍼즐인지 확인
    /// </summary>
    public static class SolvabilityValidator
    {
        public enum ProblemType
        {
            None,
            MissingData,
            InvalidSolutionOrder,
            EscapeBlocked,
            Deadlock
        }

        public class ValidationResult
        {
            public bool solvable;
            public string error;
            public ProblemType problemType;
            public int failedStep = -1;
            public int blockedArrowId = -1;
            public int blockingArrowId = -1;
            public Vector2Int blockingCell = Vector2Int.zero;
            public List<int> simulatedOrder = new List<int>();
            public List<int> problemArrowIds = new List<int>();
        }

        /// <summary>
        /// 퍼즐이 풀 수 있는지 검증
        /// </summary>
        /// <param name="arrows">모든 화살표</param>
        /// <param name="solutionOrder">해답 순서 (화살표 ID 리스트)</param>
        /// <param name="gridWidth">그리드 너비</param>
        /// <param name="gridHeight">그리드 높이</param>
        /// <returns>(풀 수 있음 여부, 오류 메시지)</returns>
        public static (bool solvable, string error) Validate(
            List<EditorArrow> arrows,
            List<int> solutionOrder,
            int gridWidth, int gridHeight)
        {
            var result = ValidateDetailed(arrows, solutionOrder, gridWidth, gridHeight);
            return (result.solvable, result.error);
        }

        /// <summary>
        /// 퍼즐이 풀 수 있는지 상세 검증
        /// 실패 사유/문제 화살표/막는 화살표/막힘 셀 정보를 함께 반환
        /// </summary>
        public static ValidationResult ValidateDetailed(
            List<EditorArrow> arrows,
            List<int> solutionOrder,
            int gridWidth, int gridHeight)
        {
            var result = new ValidationResult();

            if (arrows == null || arrows.Count == 0)
            {
                result.solvable = false;
                result.problemType = ProblemType.MissingData;
                result.error = "No arrows to validate";
                return result;
            }

            if (solutionOrder == null || solutionOrder.Count == 0)
            {
                result.solvable = false;
                result.problemType = ProblemType.MissingData;
                result.error = "No solution order provided";
                return result;
            }

            if (solutionOrder.Count != arrows.Count)
            {
                result.solvable = false;
                result.problemType = ProblemType.InvalidSolutionOrder;
                result.error = $"Solution order count ({solutionOrder.Count}) doesn't match arrow count ({arrows.Count})";
                return result;
            }

            // 점유 셀 맵 생성
            var occupiedCells = new HashSet<Vector2Int>();
            var arrowIdToCells = new Dictionary<int, List<Vector2Int>>();

            foreach (var arrow in arrows)
            {
                arrowIdToCells[arrow.id] = new List<Vector2Int>(arrow.cells);
                foreach (var cell in arrow.cells)
                {
                    occupiedCells.Add(cell);
                }
            }

            // 해답 순서대로 탈출 시뮬레이션
            for (int step = 0; step < solutionOrder.Count; step++)
            {
                int arrowId = solutionOrder[step];
                var arrow = arrows.Find(a => a.id == arrowId);

                if (arrow == null)
                {
                    result.solvable = false;
                    result.problemType = ProblemType.InvalidSolutionOrder;
                    result.failedStep = step + 1;
                    result.blockedArrowId = arrowId;
                    result.problemArrowIds.Add(arrowId);
                    result.error = $"Step {step + 1}: Arrow {arrowId} not found";
                    return result;
                }

                // 탈출 경로 확인
                var (canEscape, blockingCell, blockingArrowId) = CanEscapeNow(arrow, occupiedCells, arrowIdToCells, gridWidth, gridHeight);

                if (!canEscape)
                {
                    result.solvable = false;
                    result.problemType = ProblemType.EscapeBlocked;
                    result.failedStep = step + 1;
                    result.blockedArrowId = arrowId;
                    result.blockingArrowId = blockingArrowId;
                    result.blockingCell = blockingCell;
                    result.problemArrowIds.Add(arrowId);
                    if (blockingArrowId >= 0)
                    {
                        result.problemArrowIds.Add(blockingArrowId);
                    }

                    string blockerInfo = blockingArrowId >= 0
                        ? $"blocked by Arrow {blockingArrowId} at {blockingCell}"
                        : $"blocked at {blockingCell}";
                    result.error = $"Step {step + 1}: Arrow {arrowId} cannot escape - {blockerInfo}";
                    return result;
                }

                // 탈출 후 셀 제거
                foreach (var cell in arrow.cells)
                {
                    occupiedCells.Remove(cell);
                }
                result.simulatedOrder.Add(arrowId);
            }

            result.solvable = true;
            result.problemType = ProblemType.None;
            result.error = null;
            return result;
        }

        /// <summary>
        /// 화살표가 현재 상태에서 탈출 가능한지 확인
        /// </summary>
        private static (bool canEscape, Vector2Int blockingCell, int blockingArrowId) CanEscapeNow(
            EditorArrow arrow,
            HashSet<Vector2Int> occupiedCells,
            Dictionary<int, List<Vector2Int>> arrowIdToCells,
            int gridWidth, int gridHeight)
        {
            Vector2Int head = arrow.cells[arrow.cells.Count - 1];
            Vector2Int dir = GridUtility.GetDirectionVector(arrow.headDirection);
            Vector2Int pos = head + dir;

            // HEAD 방향으로 경계까지 이동
            while (IsInsideGrid(pos, gridWidth, gridHeight))
            {
                // 자기 셀은 제외
                if (arrow.cells.Contains(pos))
                {
                    pos += dir;
                    continue;
                }

                // 다른 화살표가 막고 있는지 확인
                if (occupiedCells.Contains(pos))
                {
                    // 어떤 화살표가 막고 있는지 찾기
                    int blockingId = -1;
                    foreach (var kvp in arrowIdToCells)
                    {
                        if (kvp.Key != arrow.id && kvp.Value.Contains(pos))
                        {
                            blockingId = kvp.Key;
                            break;
                        }
                    }

                    return (false, pos, blockingId);
                }

                pos += dir;
            }

            return (true, Vector2Int.zero, -1);
        }

        /// <summary>
        /// 좌표가 그리드 내부인지 확인
        /// </summary>
        private static bool IsInsideGrid(Vector2Int pos, int gridWidth, int gridHeight)
        {
            return pos.x >= 0 && pos.x < gridWidth && pos.y >= 0 && pos.y < gridHeight;
        }

        /// <summary>
        /// 난이도 분석 - 각 풀이 스텝에서 동시 탈출 가능한 화살표 수 측정
        /// </summary>
        /// <returns>(최대 동시 free 수, 스텝별 free 수 리스트)</returns>
        public static (int maxSimultaneousFree, List<int> freeCountPerStep) AnalyzeDifficulty(
            List<EditorArrow> arrows, int gridWidth, int gridHeight)
        {
            var freeCountPerStep = new List<int>();
            int maxFree = 0;

            if (arrows == null || arrows.Count == 0)
                return (0, freeCountPerStep);

            // 점유 셀 맵 생성
            var occupiedCells = new HashSet<Vector2Int>();
            var arrowIdToCells = new Dictionary<int, List<Vector2Int>>();
            var remaining = new List<EditorArrow>();

            foreach (var arrow in arrows)
            {
                arrowIdToCells[arrow.id] = new List<Vector2Int>(arrow.cells);
                remaining.Add(arrow);
                foreach (var cell in arrow.cells)
                {
                    occupiedCells.Add(cell);
                }
            }

            // 스텝별 시뮬레이션
            int maxIterations = arrows.Count * 2;
            int iteration = 0;

            while (remaining.Count > 0 && iteration < maxIterations)
            {
                iteration++;

                // 현재 스텝에서 탈출 가능한 모든 화살표 카운트
                int freeCount = 0;
                int firstFreeIndex = -1;

                for (int i = 0; i < remaining.Count; i++)
                {
                    var (canEscape, _, _) = CanEscapeNow(
                        remaining[i], occupiedCells, arrowIdToCells, gridWidth, gridHeight);

                    if (canEscape)
                    {
                        freeCount++;
                        if (firstFreeIndex < 0)
                            firstFreeIndex = i;
                    }
                }

                if (firstFreeIndex < 0)
                    break; // 데드락

                freeCountPerStep.Add(freeCount);
                maxFree = Mathf.Max(maxFree, freeCount);

                // 첫 번째 free arrow 탈출
                var escapingArrow = remaining[firstFreeIndex];
                foreach (var cell in escapingArrow.cells)
                {
                    occupiedCells.Remove(cell);
                }
                remaining.RemoveAt(firstFreeIndex);
            }

            return (maxFree, freeCountPerStep);
        }

        /// <summary>
        /// Fill Rate 검증
        /// </summary>
        public static (bool valid, float fillRate, int totalCells, int activeCells) ValidateFillRate(
            List<EditorArrow> arrows,
            int gridWidth, int gridHeight,
            bool[,] shapeMask = null,
            bool useShapeMask = false)
        {
            // 화살표가 차지하는 총 셀 수
            int totalCells = 0;
            var usedCells = new HashSet<Vector2Int>();

            foreach (var arrow in arrows)
            {
                foreach (var cell in arrow.cells)
                {
                    if (usedCells.Add(cell))
                    {
                        totalCells++;
                    }
                    else
                    {
                        Debug.LogWarning($"[SolvabilityValidator] Duplicate cell detected: {cell}");
                    }
                }
            }

            // 활성 셀 수 계산
            int activeCells;
            if (useShapeMask && shapeMask != null)
            {
                activeCells = 0;
                for (int x = 0; x < gridWidth; x++)
                {
                    for (int y = 0; y < gridHeight; y++)
                    {
                        if (shapeMask[x, y]) activeCells++;
                    }
                }
            }
            else
            {
                activeCells = gridWidth * gridHeight;
            }

            float fillRate = activeCells > 0 ? (float)totalCells / activeCells * 100f : 0f;
            bool valid = Mathf.Abs(fillRate - 100f) < 0.1f; // 99.9% ~ 100.1% 허용

            return (valid, fillRate, totalCells, activeCells);
        }

        /// <summary>
        /// 종합 검증 (Solvability + Fill Rate + Arrow 무결성)
        /// </summary>
        public static (bool valid, List<string> errors) ValidateAll(
            List<EditorArrow> arrows,
            List<int> solutionOrder,
            int gridWidth, int gridHeight,
            bool[,] shapeMask = null,
            bool useShapeMask = false)
        {
            var errors = new List<string>();

            // 1. Arrow 무결성 검증
            var (arrowsValid, arrowErrors) = ArrowValidator.ValidateAll(arrows, gridWidth, gridHeight);
            if (!arrowsValid)
            {
                errors.AddRange(arrowErrors);
            }

            // 2. Fill Rate 검증
            var (fillValid, fillRate, totalCells, activeCells) = ValidateFillRate(
                arrows, gridWidth, gridHeight, shapeMask, useShapeMask);

            if (!fillValid)
            {
                errors.Add($"Fill rate is {fillRate:F1}% ({totalCells}/{activeCells} cells) - expected 100%");
            }

            // 3. Solvability 검증
            var (solvable, solveError) = Validate(arrows, solutionOrder, gridWidth, gridHeight);
            if (!solvable)
            {
                errors.Add($"Solvability failed: {solveError}");
            }

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// 검증 결과를 Unity 콘솔에 출력
        /// </summary>
        public static void LogValidationResult(
            List<EditorArrow> arrows,
            List<int> solutionOrder,
            int gridWidth, int gridHeight,
            bool[,] shapeMask = null,
            bool useShapeMask = false)
        {
            var (valid, errors) = ValidateAll(arrows, solutionOrder, gridWidth, gridHeight, shapeMask, useShapeMask);

            if (valid)
            {
                Debug.Log($"[SolvabilityValidator] Puzzle is VALID and SOLVABLE!");
                Debug.Log($"  - {arrows.Count} arrows");
                Debug.Log($"  - Solution order: [{string.Join(", ", solutionOrder)}]");
            }
            else
            {
                Debug.LogError($"[SolvabilityValidator] Puzzle validation FAILED with {errors.Count} errors:");
                foreach (var error in errors)
                {
                    Debug.LogError($"  - {error}");
                }
            }
        }
    }
}
