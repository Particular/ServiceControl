namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using Microsoft.Extensions.Logging;

    class QueueLengthProvider : AbstractQueueLengthProvider
    {
        public QueueLengthProvider(TransportSettings settings, Action<QueueLengthEntry[], EndpointToQueueMapping> store, ILogger<QueueLengthProvider> logger) : base(settings, store)
        {
            var connectionSettings = ConnectionStringParser.Parse(ConnectionString);

            queryDelayInterval = connectionSettings.QueryDelayInterval ?? TimeSpan.FromMilliseconds(500);

            // PerRetry so the detector sees the 429s the SDK retries away (they never surface as exceptions).
            var clientOptions = new ServiceBusAdministrationClientOptions();
            clientOptions.AddPolicy(throttleDetector, HttpPipelinePosition.PerRetry);
            managementClient = connectionSettings.AuthenticationMethod.BuildManagementClient(clientOptions);
            this.logger = logger;
        }

        public override void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack) =>
            endpointQueueMappings.AddOrUpdate(
                queueToTrack.InputQueue,
                id => queueToTrack.EndpointName,
                (id, old) => queueToTrack.EndpointName
            );

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // currentDelay backs off while throttled and recovers to the base once clear; throttled latches the log.
            var currentDelay = queryDelayInterval;
            var throttled = false;

            while (!stoppingToken.IsCancellationRequested)
            {
                bool throttledThisCycle;

                try
                {
                    logger.LogDebug("Waiting for next interval");
                    await Task.Delay(currentDelay, stoppingToken);

                    logger.LogDebug("Querying management client");

                    // A query can succeed yet still have been throttled (429s retried away), so detect via the counter.
                    var throttledResponsesBefore = throttleDetector.ThrottledResponseCount;

                    var queueRuntimeInfos = await GetQueueList(stoppingToken);

                    logger.LogDebug("Retrieved details of {QueueCount} queues", queueRuntimeInfos.Count);

                    UpdateAllQueueLengths(queueRuntimeInfos);

                    throttledThisCycle = throttleDetector.ThrottledResponseCount > throttledResponsesBefore;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.ServiceBusy)
                {
                    // Fallback for when the SDK's retries were exhausted and it threw.
                    throttledThisCycle = true;
                }
                catch (Exception e)
                {
                    // Unrelated error: log and keep the current cadence.
                    logger.LogError(e, "Error querying Azure Service Bus queue sizes");
                    continue;
                }

                // Store nothing on a throttled cycle; the last values stand. Ideally we'd record an explicit
                // "no value" (null/-1) so the graph shows a gap, but Value is a non-nullable long and the
                // API/ServicePulse coerce gaps to 0 - that needs a new API contract + a ServicePulse using it.
                currentDelay = NextDelay(currentDelay, queryDelayInterval, MaxBackoffInterval, throttledThisCycle);

                if (throttledThisCycle)
                {
                    if (!throttled)
                    {
                        throttled = true;
                        logger.LogWarning("Azure Service Bus is throttling the management operations used to read queue lengths. Backing off to a {CurrentDelay} query interval. This is expected on busy or large namespaces; increase the 'QueueLengthQueryDelayInterval' connection string setting to reduce it. Queue length metrics may be delayed until the throttling clears.", currentDelay);
                    }
                    else
                    {
                        logger.LogDebug("Still throttled by Azure Service Bus; backing off query interval to {CurrentDelay}", currentDelay);
                    }
                }
                else if (throttled && currentDelay == queryDelayInterval)
                {
                    // Recover only once fully back at the base, so a slow ramp-down doesn't re-arm the warning.
                    throttled = false;
                    logger.LogInformation("Azure Service Bus management throttling has cleared; resumed querying queue lengths every {QueryDelayInterval}", queryDelayInterval);
                }
            }
        }

        // Pure back-off policy (unit-tested). Throttled -> exponential up to the cap; success -> halve back toward
        // the base so it settles near the sustainable rate instead of oscillating.
        internal static TimeSpan NextDelay(TimeSpan current, TimeSpan baseDelay, TimeSpan maxDelay, bool throttled)
        {
            if (throttled)
            {
                var doubled = TimeSpan.FromTicks(current.Ticks * 2);
                return doubled > maxDelay ? maxDelay : doubled;
            }

            var halved = TimeSpan.FromTicks(current.Ticks / 2);
            return halved < baseDelay ? baseDelay : halved;
        }

        async Task<IReadOnlyDictionary<string, QueueRuntimeProperties>> GetQueueList(CancellationToken cancellationToken)
        {
            var queuePathToRuntimeInfo = new Dictionary<string, QueueRuntimeProperties>(StringComparer.InvariantCultureIgnoreCase);

            var queuesRuntimeProperties = managementClient.GetQueuesRuntimePropertiesAsync(cancellationToken);
            var enumerator = queuesRuntimeProperties.GetAsyncEnumerator(cancellationToken);
            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    var queueRuntimeProperties = enumerator.Current;
                    queuePathToRuntimeInfo[queueRuntimeProperties.Name] = queueRuntimeProperties; // Assuming last write is most up to date
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            return queuePathToRuntimeInfo;
        }

        void UpdateAllQueueLengths(IReadOnlyDictionary<string, QueueRuntimeProperties> queues)
        {
            foreach (var eq in endpointQueueMappings)
            {
                UpdateQueueLength(eq, queues);
            }
        }

        void UpdateQueueLength(KeyValuePair<string, string> monitoredEndpoint, IReadOnlyDictionary<string, QueueRuntimeProperties> queues)
        {
            var endpointName = monitoredEndpoint.Value;
            var queueName = monitoredEndpoint.Key;

            if (!queues.TryGetValue(queueName, out var runtimeInfo))
            {
                return;
            }

            var entries = new[]
            {
                new QueueLengthEntry
                {
                    DateTicks =  DateTime.UtcNow.Ticks,
                    Value = runtimeInfo.ActiveMessageCount
                }
            };

            Store(entries, new EndpointToQueueMapping(endpointName, queueName));
        }

        readonly ConcurrentDictionary<string, string> endpointQueueMappings = new ConcurrentDictionary<string, string>();
        readonly ServiceBusAdministrationClient managementClient;
        readonly ManagementThrottleDetector throttleDetector = new();
        readonly TimeSpan queryDelayInterval;
        readonly ILogger<QueueLengthProvider> logger;

        // Upper bound for the reactive back-off when Azure Service Bus is throttling management operations.
        static readonly TimeSpan MaxBackoffInterval = TimeSpan.FromMinutes(1);
    }
}