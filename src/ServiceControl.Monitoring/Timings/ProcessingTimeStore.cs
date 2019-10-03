namespace ServiceControl.Monitoring.Timings
{
    using System;
    using Infrastructure;
    using Messaging;

    public class ProcessingTimeStore : IProvideBreakdownBy<EndpointInstanceId>, IProvideBreakdownBy<EndpointMessageType>
    {
        IntervalsStore<EndpointInstanceId>.IntervalsBreakdown[] IProvideBreakdownBy<EndpointInstanceId>.GetIntervals(HistoryPeriod period, DateTime now)
        {
            return byInstance.GetIntervals(period, now);
        }

        IntervalsStore<EndpointMessageType>.IntervalsBreakdown[] IProvideBreakdownBy<EndpointMessageType>.GetIntervals(HistoryPeriod period, DateTime now)
        {
            return byMessageType.GetIntervals(period, now);
        }

        public void Store(RawMessage.Entry[] entries, EndpointInstanceId instanceId, EndpointMessageType messageType)
        {
            byInstance.Store(instanceId, entries);
            byMessageType.Store(messageType, entries);
        }

        VariableHistoryIntervalStore<EndpointInstanceId> byInstance = new VariableHistoryIntervalStore<EndpointInstanceId>();
        VariableHistoryIntervalStore<EndpointMessageType> byMessageType = new VariableHistoryIntervalStore<EndpointMessageType>();
    }
}