namespace ServiceControl.Monitoring.QueueLength
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Metrics;

    class LegacyQueueLengthReportHandler(LegacyQueueLengthReportHandler.LegacyQueueLengthEndpoints legacyEndpoints, ILogger<LegacyQueueLengthReportHandler> logger) : IHandleMessages<MetricReport>
    {
        public Task Handle(MetricReport message, IMessageHandlerContext context)
        {
            var endpointInstanceId = EndpointInstanceId.From(context.MessageHeaders);

            if (legacyEndpoints.TryAdd(endpointInstanceId.InstanceId))
            {
                logger.LogWarning("Legacy queue length report received from {EndpointInstanceIdInstanceName} instance of {EndpointInstanceIdEndpointName}", endpointInstanceId.InstanceName, endpointInstanceId.EndpointName);
            }

            return Task.CompletedTask;
        }

        public class LegacyQueueLengthEndpoints
        {
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

            ConcurrentDictionary<string, string> registeredInstances = new ConcurrentDictionary<string, string>();
            long lastCleanTicks = DateTime.UtcNow.Ticks;
            static long cleanIntervalTicks = TimeSpan.FromHours(1).Ticks;
        }
    }
}