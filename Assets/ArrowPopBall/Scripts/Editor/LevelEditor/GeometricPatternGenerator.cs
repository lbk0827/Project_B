using System.Collections.Generic;
using Game.Data;

namespace Game.Editor.LevelEditor
{
    /// <summary>
    /// Geometric 패턴 유형 (Flags로 복수 선택 지원)
    /// </summary>
    [System.Flags]
    public enum GeometricPatternType
    {
        None     = 0,
        Straight = 1 << 0,   // 직선형
        LShape   = 1 << 1,   // L자형
        UShape   = 1 << 2,   // U자형
        Zigzag   = 1 << 3,   // 지그재그형 (계단식 수직 교대)
        Snake    = 1 << 4,   // Snake형 (행 반복)
        Spiral   = 1 << 5,   // 나선형
        Outline  = 1 << 6,   // 직사각형 외곽형
        All      = Straight | LShape | UShape | Zigzag | Snake | Spiral | Outline
    }

    /// <summary>
    /// Geometric 패턴 방향 시퀀스 생성기
    ///
    /// 핵심 역할: Head에서 역방향으로 성장할 때 따라갈 "방향 시퀀스"를 미리 계산
    /// 주의: ReverseGrowth에서 growDir = GetOppositeDirection(headDir)이므로,
    ///       "Straight Right" 패턴은 headDir=Left인 화살표의 body가 오른쪽으로 성장
    /// </summary>
    public static class GeometricPatternGenerator
    {
        // 각 패턴의 최소 필요 셀 수 (Head 포함)
        private static readonly Dictionary<GeometricPatternType, int> MinLengthRequirements = new()
        {
            { GeometricPatternType.Straight, 2 },
            { GeometricPatternType.LShape,   3 },
            { GeometricPatternType.UShape,   5 },
            { GeometricPatternType.Zigzag,   4 },
            { GeometricPatternType.Snake,    6 },
            { GeometricPatternType.Spiral,   6 },
            { GeometricPatternType.Outline,  6 },
        };

        /// <summary>
        /// 활성화된 패턴 중 targetLength에 적합한 것을 랜덤 선택
        /// targetLength가 패턴 최소 요구보다 짧으면 해당 패턴 제외
        /// </summary>
        public static GeometricPatternType PickRandomEnabled(
            GeometricPatternType enabledPatterns, int targetLength, System.Random random)
        {
            var candidates = new List<GeometricPatternType>();

            foreach (var pattern in GetAllPatterns())
            {
                if ((enabledPatterns & pattern) == 0) continue;

                if (MinLengthRequirements.TryGetValue(pattern, out int minLen) && targetLength < minLen)
                    continue;

                candidates.Add(pattern);
            }

            if (candidates.Count == 0)
                return GeometricPatternType.Straight; // 최소 fallback

            return candidates[random.Next(candidates.Count)];
        }

        /// <summary>
        /// 지정된 패턴 타입에 따른 방향 시퀀스 반환
        /// Head에서 역방향으로 성장할 때 각 스텝에서 어느 방향으로 갈지의 리스트
        /// </summary>
        /// <param name="patternType">패턴 유형</param>
        /// <param name="growDirection">성장 시작 방향 (headDir의 반대)</param>
        /// <param name="targetLength">목표 셀 수 (Head 포함)</param>
        /// <param name="random">시드 재현용 Random 인스턴스</param>
        /// <returns>방향 시퀀스 (최대 length = targetLength - 1)</returns>
        public static List<ArrowDirection> GenerateDirectionSequence(
            GeometricPatternType patternType,
            ArrowDirection growDirection,
            int targetLength,
            System.Random random)
        {
            int steps = targetLength - 1; // Head 제외
            if (steps <= 0)
                return new List<ArrowDirection>();

            return patternType switch
            {
                GeometricPatternType.Straight => GenerateStraight(growDirection, steps),
                GeometricPatternType.LShape   => GenerateLShape(growDirection, steps, random),
                GeometricPatternType.UShape   => GenerateUShape(growDirection, steps, random),
                GeometricPatternType.Zigzag   => GenerateZigzag(growDirection, steps, random),
                GeometricPatternType.Snake    => GenerateSnake(growDirection, steps, random),
                GeometricPatternType.Spiral   => GenerateSpiral(growDirection, steps),
                GeometricPatternType.Outline  => GenerateOutline(growDirection, steps, random),
                _ => GenerateStraight(growDirection, steps)
            };
        }

        // ========== 패턴별 생성 알고리즘 ==========

        /// <summary>
        /// 직선형: growDir 반복
        /// 결과: → → → → →
        /// </summary>
        private static List<ArrowDirection> GenerateStraight(ArrowDirection growDir, int steps)
        {
            var seq = new List<ArrowDirection>(steps);
            for (int i = 0; i < steps; i++)
                seq.Add(growDir);
            return seq;
        }

