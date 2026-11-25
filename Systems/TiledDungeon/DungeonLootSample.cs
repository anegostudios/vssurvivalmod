using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Vintagestory.ServerMods;

public class DungeonLootSample
{
    public int Runs;
    public float Sum;
    public float Average;
    public float Min;
    public float Max;
    public float Median;
    public float StdDev;
    public float Cv; // StdDev / Average
    public float P10, P25, P75, P90, P99;
    public float DropRate; // fraction of runs where the item dropped at all
    public float AvgWhenDropped; // average count, ignoring runs with 0
    public float Outliers;
    public float OutlierPct;
    public int ZeroRuns; // number of runs where the item did not drop
    public float ZeroRate; // ZeroRuns / Runs

    public static OrderedDictionary<string, DungeonLootSample> BuildStats(
        List<Dictionary<string, int>> odds)
    {
        int runs = odds.Count;

        var drops = new Dictionary<string, List<int>>();
        foreach (var run in odds)
        {
            foreach (var (code, count) in run)
            {
                if (!drops.TryGetValue(code, out var list))
                    drops[code] = list = new List<int>();
                list.Add(count);
            }
        }

        var finalOdds = new OrderedDictionary<string, DungeonLootSample>();

        foreach (var (code, nonZero) in drops)
        {
            // fill missing values at the start with zeros and sort the rest
            nonZero.Sort();
            var values = new float[runs];
            int offset = runs - nonZero.Count;
            for (int i = 0; i < nonZero.Count; i++)
                values[offset + i] = nonZero[i];

            float sum = values.Sum();
            float avg = values.Average();

            float variance = (float)values.Select(x => Math.Pow(x - avg, 2)).Average();
            float stdDev = MathF.Sqrt(variance / runs);

            float q1 = Percentile(values, 0.25f);
            float q3 = Percentile(values, 0.75f);
            float upperFence = q3 + 1.5f * (q3 - q1);

            float max = values[^1];
            int outliers = 0;
            foreach (var v in values)
            {
                if (v > upperFence) outliers++;
            }

            finalOdds[code] = new DungeonLootSample
            {
                Runs = runs,
                Sum = sum,
                Average = avg,
                Min = values[0],
                Max = max,
                Median = Percentile(values, 0.50f),
                StdDev = stdDev,
                Cv = avg > 0 ? stdDev / avg : 0,
                P10 = Percentile(values, 0.10f),
                P25 = q1,
                P75 = q3,
                P90 = Percentile(values, 0.90f),
                P99 = Percentile(values, 0.99f),
                DropRate = (float)nonZero.Count / runs,
                AvgWhenDropped = (float)nonZero.Sum() / nonZero.Count,
                Outliers = outliers,
                OutlierPct = (float)outliers / runs,
                ZeroRuns = runs - nonZero.Count,
                ZeroRate = (float)(runs - nonZero.Count) / runs,
            };
        }

        return finalOdds;
    }

    /// Linear-interpolated percentile on an ascending-sorted array
    private static float Percentile(float[] sorted, float p)
    {
        if (sorted.Length == 1) return sorted[0];
        float pos = p * (sorted.Length - 1);
        int lo = (int)MathF.Floor(pos);
        int hi = (int)MathF.Ceiling(pos);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
    }


    public static void GenerateReport(OrderedDictionary<string, DungeonLootSample> stats, StringBuilder sb)
    {
        var ci = CultureInfo.InvariantCulture;
        sb.AppendLine("item,sum,avg,avg_when_dropped,min,max,median,stddev,cv,p10,p25,p75,p90,p99," +
                      "drop_rate,zero_runs,zero_rate,drop_runs,outliers,outlier_pct");
        foreach (var (stackCode, s) in stats)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                stackCode,
                s.Sum.ToString("0.##", ci), s.Average.ToString("0.###", ci), s.AvgWhenDropped.ToString("0.###", ci),
                s.Min.ToString("0.##", ci), s.Max.ToString("0.##", ci),
                s.Median.ToString("0.##", ci), s.StdDev.ToString("0.###", ci), s.Cv.ToString("0.###", ci),
                s.P10.ToString("0.##", ci), s.P25.ToString("0.##", ci),
                s.P75.ToString("0.##", ci), s.P90.ToString("0.##", ci), s.P99.ToString("0.##", ci),
                s.DropRate.ToString("0.####", ci), s.ZeroRuns.ToString(ci), s.ZeroRate.ToString("0.####", ci),
                (s.Runs - s.ZeroRuns).ToString(ci),
                s.Outliers.ToString("0.##", ci), s.OutlierPct.ToString("0.####", ci)
            }));
        }
    }
}
