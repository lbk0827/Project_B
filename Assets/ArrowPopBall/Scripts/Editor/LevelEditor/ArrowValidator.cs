using System.Collections.Generic;
using UnityEngine;
using Game.Data;

namespace Game.Editor.LevelEditor
{
    /// <summary>
    /// 화살표 무결성 검증 클래스
    /// 모든 화살표 생성 로직에서 반드시 사용하여 버그 방지
    ///
    /// 핵심 검증 규칙:
    /// 1. 최소 길이: cells.Count >= 2
    /// 2. HEAD 정렬: cells[^2] → cells[^1] 방향 = headDirection
    /// 3. 자기 참조 금지: Head 탈출 경로에 자신의 Body 없음
    /// 4. 셀 연속성: 인접 셀만 연결 (대각선 금지)
    /// </summary>
    public static class ArrowValidator
    {
        // ========== 단일 화살표 검증 ==========

        /// <summary>
        /// 화살표 무결성 검증 - 모든 화살표 생성 후 반드시 호출
        /// </summary>
        /// <returns>(유효 여부, 오류 메시지)</returns>
        public static (bool valid, string error) Validate(EditorArrow arrow, int gridWidth, int gridHeight)
        {
            if (arrow == null)
                return (false, "Arrow is null");

            if (arrow.cells == null)
                return (false, "Arrow cells is null");

            // 1. 최소 길이 검증
            // 특수 케이스: 1칸짜리 화살표 허용 (Gap Filling 코너 셀용)
            if (arrow.cells.Count < 1)
                return (false, $"Arrow must have at least 1 cell, but has {arrow.cells.Count}");

            // 1칸짜리 화살표는 특수 케이스로 별도 검증
            if (arrow.cells.Count == 1)
            {
                // 1칸짜리 화살표는 headDirection만 유효하면 됨
                return (true, null);
            }

            // 2. HEAD 정렬 규칙 검증 (2셀 이상인 경우)
            // cells[^2] → cells[^1] 방향이 headDirection과 일치해야 함
            // 이 규칙은 대각선 방지를 위해 반드시 지켜야 함
            Vector2Int secondLast = arrow.cells[arrow.cells.Count - 2];
            Vector2Int last = arrow.cells[arrow.cells.Count - 1];
            ArrowDirection cellDirection = GridUtility.GetDirectionFromTo(secondLast, last);

            if (cellDirection != arrow.headDirection)
            {
                return (false, $"HEAD alignment mismatch: cell direction from {secondLast} to {last} is {cellDirection}, but headDirection is {arrow.headDirection}");
            }

            // 3. 자기 참조 검증 (Head가 자신의 Body를 바라보는지)
            var selfRefResult = ValidateNoSelfReference(arrow, gridWidth, gridHeight);
            if (!selfRefResult.valid)
                return selfRefResult;

            // 4. 셀 연속성 검증 (인접 셀만 연결, 대각선 금지)
            var continuityResult = ValidateCellContinuity(arrow);
            if (!continuityResult.valid)
                return continuityResult;

            return (true, null);
        }

        /// <summary>
        /// 자기 참조 검증 - Head 탈출 경로에 자신의 Body가 없는지 확인
        /// </summary>
        private static (bool valid, string error) ValidateNoSelfReference(EditorArrow arrow, int gridWidth, int gridHeight)
        {
            Vector2Int head = arrow.cells[arrow.cells.Count - 1];
            Vector2Int dir = GridUtility.GetDirectionVector(arrow.headDirection);
            Vector2Int current = head + dir;

            // Head에서 headDirection 방향으로 이동하면서 자신의 Body와 겹치는지 확인
            while (IsInsideGrid(current, gridWidth, gridHeight))
            {
                // Head 자신은 제외하고 Body 셀만 확인
                for (int i = 0; i < arrow.cells.Count - 1; i++)
                {
                    if (arrow.cells[i] == current)
                    {
                        return (false, $"Head at {head} with direction {arrow.headDirection} looks at own body cell {current} (index {i})");
                    }
                }
                current += dir;
            }

            return (true, null);
        }

        /// <summary>
        /// 셀 연속성 검증 - 모든 셀이 상하좌우로만 연결되어 있는지 확인
        /// </summary>
        private static (bool valid, string error) ValidateCellContinuity(EditorArrow arrow)
        {
            for (int i = 1; i < arrow.cells.Count; i++)
            {
                Vector2Int diff = arrow.cells[i] - arrow.cells[i - 1];
                int manhattanDist = Mathf.Abs(diff.x) + Mathf.Abs(diff.y);

                if (manhattanDist != 1)
                {
                    return (false, $"Non-adjacent cells at index {i - 1} ({arrow.cells[i - 1]}) to {i} ({arrow.cells[i]}), distance = {manhattanDist}");
                }

                // 대각선 체크 (둘 다 0이 아니면 대각선)
                if (diff.x != 0 && diff.y != 0)
                {
                    return (false, $"Diagonal movement detected at index {i - 1} to {i}: {diff}");
                }
            }

            return (true, null);
        }

