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

    [Handler]
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
#pragma warning disable RS0030 // Do not use banned apis: Monitoring windows, cache headers, and activity timestamps require current wall-clock time
                var nowTicks = DateTime.UtcNow.Ticks;
#pragma warning restore RS0030

                if (Volatile.Read(ref lastCleanTicks) + cleanIntervalTicks < nowTicks)
                {
                    Interlocked.Exchange(ref lastCleanTicks, nowTicks);

                    registeredInstances.Clear();
                }

                return registeredInstances.TryAdd(id, id);
            }

            readonly ConcurrentDictionary<string, string> registeredInstances = new ConcurrentDictionary<string, string>();
#pragma warning disable RS0030 // Do not use banned apis: Monitoring windows, cache headers, and activity timestamps require current wall-clock time
            long lastCleanTicks = DateTime.UtcNow.Ticks;
#pragma warning restore RS0030
            static readonly long cleanIntervalTicks = TimeSpan.FromHours(1).Ticks;
        }
    }
}