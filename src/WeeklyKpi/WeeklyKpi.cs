using System;
using System.Collections.Generic;
using System.Linq;

public record Shift(int Id, string Location, DateTime Start, DateTime End, bool Removed);
public record Coverage(int Id, string Location, DateTime Start, DateTime End);

public static class WeeklyKpi
{
    public static (double scheduled, double covered, double dark) Calculate(
        string location, DateTime from, DateTime to,
        IEnumerable<Shift> shifts, IEnumerable<Coverage> coverage)
    {
        // Rule 2: removed shifts are excluded from scheduled hours but stay in the input for audit.
        // Rule 3 + Rule 6: clip each active shift to [from,to) and merge overlaps instead of summing.
        var scheduled = IntervalMath.Merge(
            shifts.Where(s => s.Location == location && !s.Removed)
                  .Select(s => IntervalMath.Clip(s.Start, s.End, from, to))
                  .Where(i => i.HasValue)
                  .Select(i => i!.Value));

        // Rule 4: coverage is clipped to the window, merged, then clipped to scheduled so duplicates count once.
        var covered = IntervalMath.Merge(IntervalMath.Intersect(
            IntervalMath.Merge(
                coverage.Where(c => c.Location == location)
                        .Select(c => IntervalMath.Clip(c.Start, c.End, from, to))
                        .Where(i => i.HasValue)
                        .Select(i => i!.Value)),
            scheduled));

        // Rule 7: whole minutes as long, converted to hours only here; 2-decimal display is the caller's job.
        long scheduledMin = IntervalMath.TotalMinutes(scheduled);
        long coveredMin = IntervalMath.TotalMinutes(covered);

        // Rule 5: dark = scheduled - covered, floored at zero.
        long darkMin = Math.Max(0, scheduledMin - coveredMin);

        return (scheduledMin / 60.0, coveredMin / 60.0, darkMin / 60.0);
    }
}

internal static class IntervalMath
{
    // Rule 1 + Rule 6: [start,end) ∩ [from,to); null when empty, zero-length or inverted.
    public static (DateTime Start, DateTime End)? Clip(DateTime start, DateTime end, DateTime from, DateTime to)
    {
        var s = start > from ? start : from;
        var e = end < to ? end : to;
        return s < e ? (s, e) : null;
    }

    // Rule 1 + Rule 3: sort by Start and sweep; only a strict overlap (next.Start < current.End) merges, touching stays adjacent.
    public static List<(DateTime Start, DateTime End)> Merge(IEnumerable<(DateTime Start, DateTime End)> intervals)
    {
        var result = new List<(DateTime Start, DateTime End)>();
        foreach (var iv in intervals.Where(i => i.Start < i.End).OrderBy(i => i.Start))
        {
            if (result.Count > 0 && iv.Start < result[^1].End)
            {
                if (iv.End > result[^1].End)
                    result[^1] = (result[^1].Start, iv.End);
            }
            else
            {
                result.Add(iv);
            }
        }
        return result;
    }

    // Rule 4: two-pointer sweep over two merged lists; one interval may yield several pieces.
    public static List<(DateTime Start, DateTime End)> Intersect(
        List<(DateTime Start, DateTime End)> mergedA, List<(DateTime Start, DateTime End)> mergedB)
    {
        var result = new List<(DateTime Start, DateTime End)>();
        int a = 0, b = 0;
        while (a < mergedA.Count && b < mergedB.Count)
        {
            var s = mergedA[a].Start > mergedB[b].Start ? mergedA[a].Start : mergedB[b].Start;
            var e = mergedA[a].End < mergedB[b].End ? mergedA[a].End : mergedB[b].End;
            if (s < e) result.Add((s, e));
            if (mergedA[a].End < mergedB[b].End) a++; else b++;
        }
        return result;
    }

    // Rule 7: whole minutes accumulated as long; sub-minute ticks truncate.
    public static long TotalMinutes(List<(DateTime Start, DateTime End)> merged)
    {
        long ticks = 0;
        foreach (var iv in merged) ticks += (iv.End - iv.Start).Ticks;
        return ticks / TimeSpan.TicksPerMinute;
    }
}
