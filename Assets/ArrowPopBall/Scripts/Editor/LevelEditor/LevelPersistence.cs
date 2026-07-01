using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Game.Data;

namespace Game.Editor.LevelEditor
{
    /// <summary>
    /// 레벨 데이터 저장/로드 관리 클래스
    /// Level Editor 리팩토링 - Phase 3
    /// </summary>
    public static class LevelPersistence
    {
        private const string LEVELS_PATH = "Assets/ArrowPopBall/Resources/Levels";

        // ========== 파일 경로 ==========

        public static string GetLevelFilePath(int levelId)
        {
            return $"{LEVELS_PATH}/Level_{levelId:D3}.json";
        }

        // ========== 저장 ==========

        /// <summary>
        /// 레벨 데이터를 JSON으로 저장
        /// </summary>
        public static (bool success, string message) SaveLevel(
            int levelId,
            int gridWidth,
            int gridHeight,
            List<EditorArrow> arrows)
        {
            if (arrows.Count == 0)
            {
                return (false, "No arrows to save!");
            }

            // 폴더 생성
            if (!Directory.Exists(LEVELS_PATH))
            {
                Directory.CreateDirectory(LEVELS_PATH);
            }

            // LevelData 생성
            LevelData levelData = new LevelData
            {
                levelId = levelId,
                gridWidth = gridWidth,
                gridHeight = gridHeight,
                arrows = ConvertToArrowDataArray(arrows),
                balloons = GenerateBalloonData(arrows),
                parMoves = arrows.Count,
                starThresholds1 = arrows.Count + 2,
                starThresholds2 = arrows.Count + 1,
                starThresholds3 = arrows.Count
            };

            // JSON 저장
            string json = JsonUtility.ToJson(levelData, true);
            string filePath = GetLevelFilePath(levelId);
            File.WriteAllText(filePath, json);

            AssetDatabase.Refresh();
            return (true, $"Saved to {filePath}");
        }

        // ========== 로드 ==========

        /// <summary>
        /// JSON에서 레벨 데이터 로드
        /// </summary>
        public static (bool success, string message, int gridWidth, int gridHeight, List<EditorArrow> arrows) LoadLevel(int levelId)
        {
            string filePath = GetLevelFilePath(levelId);

            if (!File.Exists(filePath))
            {
                return (false, $"File not found: {filePath}", 0, 0, null);
            }

            string json = File.ReadAllText(filePath);
            LevelData levelData = JsonUtility.FromJson<LevelData>(json);

            if (levelData == null)
            {
                return (false, "Failed to parse JSON!", 0, 0, null);
            }

            // 화살표 변환
            var arrows = new List<EditorArrow>();
            if (levelData.arrows != null)
            {
                foreach (var arrowData in levelData.arrows)
                {
                    var editorArrow = ConvertFromArrowData(arrowData);
                    if (editorArrow != null)
                        arrows.Add(editorArrow);
                }
            }

            return (true, $"Loaded {arrows.Count} arrows from {filePath}", levelData.gridWidth, levelData.gridHeight, arrows);
        }

        // ========== 데이터 변환 ==========

        /// <summary>
        /// EditorArrow 리스트를 ArrowData 배열로 변환
        /// </summary>
        public static ArrowData[] ConvertToArrowDataArray(List<EditorArrow> arrows)
        {
            var arrowDataList = new List<ArrowData>();

            foreach (var arrow in arrows)
            {
                if (arrow.cells.Count == 0) continue;

                var arrowData = new ArrowData
                {
                    id = arrow.id,
                    x = arrow.cells[0].x,
                    y = arrow.cells[0].y,
                    color = arrow.color,
                    segments = CalculateSegments(arrow.cells, arrow.headDirection)
                };

                arrowDataList.Add(arrowData);
            }

            return arrowDataList.ToArray();
        }

        /// <summary>
        /// ArrowData를 EditorArrow로 변환
        /// </summary>
        public static EditorArrow ConvertFromArrowData(ArrowData arrowData)
        {
            var cells = new List<Vector2Int>();
            Vector2Int current = new Vector2Int(arrowData.x, arrowData.y);
            cells.Add(current);

            if (arrowData.segments != null)
            {
                foreach (var segment in arrowData.segments)
                {
                    Vector2Int dir = GridUtility.GetDirectionVector(segment.direction);
                    for (int i = 0; i < segment.length; i++)
                    {
                        current += dir;
                        cells.Add(current);
                    }
                }
            }

            return new EditorArrow
            {
                id = arrowData.id,
                cells = cells,
                headDirection = arrowData.HeadDirection,
                color = arrowData.color
            };
        }

        /// <summary>
        /// 셀 리스트에서 세그먼트 계산
        /// </summary>
        public static SegmentData[] CalculateSegments(List<Vector2Int> cells, ArrowDirection headDir)
        {
            if (cells.Count <= 1)
            {
                // 1칸짜리 화살표: length=0 세그먼트로 저장
                return new SegmentData[]
                {
                    new SegmentData { direction = headDir, length = 0 }
                };
            }

            var segments = new List<SegmentData>();
            ArrowDirection currentDir = ArrowDirection.Up;
            int currentLength = 0;

            for (int i = 1; i < cells.Count; i++)
            {
                ArrowDirection dir = GridUtility.GetDirectionFromTo(cells[i - 1], cells[i]);

                if (i == 1)
                {
                    currentDir = dir;
                    currentLength = 1;
                }
                else if (dir == currentDir)
                {
                    currentLength++;
                }
                else
                {
                    segments.Add(new SegmentData { direction = currentDir, length = currentLength });
                    currentDir = dir;
                    currentLength = 1;
                }
            }

            // 마지막 세그먼트 추가
            if (currentLength > 0)
            {
                segments.Add(new SegmentData { direction = currentDir, length = currentLength });
            }

            // headDir(탈출 방향)을 저장하기 위해 length=0 세그먼트 추가
            if (segments.Count == 0 || segments[^1].direction != headDir)
            {
                segments.Add(new SegmentData { direction = headDir, length = 0 });
            }

            return segments.ToArray();
        }

        /// <summary>
        /// 화살표 색상에 맞는 풍선 데이터 생성
        /// </summary>
        public static BalloonData[] GenerateBalloonData(List<EditorArrow> arrows)
        {
            // 색상별 화살표 개수 계산
            var colorCounts = new Dictionary<GameColor, int>();
            foreach (var arrow in arrows)
            {
                if (!colorCounts.ContainsKey(arrow.color))
                    colorCounts[arrow.color] = 0;
                colorCounts[arrow.color]++;
            }

            // 풍선 데이터 생성
            var balloons = new List<BalloonData>();
            int slotIndex = 0;

            foreach (var kvp in colorCounts)
            {
                for (int i = 0; i < kvp.Value; i++)
                {
                    balloons.Add(new BalloonData
                    {
                        id = slotIndex + 1,
                        slotIndex = slotIndex,
                        color = kvp.Key
                    });
                    slotIndex++;
                }
            }

            return balloons.ToArray();
        }
    }
}
