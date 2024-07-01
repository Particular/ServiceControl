namespace ServiceControl.Transports.MT.RabbitMQ;

using NServiceBus.Transport;

class RabbitMQAdapter : TransportDefinition
{
#pragma warning disable IDE0052
    readonly string connectionString;
#pragma warning restore IDE0052
    RabbitMQTransport transport;
    TransportInfrastructure infrastructure;

    public RabbitMQAdapter(string connectionString) : base(TransportTransactionMode.ReceiveOnly, false, false, false)
    {
        this.connectionString = connectionString;
    }

    public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers,
        string[] sendingAddresses,
        CancellationToken cancellationToken = new CancellationToken())
    {
        transport = new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), connectionString);

        infrastructure = await transport.Initialize(hostSettings, receivers, sendingAddresses, cancellationToken).ConfigureAwait(false);

        return infrastructure;
    }

    public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes() => new[]
    {
        TransportTransactionMode.ReceiveOnly
    };
}