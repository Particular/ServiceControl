namespace ServiceControl.Monitoring.Infrastructure
{
    using System;

    public interface IProvideBreakdown {  }

    public interface IProvideBreakdownBy<T> : IProvideBreakdown
    {
        IntervalsStore<T>.IntervalsBreakdown[] GetIntervals(HistoryPeriod period, DateTime now);
    }
}