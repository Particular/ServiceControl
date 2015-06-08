namespace Particular.ServiceControl
{
    using Metrics;
    using NServiceBus;
    using NServiceBus.MessageMutator;

    public class ThroughputMeter : IMutateIncomingTransportMessages
    {
        Meter throughputMetric;

        public ThroughputMeter()
        {
            throughputMetric = Metric.Meter("Input queue", "messages");
        }

        public void MutateIncoming(TransportMessage transportMessage)
        {
            throughputMetric.Mark();
        }
    }
}