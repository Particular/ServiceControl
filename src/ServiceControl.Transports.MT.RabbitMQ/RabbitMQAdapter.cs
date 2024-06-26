namespace ServiceControl.Transports.MT.RabbitMQ;

using NServiceBus.Transport;

class RabbitMQAdapter : TransportDefinition
{
#pragma warning disable IDE0052
    readonly string connectionString;
#pragma warning restore IDE0052

    public RabbitMQAdapter(string connectionString) : base(TransportTransactionMode.ReceiveOnly, false, false, false)
    {
        this.connectionString = connectionString;
    }

    public override Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses,
        CancellationToken cancellationToken = new CancellationToken()) =>
        throw new NotImplementedException();

    public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes() => new[]
    {
        TransportTransactionMode.ReceiveOnly
    };
}