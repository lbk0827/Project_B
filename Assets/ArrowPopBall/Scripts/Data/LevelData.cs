using System;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 레벨 데이터 (JSON 직렬화용)
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public int levelId;
        public int gridWidth;
        public int gridHeight;
        public ArrowData[] arrows;
        public BalloonData[] balloons;
        public int parMoves;
        public int starThresholds1;
        public int starThresholds2;
        public int starThresholds3;
        public int maxLives = 3;  // 레벨별 최대 Life 개수 (기본 3개)
    }

    /// <summary>
    /// 화살표 데이터 (폴리라인 지원)
    /// </summary>
    [Serializable]
    public class ArrowData
    {
        public int id;
        public int x;
        public int y;
        public GameColor color;
        public SegmentData[] segments;

        public Vector2Int StartPosition => new Vector2Int(x, y);

        /// <summary>
        /// 화살표 머리 방향 (마지막 세그먼트 방향)
        /// </summary>
        public ArrowDirection HeadDirection
        {
            get
            {
                if (segments == null || segments.Length == 0)
                    return ArrowDirection.Up;
                return segments[segments.Length - 1].direction;
            }
        }

        /// <summary>
        /// 전체 셀 개수 (모든 세그먼트 길이 합)
        /// </summary>
        public int TotalLength
        {
            get
            {
                if (segments == null)
                    return 0;
                int total = 0;
                foreach (var seg in segments)
                {
                    total += seg.length;
                }
                return total;
            }
        }
    }

    /// <summary>
    /// 세그먼트 데이터 (화살표의 직선 조각)
    /// </summary>
    [Serializable]
    public class SegmentData
    {
        public ArrowDirection direction;
        public int length;
    }

    /// <summary>
    /// 풍선 데이터
    /// </summary>
    [Serializable]
    public class BalloonData
    {
        public int id;
        public int slotIndex;
        public GameColor color;
    }
}