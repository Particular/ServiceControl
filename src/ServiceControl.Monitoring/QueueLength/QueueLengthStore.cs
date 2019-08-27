namespace ServiceControl.Monitoring.QueueLength
{
    using System;
    using Infrastructure;
    using Messaging;

    public class QueueLengthStore : IProvideBreakdownBy<EndpointInputQueue>
    {
        VariableHistoryIntervalStore<EndpointInputQueue> byInputQueue = new VariableHistoryIntervalStore<EndpointInputQueue>();

        public IntervalsStore<EndpointInputQueue>.IntervalsBreakdown[] GetIntervals(HistoryPeriod period, DateTime now)
        {
            return byInputQueue.GetIntervals(period, now);
        }

        public void Store(RawMessage.Entry[] entries, EndpointInputQueue endpointInputQueue)
        {
            byInputQueue.Store(endpointInputQueue, entries);
        }
    }
}