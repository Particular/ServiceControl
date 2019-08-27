namespace ServiceControl.Monitoring.Http.Diagrams
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Infrastructure;

    public static class Aggregator
    {
        internal static MonitoredValues ToAverages<T>(List<IntervalsStore<T>.IntervalsBreakdown> intervals, HistoryPeriod period)
        {
            Func<long, double> returnOneIfZero = x => x == 0 ? 1 : x;

            return new MonitoredValues
            {
                Average = intervals.Sum(t => t.TotalValue) / returnOneIfZero(intervals.Sum(t => t.TotalMeasurements)),
                Points = intervals.SelectMany(t => t.Intervals)
                    .GroupBy(i => i.IntervalStart)
                    .OrderBy(g => g.Key)
                    .Select(g => g.Sum(i => i.TotalValue) / returnOneIfZero(g.Sum(i => i.TotalMeasurements)))
                    .ToArray()
            };
        }

        internal static MonitoredValues ToRoundedSumOfBreakdownAverages<T>(List<IntervalsStore<T>.IntervalsBreakdown> intervals, HistoryPeriod period)
        {
            Func<long, double> returnOneIfZero = x => x == 0 ? 1 : x;

            return new MonitoredValues
            {
                Average = Math.Round(intervals.Sum(t => t.TotalValue / returnOneIfZero(t.TotalMeasurements))),
                Points = intervals.SelectMany(x => x.Intervals)
                    .GroupBy(i => i.IntervalStart)
                    .OrderBy(g => g.Key)
                    .Select(gg => Math.Round(gg.Sum(t => t.TotalValue / returnOneIfZero(t.TotalMeasurements))))
                    .ToArray()
            };
        }

        public static MonitoredValues ToTotalMeasurementsPerSecond<T>(List<IntervalsStore<T>.IntervalsBreakdown> intervals, HistoryPeriod period)
        {
            Func<long, double> returnOneIfZero = x => x == 0 ? 1 : x;

            var seconds = period.IntervalSize.TotalSeconds;

            var uniqueIntervals = intervals.SelectMany(t => t.Intervals).GroupBy(i => i.IntervalStart).ToList();

            return new MonitoredValues
            {
                Average = uniqueIntervals.Sum(ig => ig.Sum(i => i.TotalMeasurements)) / returnOneIfZero(uniqueIntervals.Count) / seconds,
                Points = uniqueIntervals
                    .OrderBy(g => g.Key)
                    .Select(g => g.Sum(i => i.TotalMeasurements) / seconds)
                    .ToArray()
            };
        }
    }
}