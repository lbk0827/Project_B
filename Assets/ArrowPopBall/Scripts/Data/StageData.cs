using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 스테이지 데이터 (ScriptableObject)
    /// 순수 맵 데이터만 포함 - 레벨 설정은 LevelConfig에서 관리
    /// </summary>
    [CreateAssetMenu(fileName = "Stage_001", menuName = "ArrowPopBall/Stage Data")]
    public class StageData : ScriptableObject
    {
        // ========== 기본 정보 ==========
        [Header("기본 정보")]
        [Tooltip("스테이지 고유 ID (예: Stage_001)")]
        public string stageId;

        [Tooltip("스테이지 설명 (에디터용)")]
        [TextArea(2, 4)]
        public string description;

        // ========== 그리드 설정 ==========
        [Header("그리드 설정")]
        public int gridWidth = 10;
        public int gridHeight = 10;

        // ========== 화살표 데이터 ==========
        [Header("화살표 데이터")]
        public List<StageArrowData> arrows = new List<StageArrowData>();

        // ========== 정답 순서 ==========
        [Header("정답 순서")]
        [Tooltip("화살표 ID 순서 (Solvability 보장)")]
        public List<int> solutionOrder = new List<int>();

        // ========== 유틸리티 ==========

        /// <summary>
        /// 화살표 개수
        /// </summary>
        public int ArrowCount => arrows?.Count ?? 0;

        /// <summary>
        /// 색상별 화살표 개수 반환
        /// </summary>
        public Dictionary<GameColor, int> GetColorCounts()
        {
            var counts = new Dictionary<GameColor, int>();
            if (arrows == null) return counts;

            foreach (var arrow in arrows)
            {
                if (!counts.ContainsKey(arrow.color))
                    counts[arrow.color] = 0;
                counts[arrow.color]++;
            }
            return counts;
        }

        /// <summary>
        /// LevelData로 변환 (기존 시스템 호환용)
        /// </summary>
        public LevelData ToLevelData(int levelId, int lives, int[] starThresholds)
        {
            var levelData = new LevelData
            {
                levelId = levelId,
                gridWidth = gridWidth,
                gridHeight = gridHeight,
                maxLives = lives,
                starThresholds1 = starThresholds?.Length > 0 ? starThresholds[0] : 3,
                starThresholds2 = starThresholds?.Length > 1 ? starThresholds[1] : 2,
                starThresholds3 = starThresholds?.Length > 2 ? starThresholds[2] : 1,
                arrows = ConvertArrows(),
                balloons = GenerateBalloons()
            };

            return levelData;
        }

        private ArrowData[] ConvertArrows()
        {
            if (arrows == null) return new ArrowData[0];

            var result = new ArrowData[arrows.Count];
            for (int i = 0; i < arrows.Count; i++)
            {
                var src = arrows[i];
                result[i] = new ArrowData
                {
                    id = src.id,
                    x = src.x,
                    y = src.y,
                    color = src.color,
                    segments = ConvertSegments(src.segments)
                };
            }
            return result;
        }

        private SegmentData[] ConvertSegments(List<StageSegmentData> srcSegments)
        {
            if (srcSegments == null) return new SegmentData[0];

            var result = new SegmentData[srcSegments.Count];
            for (int i = 0; i < srcSegments.Count; i++)
            {
                result[i] = new SegmentData
                {
                    direction = srcSegments[i].direction,
                    length = srcSegments[i].length
                };
            }
            return result;
        }

        private BalloonData[] GenerateBalloons()
        {
            var colorCounts = GetColorCounts();
            var balloons = new List<BalloonData>();
            int slotIndex = 0;

            foreach (var kvp in colorCounts)
            {
                for (int i = 0; i < kvp.Value; i++)
                {
                    balloons.Add(new BalloonData
                    {
                        id = balloons.Count,
                        slotIndex = slotIndex++,
                        color = kvp.Key
                    });
                }
            }

            return balloons.ToArray();
        }
    }

    /// <summary>
    /// 스테이지용 화살표 데이터 (ScriptableObject 내장용)
    /// </summary>
    [Serializable]
    public class StageArrowData
    {
        public int id;
        public int x;
        public int y;
        public GameColor color;
        public List<StageSegmentData> segments = new List<StageSegmentData>();

        public Vector2Int StartPosition => new Vector2Int(x, y);

        /// <summary>
        /// 화살표 머리 방향
        /// </summary>
        public ArrowDirection HeadDirection
        {
            get
            {
                if (segments == null || segments.Count == 0)
                    return ArrowDirection.Up;
                return segments[segments.Count - 1].direction;
            }
        }

        /// <summary>
        /// 전체 셀 개수
        /// </summary>
        public int TotalLength
        {
            get
            {
                if (segments == null) return 0;
                int total = 0;
                foreach (var seg in segments)
                    total += seg.length;
                return total;
            }
        }
    }

    /// <summary>
    /// 스테이지용 세그먼트 데이터
    /// </summary>
    [Serializable]
    public class StageSegmentData
    {
        public ArrowDirection direction;
        public int length;
    }
}