        /// <summary>
        /// L자형: 한 번 90도 꺾임
        /// 결과: → → → ↑ ↑
        /// </summary>
        private static List<ArrowDirection> GenerateLShape(
            ArrowDirection growDir, int steps, System.Random random)
        {
            var seq = new List<ArrowDirection>(steps);

            int firstLen = random.Next(1, steps); // 1 ~ steps-1
            int secondLen = steps - firstLen;

            var perpDir = GetPerpendicularDirection(growDir, random.Next(2) == 0);

            for (int i = 0; i < firstLen; i++)
                seq.Add(growDir);
            for (int i = 0; i < secondLen; i++)
                seq.Add(perpDir);

            return seq;
        }

        /// <summary>
        /// U자형: forward → perpendicular → backward (3구간)
        /// 결과: → → ↑ ↑ ← ←
        /// </summary>
        private static List<ArrowDirection> GenerateUShape(
            ArrowDirection growDir, int steps, System.Random random)
        {
            var seq = new List<ArrowDirection>(steps);

            int side1 = System.Math.Max(1, steps / 3);
            int bottom = System.Math.Max(1, steps / 3);
            int side2 = steps - side1 - bottom;
            if (side2 < 1) { side2 = 1; side1 = System.Math.Max(1, steps - bottom - 1); }

            var perpDir = GetPerpendicularDirection(growDir, random.Next(2) == 0);
            var oppositeDir = GetOppositeDirection(growDir);

            for (int i = 0; i < side1; i++)
                seq.Add(growDir);
            for (int i = 0; i < bottom; i++)
                seq.Add(perpDir);
            for (int i = 0; i < side2; i++)
                seq.Add(oppositeDir);

            return seq;
        }

        /// <summary>
        /// 지그재그형: growDir과 수직 방향을 교대 (수직 방향이 매번 바뀜)
        /// FindNextBodyCell의 반대방향 hard skip을 회피하기 위해
        /// perpDir1/perpDir2를 직접 교대하지 않고, growDir을 사이에 끼워 방향 전환
        ///
        /// 결과: → → ↑ → → ↓ → → ↑ (growDir 사이에 수직 방향 교대)
        /// </summary>
        private static List<ArrowDirection> GenerateZigzag(
            ArrowDirection growDir, int steps, System.Random random)
        {
            var seq = new List<ArrowDirection>(steps);

            var perpDir1 = GetPerpendicularDirection(growDir, true);  // 시계 방향
            var perpDir2 = GetPerpendicularDirection(growDir, false); // 반시계 방향

            int forwardStep = random.Next(1, 3) + 1; // 2~3
            int perpStep = 1;
            bool useFirst = true;

            while (seq.Count < steps)
            {
                // forward 구간
                for (int i = 0; i < forwardStep && seq.Count < steps; i++)
                    seq.Add(growDir);

                // perpendicular 구간 (교대)
                var currentPerp = useFirst ? perpDir1 : perpDir2;
                for (int i = 0; i < perpStep && seq.Count < steps; i++)
                    seq.Add(currentPerp);

                useFirst = !useFirst;
            }

            return seq;
        }

        /// <summary>
        /// Snake형: 행 이동 → 커넥터(수직) → 반대 행 이동 반복
        /// 결과: → → → → ↑ ← ← ← ← ↑ → → → →
        /// </summary>
        private static List<ArrowDirection> GenerateSnake(
            ArrowDirection growDir, int steps, System.Random random)
        {
            var seq = new List<ArrowDirection>(steps);

            int rowLength = random.Next(2, 5) + 1; // 3~5
            var connectorDir = GetPerpendicularDirection(growDir, random.Next(2) == 0);
            var currentRowDir = growDir;

            while (seq.Count < steps)
            {
                // 행 이동
                for (int i = 0; i < rowLength && seq.Count < steps; i++)
                    seq.Add(currentRowDir);

                // 커넥터 (1셀 수직)
                if (seq.Count < steps)
                    seq.Add(connectorDir);

                // 다음 행 방향 반전
                currentRowDir = GetOppositeDirection(currentRowDir);
            }

            return seq;
        }

