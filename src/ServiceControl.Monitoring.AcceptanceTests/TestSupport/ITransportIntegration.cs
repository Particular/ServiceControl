namespace ServiceBus.Management.AcceptanceTests
{
    using System.Threading.Tasks;
    using NServiceBus;

    public interface ITransportIntegration
    {
        string MonitoringSeamTypeName { get; }
        string ConnectionString { get; set; }

        Task Configure(string endpointName, EndpointConfiguration configuration);

        Task Cleanup();
    }
}