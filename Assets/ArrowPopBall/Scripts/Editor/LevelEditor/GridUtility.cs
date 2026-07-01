using System.Collections.Generic;
using UnityEngine;
using Game.Data;

namespace Game.Editor.LevelEditor
{
    /// <summary>
    /// 그리드 좌표 및 방향 관련 유틸리티 (static class)
    /// Level Editor 리팩토링 - Phase 1
    /// </summary>
    public static class GridUtility
    {
        // ========== 방향 벡터 변환 ==========

        /// <summary>
        /// ArrowDirection을 Vector2Int로 변환
        /// </summary>
        public static Vector2Int GetDirectionVector(ArrowDirection dir)
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

        /// <summary>
        /// 반대 방향 반환
        /// </summary>
        public static ArrowDirection GetOppositeDirection(ArrowDirection dir)
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

        /// <summary>
        /// 두 셀 간의 방향 계산
        /// </summary>
        public static ArrowDirection GetDirectionFromTo(Vector2Int from, Vector2Int to)
        {
            Vector2Int diff = to - from;
            if (diff.x > 0) return ArrowDirection.Right;
            if (diff.x < 0) return ArrowDirection.Left;
            if (diff.y > 0) return ArrowDirection.Up;
            if (diff.y < 0) return ArrowDirection.Down;
            return ArrowDirection.Up;
        }

        /// <summary>
        /// 수직 방향 목록 반환 (Up/Down ↔ Left/Right)
        /// </summary>
        public static List<ArrowDirection> GetPerpendicularDirections(ArrowDirection dir)
        {
            return dir switch
            {
                ArrowDirection.Up or ArrowDirection.Down =>
                    new List<ArrowDirection> { ArrowDirection.Left, ArrowDirection.Right },
                ArrowDirection.Left or ArrowDirection.Right =>
                    new List<ArrowDirection> { ArrowDirection.Up, ArrowDirection.Down },
                _ => new List<ArrowDirection>()
            };
        }

        /// <summary>
        /// 4방향을 섞어서 반환 (Fisher-Yates shuffle)
        /// </summary>
        public static List<ArrowDirection> GetShuffledDirections()
        {
            var dirs = new List<ArrowDirection>
            {
                ArrowDirection.Up, ArrowDirection.Down,
                ArrowDirection.Left, ArrowDirection.Right
            };

            for (int i = dirs.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (dirs[i], dirs[j]) = (dirs[j], dirs[i]);
            }

            return dirs;
        }

        // ========== 셀 유효성 검사 ==========

        /// <summary>
        /// 셀이 그리드 경계 내에 있는지 확인 (마스크 미적용)
        /// </summary>
        public static bool IsInBounds(Vector2Int cell, int gridWidth, int gridHeight)
        {
            return cell.x >= 0 && cell.x < gridWidth &&
                   cell.y >= 0 && cell.y < gridHeight;
        }

        /// <summary>
        /// 셀이 유효한지 확인 (그리드 경계 + 마스크 적용)
        /// </summary>
        public static bool IsValidCell(Vector2Int cell, int gridWidth, int gridHeight, bool[,] shapeMask)
        {
            if (!IsInBounds(cell, gridWidth, gridHeight))
                return false;

            // 마스크가 있으면 마스크 내 셀인지 확인
            if (shapeMask != null)
            {
                return shapeMask[cell.x, cell.y];
            }
            return true;
        }

        // ========== 경계 거리 계산 ==========

        /// <summary>
        /// 특정 방향으로 경계까지의 거리 (마스크 미적용)
        /// </summary>
        public static int GetDistanceToBoundarySimple(Vector2Int cell, ArrowDirection dir, int gridWidth, int gridHeight)
        {
            return dir switch
            {
                ArrowDirection.Up => gridHeight - 1 - cell.y,
                ArrowDirection.Down => cell.y,
                ArrowDirection.Right => gridWidth - 1 - cell.x,
                ArrowDirection.Left => cell.x,
                _ => int.MaxValue
            };
        }

        /// <summary>
        /// 특정 방향으로 경계까지의 거리 (마스크 적용)
        /// </summary>
        public static int GetDistanceToBoundary(Vector2Int cell, ArrowDirection dir, int gridWidth, int gridHeight, bool[,] shapeMask)
        {
            // 마스크 사용 시: 마스크 경계까지의 거리 계산
            if (shapeMask != null)
            {
                Vector2Int dirVec = GetDirectionVector(dir);
                Vector2Int current = cell + dirVec;
                int distance = 0;

                while (current.x >= 0 && current.x < gridWidth &&
                       current.y >= 0 && current.y < gridHeight &&
                       shapeMask[current.x, current.y])
                {
                    distance++;
                    current += dirVec;
                }
                return distance;
            }

            // 마스크 미사용 시: 그리드 경계까지의 거리
            return GetDistanceToBoundarySimple(cell, dir, gridWidth, gridHeight);
        }

        /// <summary>
        /// 셀에서 가장 가까운 경계까지의 거리 (4방향 중 최소값)
        /// 값이 클수록 내부에 위치
        /// </summary>
        public static float GetMinDistanceToEdge(Vector2Int cell, int gridWidth, int gridHeight, bool[,] shapeMask)
        {
            int distUp = GetDistanceToBoundary(cell, ArrowDirection.Up, gridWidth, gridHeight, shapeMask);
            int distDown = GetDistanceToBoundary(cell, ArrowDirection.Down, gridWidth, gridHeight, shapeMask);
            int distRight = GetDistanceToBoundary(cell, ArrowDirection.Right, gridWidth, gridHeight, shapeMask);
            int distLeft = GetDistanceToBoundary(cell, ArrowDirection.Left, gridWidth, gridHeight, shapeMask);
            return Mathf.Min(distUp, distDown, distRight, distLeft);
        }
    }
}
