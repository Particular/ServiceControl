#nullable enable
namespace ServiceControl.Transports.IBMMQ;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using IBM.WMQ;
using Microsoft.Extensions.Logging;

class QueueLengthProvider : AbstractQueueLengthProvider
{
    public QueueLengthProvider(TransportSettings settings, Action<QueueLengthEntry[], EndpointToQueueMapping> store, ILogger<QueueLengthProvider> logger)
        : base(settings, store)
    {
        this.logger = logger;
        (queueManagerName, connectionProperties) = ConnectionProperties.Parse(ConnectionString);
    }

    public override void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack) =>
        endpointQueues.AddOrUpdate(queueToTrack.EndpointName, _ => queueToTrack.InputQueue, (_, currentValue) =>
        {
            if (currentValue != queueToTrack.InputQueue)
            {
                sizes.TryRemove(currentValue, out var _);
            }

            return queueToTrack.InputQueue;
        });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                FetchQueueLengths();

                UpdateQueueLengthStore();

                await Task.Delay(QueryDelayInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // no-op
            }
            catch (Exception e)
            {
                logger.LogError(e, "Queue length query loop failure");
                CloseConnection();
                await Task.Delay(ReconnectDelayInterval, stoppingToken).ConfigureAwait(false);
            }
        }

        CloseConnection();
    }

    void UpdateQueueLengthStore()
    {
        var nowTicks = DateTime.UtcNow.Ticks;

        foreach (var endpointQueuePair in endpointQueues)
        {
            if (sizes.TryGetValue(endpointQueuePair.Value, out var size))
            {
                Store(
                    [
                        new QueueLengthEntry
                        {
                            DateTicks = nowTicks,
                            Value = size
                        }
                    ],
                    new EndpointToQueueMapping(endpointQueuePair.Key, endpointQueuePair.Value));
            }
        }
    }

    void FetchQueueLengths()
    {
        if (endpointQueues.IsEmpty)
        {
            return;
        }

        var manager = EnsureConnected();

        foreach (var endpointQueuePair in endpointQueues)
        {
            var queueName = endpointQueuePair.Value;
            try
            {
                using var queue = manager.AccessQueue(queueName, MQC.MQOO_INQUIRE | MQC.MQOO_FAIL_IF_QUIESCING);
                sizes[queueName] = queue.CurrentDepth;
            }
            catch (MQException e) when (IsConnectionError(e))
            {
                logger.LogWarning(e, "Lost connection to queue manager while querying {QueueName}", queueName);
                CloseConnection();
                throw;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Error querying queue length for {QueueName}", queueName);
            }
        }
    }

    MQQueueManager EnsureConnected()
    {
        if (queueManager is not null)
        {
            return queueManager;
        }

        queueManager = new MQQueueManager(queueManagerName, connectionProperties);
        logger.LogInformation("Connected to queue manager '{QueueManagerName}'", queueManagerName);
        return queueManager;
    }

    void CloseConnection()
    {
        if (queueManager is null)
        {
            return;
        }

        try
        {
            queueManager.Disconnect();
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Error disconnecting from queue manager");
        }

        queueManager = null;
    }

    static bool IsConnectionError(MQException e) => e.ReasonCode
            is MQC.MQRC_CONNECTION_BROKEN
            or MQC.MQRC_CONNECTION_ERROR
            or MQC.MQRC_CONNECTION_STOPPED
            or MQC.MQRC_CONNECTION_QUIESCING
            or MQC.MQRC_CONNECTION_NOT_AVAILABLE
            or MQC.MQRC_Q_MGR_NOT_AVAILABLE
            or MQC.MQRC_Q_MGR_NOT_ACTIVE;

    static readonly TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(200);
    static readonly TimeSpan ReconnectDelayInterval = TimeSpan.FromSeconds(10);

    MQQueueManager? queueManager;

    readonly ConcurrentDictionary<string, string> endpointQueues = new();
    readonly ConcurrentDictionary<string, int> sizes = new();

    readonly string queueManagerName;
    readonly Hashtable connectionProperties;
    readonly ILogger<QueueLengthProvider> logger;
}
