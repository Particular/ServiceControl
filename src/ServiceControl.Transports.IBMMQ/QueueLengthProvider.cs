namespace ServiceControl.Transports.IBMMQ;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using IBM.WMQ;
using Microsoft.Extensions.Logging;

class QueueLengthProvider : AbstractQueueLengthProvider
{
    public QueueLengthProvider(TransportSettings settings, Action<QueueLengthEntry[], EndpointToQueueMapping> store, ILogger<QueueLengthProvider> logger)
        : base(settings, store)
    {
        this.logger = logger;

        var connectionUri = new Uri(ConnectionString);
        var query = HttpUtility.ParseQueryString(connectionUri.Query);

        queueManagerName = connectionUri.AbsolutePath.Trim('/') is { Length: > 0 } path
            ? Uri.UnescapeDataString(path)
            : "QM1";

        connectionProperties = new Hashtable
        {
            [MQC.TRANSPORT_PROPERTY] = MQC.TRANSPORT_MQSERIES_MANAGED,
            [MQC.HOST_NAME_PROPERTY] = connectionUri.Host,
            [MQC.PORT_PROPERTY] = connectionUri.Port > 0 ? connectionUri.Port : 1414,
            [MQC.CHANNEL_PROPERTY] = query["channel"] ?? "DEV.ADMIN.SVRCONN"
        };

        var userInfo = connectionUri.UserInfo;
        if (!string.IsNullOrEmpty(userInfo))
        {
            var parts = userInfo.Split(':');
            var user = Uri.UnescapeDataString(parts[0]);

            if (!string.IsNullOrWhiteSpace(user))
            {
                connectionProperties[MQC.USE_MQCSP_AUTHENTICATION_PROPERTY] = true;
                connectionProperties[MQC.USER_ID_PROPERTY] = user;
            }

            if (parts.Length > 1)
            {
                var password = Uri.UnescapeDataString(parts[1]);
                if (!string.IsNullOrWhiteSpace(password))
                {
                    connectionProperties[MQC.PASSWORD_PROPERTY] = password;
                }
            }
        }

        if (query["sslkeyrepo"] is { } sslKeyRepo)
        {
            connectionProperties[MQC.SSL_CERT_STORE_PROPERTY] = sslKeyRepo;
        }

        if (query["cipherspec"] is { } cipherSpec)
        {
            connectionProperties[MQC.SSL_CIPHER_SPEC_PROPERTY] = cipherSpec;
        }

        if (query["sslpeername"] is { } sslPeerName)
        {
            connectionProperties[MQC.SSL_PEER_NAME_PROPERTY] = sslPeerName;
        }
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
            }
        }
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

        using var queueManager = new MQQueueManager(queueManagerName, connectionProperties);

        foreach (var endpointQueuePair in endpointQueues)
        {
            var queueName = endpointQueuePair.Value;
            try
            {
                using var queue = queueManager.AccessQueue(queueName, MQC.MQOO_INQUIRE | MQC.MQOO_FAIL_IF_QUIESCING);
                sizes[queueName] = queue.CurrentDepth;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Error querying queue length for {QueueName}", queueName);
            }
        }
    }

    static readonly TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(200);

    readonly ConcurrentDictionary<string, string> endpointQueues = new();
    readonly ConcurrentDictionary<string, int> sizes = new();

    readonly string queueManagerName;
    readonly Hashtable connectionProperties;
    readonly ILogger<QueueLengthProvider> logger;
}
