namespace ServiceControl.Transport
{
    public abstract class SqlServerTransportCustomization : TransportCustomization
    {
        protected sealed override string Transport { get; } = "NServiceBus.SqlServerTransport, NServiceBus.Transport.SqlServer";
    }
}