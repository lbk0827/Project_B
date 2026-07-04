using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Game.Editor.LevelEditor
{
    /// <summary>
    /// ReverseGrowthGenerator 성능/품질 벤치마크 (개발용).
    /// 여러 그리드 크기 × 시드로 생성하여 생성시간 / Solvable / FillRate 를 콘솔에 요약 출력.
    ///
    /// 목적: Level Editor 생성 알고리즘 개선(C안) 전후 비교의 기준선(baseline) 측정.
    /// 실행: 메뉴 "Arrow Pop/Benchmark/Run Generator Benchmark"
    /// </summary>
    public static class LevelGeneratorBenchmark
    {
        // 측정할 그리드 크기
        private static readonly (int w, int h)[] Sizes =
        {
            (10, 10),
            (20, 20),
            (30, 30),
        };

        private const int SeedsPerSize = 3;
        private const int MinLen = 2;
        private const int MaxLen = 8;
        private const int ColorCount = 5;
        private const int MaxFreeArrows = 0; // 난이도 제약 없음 → 생성기 자체 성능 측정에 집중

        [MenuItem("Arrow Pop/Benchmark/Run Generator Benchmark")]
        public static void RunBenchmark()
        {
            Debug.Log($"[Benchmark] ===== START ===== seeds/size={SeedsPerSize}, minLen={MinLen}, maxLen={MaxLen}, colors={ColorCount}, maxFreeArrows={MaxFreeArrows}");
            var sb = new StringBuilder();
            sb.AppendLine("===== ReverseGrowthGenerator Benchmark (summary) =====");
            sb.AppendLine("size    | avg_ms  | max_ms  | solvable | avg_arrows | avg_fill%");

            foreach (var (w, h) in Sizes)
            {
                double totalMs = 0;
                double maxMs = 0;
                int solvableCount = 0;
                int totalArrows = 0;
                double totalFill = 0;
                int successRuns = 0;

                for (int s = 0; s < SeedsPerSize; s++)
                {
                    int seed = 1000 + s; // 재현 가능한 고정 시드

                    var gen = new ReverseGrowthGenerator(seed);
                    gen.SetContext(w, h, null, false);
                    gen.SetParameters(MinLen, MaxLen, ColorCount);
                    gen.SetDifficultyParameters(MaxFreeArrows);
                    gen.SetGeometricPatterns(false, GeometricPatternType.All);
                    gen.SetColorMap(null, false);

                    var sw = Stopwatch.StartNew();
                    var (arrows, order) = gen.Generate();
                    sw.Stop();

                    double ms = sw.Elapsed.TotalMilliseconds;
                    totalMs += ms;
                    if (ms > maxMs) maxMs = ms;

                    if (arrows == null || arrows.Count == 0)
                    {
                        Debug.LogWarning($"[Benchmark] {w}x{h} seed={seed}: generation returned no arrows");
                        continue;
                    }

                    successRuns++;

                    var (solvable, _) = SolvabilityValidator.Validate(arrows, order, w, h);
                    if (solvable) solvableCount++;

                    int cellCount = 0;
                    foreach (var a in arrows) cellCount += a.cells.Count;

                    totalArrows += arrows.Count;
                    totalFill += (double)cellCount / (w * h) * 100.0;
                }

                double avgMs = totalMs / SeedsPerSize;
                double avgArrows = successRuns > 0 ? (double)totalArrows / successRuns : 0;
                double avgFill = successRuns > 0 ? totalFill / successRuns : 0;

                string line =
                    $"{w,2}x{h,-2}  | {avgMs,7:F1} | {maxMs,7:F1} | {solvableCount,3}/{SeedsPerSize}    | {avgArrows,8:F1}   | {avgFill,7:F1}";
                sb.AppendLine(line);

                // 크기별 즉시 로그 (전체가 타임아웃돼도 부분 결과 회수 가능)
                Debug.Log($"[Benchmark] DONE {line}");
            }

            sb.AppendLine("============================================");
            Debug.Log(sb.ToString());
        }
    }
}
