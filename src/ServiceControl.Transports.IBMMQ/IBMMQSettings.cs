#nullable enable
namespace ServiceControl.Transports.IBMMQ;

static class IBMMQSettings
{
    public const string ConnectionString = "IBMMQ/ConnectionString";

    public const string ConnectionStringDescription =
        "URI-style IBM MQ connection string used by throughput collection. When omitted, the connection string configured for the ServiceControl Primary instance is used. Format: ibmmq://user:password@host:port/QM_NAME?channel=...";

    public const string StatisticsQueue = "IBMMQ/StatisticsQueue";

    public const string StatisticsQueueDescription =
        "Name of the queue from which IBM MQ statistics PCF messages are read. Defaults to SYSTEM.ADMIN.STATISTICS.QUEUE. Override when another consumer owns the system statistics queue and a forwarder or topic subscription delivers a per-consumer copy of stats messages to a dedicated queue.";

    public const string DefaultStatisticsQueue = "SYSTEM.ADMIN.STATISTICS.QUEUE";

    public const string StatisticsForwardingQueue = "IBMMQ/StatisticsForwardingQueue";

    public const string StatisticsForwardingQueueDescription =
        "Optional. When set, each statistics message read by ServiceControl is also re-published to this queue in the same transactional unit, allowing other tools to consume their own copy. Mirrors NServiceBus error/audit forwarding semantics. Leave unset for single-consumer setups.";
}