        /// <summary>
        /// 나선형: 시계방향으로 회전하며 안쪽으로 감기
        /// 2번 회전마다 sideLength -= 1
        /// 결과: → → → ↓ ↓ ↓ ← ← ↑ ↑ → ↓
        /// </summary>
        private static List<ArrowDirection> GenerateSpiral(ArrowDirection growDir, int steps)
        {
            var seq = new List<ArrowDirection>(steps);

            int sideLength = System.Math.Max(2, (int)System.Math.Ceiling(System.Math.Sqrt(steps)));
            int currentLength = sideLength;
            var currentDir = growDir;
            int turnCount = 0;

            while (seq.Count < steps)
            {
                for (int i = 0; i < currentLength && seq.Count < steps; i++)
                    seq.Add(currentDir);

                // 시계방향 90도 회전
                currentDir = RotateClockwise(currentDir);
                turnCount++;

                // 2번 회전마다 길이 감소
                if (turnCount % 2 == 0)
                    currentLength = System.Math.Max(1, currentLength - 1);
            }

            return seq;
        }

        /// <summary>
        /// Outline형: 직사각형 외곽을 따라 이동
        /// 결과: → → → ↓ ↓ ← ← ← ↑ ↑
        /// </summary>
        private static List<ArrowDirection> GenerateOutline(
            ArrowDirection growDir, int steps, System.Random random)
        {
            var seq = new List<ArrowDirection>(steps);

            // 둘레 크기 계산: steps에 맞춰 width/height 결정
            // 2*(w+h) = perimeter, perimeter >= steps
            int width = System.Math.Max(2, steps / 4 + 1);
            int height = System.Math.Max(2, (steps - 2 * width) / 2 + 1);

            // 자기교차 방지: 둘레가 steps보다 작으면 크기 조정
            int perimeter = 2 * (width + height) - 4; // 꼭짓점 중복 제거
            while (perimeter < steps && width < steps && height < steps)
            {
                if (width <= height)
                    width++;
                else
                    height++;
                perimeter = 2 * (width + height) - 4;
            }

            var dir1 = growDir;
            var dir2 = RotateClockwise(dir1);
            var dir3 = RotateClockwise(dir2);
            var dir4 = RotateClockwise(dir3);

            // 첫 번째 변 (width - 1 셀)
            for (int i = 0; i < width - 1 && seq.Count < steps; i++)
                seq.Add(dir1);
            // 두 번째 변 (height - 1 셀)
            for (int i = 0; i < height - 1 && seq.Count < steps; i++)
                seq.Add(dir2);
            // 세 번째 변 (width - 1 셀)
            for (int i = 0; i < width - 1 && seq.Count < steps; i++)
                seq.Add(dir3);
            // 네 번째 변 (height - 1 셀)
            for (int i = 0; i < height - 1 && seq.Count < steps; i++)
                seq.Add(dir4);

            return seq;
        }

        // ========== 방향 유틸리티 ==========

        private static ArrowDirection GetOppositeDirection(ArrowDirection dir)
        {
            return dir switch
            {
                ArrowDirection.Up    => ArrowDirection.Down,
                ArrowDirection.Down  => ArrowDirection.Up,
                ArrowDirection.Left  => ArrowDirection.Right,
                ArrowDirection.Right => ArrowDirection.Left,
                _ => dir
            };
        }

        /// <summary>
        /// 수직(perpendicular) 방향 반환
        /// </summary>
        /// <param name="dir">기준 방향</param>
        /// <param name="clockwise">true: 시계방향, false: 반시계방향</param>
        private static ArrowDirection GetPerpendicularDirection(ArrowDirection dir, bool clockwise)
        {
            return (dir, clockwise) switch
            {
                (ArrowDirection.Up,    true)  => ArrowDirection.Right,
                (ArrowDirection.Up,    false) => ArrowDirection.Left,
                (ArrowDirection.Down,  true)  => ArrowDirection.Left,
                (ArrowDirection.Down,  false) => ArrowDirection.Right,
                (ArrowDirection.Left,  true)  => ArrowDirection.Up,
                (ArrowDirection.Left,  false) => ArrowDirection.Down,
                (ArrowDirection.Right, true)  => ArrowDirection.Down,
                (ArrowDirection.Right, false) => ArrowDirection.Up,
                _ => dir
            };
        }

        private static ArrowDirection RotateClockwise(ArrowDirection dir)
        {
            return dir switch
            {
                ArrowDirection.Up    => ArrowDirection.Right,
                ArrowDirection.Right => ArrowDirection.Down,
                ArrowDirection.Down  => ArrowDirection.Left,
                ArrowDirection.Left  => ArrowDirection.Up,
                _ => dir
            };
        }

        private static GeometricPatternType[] GetAllPatterns()
        {
            return new[]
            {
                GeometricPatternType.Straight,
                GeometricPatternType.LShape,
                GeometricPatternType.UShape,
                GeometricPatternType.Zigzag,
                GeometricPatternType.Snake,
                GeometricPatternType.Spiral,
                GeometricPatternType.Outline,
            };
        }
    }
}
