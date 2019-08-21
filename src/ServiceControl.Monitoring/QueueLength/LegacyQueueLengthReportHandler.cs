namespace ServiceControl.Monitoring.QueueLength
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Metrics;

    class LegacyQueueLengthReportHandler : IHandleMessages<MetricReport>
    {
        LegacyQueueLengthEndpoints legacyEndpoints;

        public LegacyQueueLengthReportHandler(LegacyQueueLengthEndpoints legacyEndpoints)
        {
            this.legacyEndpoints = legacyEndpoints;
        }

        public Task Handle(MetricReport message, IMessageHandlerContext context)
        {
            var endpointInstanceId = EndpointInstanceId.From(context.MessageHeaders);

            if (legacyEndpoints.TryAdd(endpointInstanceId.InstanceId))
            {
                Logger.Warn($"Legacy queue length report received from {endpointInstanceId.InstanceName} instance of {endpointInstanceId.EndpointName}");
            }

            return TaskEx.Completed;
        }

        public class LegacyQueueLengthEndpoints
        {
            static long cleanIntervalTicks = TimeSpan.FromHours(1).Ticks;

            ConcurrentDictionary<string, string> registeredInstances = new ConcurrentDictionary<string, string>();
            long lastCleanTicks = DateTime.UtcNow.Ticks;

            public bool TryAdd(string id)
            {
                var nowTicks = DateTime.UtcNow.Ticks;

                if (Volatile.Read(ref lastCleanTicks) + cleanIntervalTicks < nowTicks)
                {
                    Interlocked.Exchange(ref lastCleanTicks, nowTicks);
                    
                    registeredInstances.Clear();
                }

                return registeredInstances.TryAdd(id, id);
            }
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(LegacyQueueLengthReportHandler));
    }
}