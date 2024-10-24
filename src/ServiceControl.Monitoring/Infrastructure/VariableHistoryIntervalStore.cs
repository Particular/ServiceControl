namespace ServiceControl.Monitoring.Infrastructure
{
    using System;
    using System.Collections.Concurrent;
    using Messaging;

    public class VariableHistoryIntervalStore<BreakdownT> : IProvideBreakdownBy<BreakdownT>
    {
        public VariableHistoryIntervalStore()
        {
            histories = [];

            foreach (var period in HistoryPeriod.All)
            {
                histories.TryAdd(period, new IntervalsStore<BreakdownT>(period.IntervalSize, period.NumberOfIntervals, period.DelayedIntervals));
            }
        }

        public IntervalsStore<BreakdownT>.IntervalsBreakdown[] GetIntervals(HistoryPeriod period, DateTime now)
        {
            if (histories.TryGetValue(period, out var store))
            {
                return store.GetIntervals(now);
            }

            throw new Exception("Unsupported history size.");
        }

        public void Store(BreakdownT id, RawMessage.Entry[] entries)
        {
            foreach (var kvHistory in histories)
            {
                kvHistory.Value.Store(id, entries);
            }
        }

        ConcurrentDictionary<HistoryPeriod, IntervalsStore<BreakdownT>> histories;
    }
}