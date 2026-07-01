using System.Collections.Generic;
using UnityEngine;
using Game.Data;

namespace Game.Editor.LevelEditor
{
    /// <summary>
    /// 색상 관리 유틸리티 클래스
    /// Level Editor 리팩토링 - Phase 1
    /// </summary>
    public static class ColorManager
    {
        // ========== 색상 변환 ==========

        /// <summary>
        /// GameColor를 Unity Color로 변환 (에디터 표시용)
        /// </summary>
        public static Color GetColorForGameColor(GameColor gameColor)
        {
            return gameColor switch
            {
                GameColor.Red => Color.red,
                GameColor.Blue => Color.blue,
                GameColor.Green => Color.green,
                GameColor.Yellow => Color.yellow,
                GameColor.Purple => new Color(0.5f, 0, 0.5f),
                GameColor.Orange => new Color(1f, 0.5f, 0),
                GameColor.Cyan => Color.cyan,
                GameColor.Pink => new Color(1f, 0.4f, 0.7f),
                GameColor.Brown => new Color(0.6f, 0.3f, 0.1f),
                GameColor.Lime => new Color(0.5f, 1f, 0),
                GameColor.Navy => new Color(0, 0, 0.5f),
                GameColor.Magenta => Color.magenta,
                GameColor.Black => Color.black,
                _ => Color.gray
            };
        }

        // ========== 사용 가능한 색상 목록 ==========

        /// <summary>
        /// 자동 색상 모드: 그리드 크기에 따라 적절한 수의 색상 선택
        /// </summary>
        public static List<GameColor> GetAutoColors(int gridWidth, int gridHeight)
        {
            var colors = new List<GameColor>();
            int autoColorCount = Mathf.Clamp((gridWidth * gridHeight) / 15, 3, 6);

            for (int i = 0; i < autoColorCount; i++)
            {
                colors.Add((GameColor)i);
            }

            return colors;
        }

        /// <summary>
        /// 수동 색상 모드: 선택된 색상만 사용
        /// </summary>
        public static List<GameColor> GetManualColors(bool[] selectedColors)
        {
            var colors = new List<GameColor>();

            if (selectedColors != null)
            {
                for (int i = 0; i < selectedColors.Length && i < 13; i++)
                {
                    if (selectedColors[i])
                    {
                        colors.Add((GameColor)i);
                    }
                }
            }

            // 최소 1개 보장
            if (colors.Count < 1)
            {
                colors.Clear();
                colors.Add(GameColor.Red);
            }

            return colors;
        }

        /// <summary>
        /// 현재 설정에 따라 사용 가능한 색상 목록 반환
        /// </summary>
        public static List<GameColor> GetAvailableColors(bool useAutoColors, bool[] selectedColors, int gridWidth, int gridHeight)
        {
            if (useAutoColors)
            {
                return GetAutoColors(gridWidth, gridHeight);
            }
            return GetManualColors(selectedColors);
        }

        // ========== 색상 매핑 ==========

        /// <summary>
        /// 셀 목록에서 가장 흔한 매핑 색상 반환
        /// </summary>
        public static GameColor GetDominantColorForCells(
            List<Vector2Int> cells,
            GameColor[,] colorMap,
            int gridWidth,
            int gridHeight,
            List<GameColor> fallbackColors)
        {
            if (colorMap == null || cells == null || cells.Count == 0)
            {
                if (fallbackColors != null && fallbackColors.Count > 0)
                {
                    return fallbackColors[Random.Range(0, fallbackColors.Count)];
                }
                return GameColor.Red;
            }

            var colorCount = new Dictionary<GameColor, int>();
            foreach (var cell in cells)
            {
                if (cell.x >= 0 && cell.x < gridWidth && cell.y >= 0 && cell.y < gridHeight)
                {
                    var color = colorMap[cell.x, cell.y];
                    if (!colorCount.ContainsKey(color))
                        colorCount[color] = 0;
                    colorCount[color]++;
                }
            }

            GameColor dominant = GameColor.Red;
            int maxCount = 0;
            foreach (var kvp in colorCount)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    dominant = kvp.Key;
                }
            }
            return dominant;
        }

        /// <summary>
        /// 랜덤 색상 선택
        /// </summary>
        public static GameColor GetRandomColor(List<GameColor> availableColors)
        {
            if (availableColors == null || availableColors.Count == 0)
            {
                return GameColor.Red;
            }
            return availableColors[Random.Range(0, availableColors.Count)];
        }
    }
}
