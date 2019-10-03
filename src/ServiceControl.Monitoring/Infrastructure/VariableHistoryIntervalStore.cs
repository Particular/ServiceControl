namespace ServiceControl.Monitoring.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using Messaging;

    public class VariableHistoryIntervalStore<BreakdownT> : IProvideBreakdownBy<BreakdownT>
    {
        public VariableHistoryIntervalStore()
        {
            histories = new Dictionary<HistoryPeriod, IntervalsStore<BreakdownT>>();

            foreach (var period in HistoryPeriod.All)
            {
                histories.Add(period, new IntervalsStore<BreakdownT>(period.IntervalSize, period.NumberOfIntervals, period.DelayedIntervals));
            }
        }

        public IntervalsStore<BreakdownT>.IntervalsBreakdown[] GetIntervals(HistoryPeriod period, DateTime now)
        {
            IntervalsStore<BreakdownT> store;

            if (histories.TryGetValue(period, out store))
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

        Dictionary<HistoryPeriod, IntervalsStore<BreakdownT>> histories;
    }
}