        // ========== 전체 화살표 리스트 검증 ==========

        /// <summary>
        /// 전체 화살표 리스트 검증
        /// </summary>
        /// <returns>(유효 여부, 오류 메시지 리스트)</returns>
        public static (bool valid, List<string> errors) ValidateAll(
            List<EditorArrow> arrows,
            int gridWidth,
            int gridHeight)
        {
            var errors = new List<string>();

            if (arrows == null)
            {
                errors.Add("Arrows list is null");
                return (false, errors);
            }

            // 각 화살표 개별 검증
            foreach (var arrow in arrows)
            {
                var (valid, error) = Validate(arrow, gridWidth, gridHeight);
                if (!valid)
                {
                    errors.Add($"Arrow {arrow.id}: {error}");
                }
            }

            // 셀 겹침 검증
            var overlapErrors = ValidateNoOverlap(arrows);
            errors.AddRange(overlapErrors);

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// 셀 겹침 검증 - 각 셀이 정확히 하나의 화살표에만 속하는지 확인
        /// </summary>
        private static List<string> ValidateNoOverlap(List<EditorArrow> arrows)
        {
            var errors = new List<string>();
            var cellToArrow = new Dictionary<Vector2Int, int>(); // cell → arrow id

            foreach (var arrow in arrows)
            {
                foreach (var cell in arrow.cells)
                {
                    if (cellToArrow.TryGetValue(cell, out int existingArrowId))
                    {
                        errors.Add($"Cell {cell} is used by both Arrow {existingArrowId} and Arrow {arrow.id}");
                    }
                    else
                    {
                        cellToArrow[cell] = arrow.id;
                    }
                }
            }

            return errors;
        }

        // ========== 유틸리티 ==========

        /// <summary>
        /// 좌표가 그리드 내부인지 확인
        /// </summary>
        private static bool IsInsideGrid(Vector2Int pos, int gridWidth, int gridHeight)
        {
            return pos.x >= 0 && pos.x < gridWidth && pos.y >= 0 && pos.y < gridHeight;
        }

        // ========== 디버그 출력 ==========

        /// <summary>
        /// 검증 결과를 Unity 콘솔에 출력
        /// </summary>
        public static void LogValidationResult(List<EditorArrow> arrows, int gridWidth, int gridHeight)
        {
            var (valid, errors) = ValidateAll(arrows, gridWidth, gridHeight);

            if (valid)
            {
                Debug.Log($"[ArrowValidator] All {arrows.Count} arrows are valid.");
            }
            else
            {
                Debug.LogError($"[ArrowValidator] Validation failed with {errors.Count} errors:");
                foreach (var error in errors)
                {
                    Debug.LogError($"  - {error}");
                }
            }
        }

        /// <summary>
        /// 단일 화살표 상세 정보 출력 (디버깅용)
        /// </summary>
        public static void LogArrowDetails(EditorArrow arrow)
        {
            if (arrow == null)
            {
                Debug.Log("[ArrowValidator] Arrow is null");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[ArrowValidator] Arrow {arrow.id} Details:");
            sb.AppendLine($"  Color: {arrow.color}");
            sb.AppendLine($"  HeadDirection: {arrow.headDirection}");
            sb.AppendLine($"  Cell Count: {arrow.cells?.Count ?? 0}");

            if (arrow.cells != null && arrow.cells.Count > 0)
            {
                sb.AppendLine($"  Tail (cells[0]): {arrow.cells[0]}");
                sb.AppendLine($"  Head (cells[^1]): {arrow.cells[arrow.cells.Count - 1]}");

                if (arrow.cells.Count >= 2)
                {
                    var secondLast = arrow.cells[arrow.cells.Count - 2];
                    var last = arrow.cells[arrow.cells.Count - 1];
                    var cellDir = GridUtility.GetDirectionFromTo(secondLast, last);
                    sb.AppendLine($"  cells[^2] → cells[^1] direction: {cellDir}");
                    sb.AppendLine($"  HEAD alignment match: {cellDir == arrow.headDirection}");
                }

                sb.Append("  Cells: ");
                for (int i = 0; i < arrow.cells.Count; i++)
                {
                    sb.Append(arrow.cells[i]);
                    if (i < arrow.cells.Count - 1) sb.Append(" → ");
                }
            }

            Debug.Log(sb.ToString());
        }
    }
}
