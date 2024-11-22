namespace ServiceControl.Monitoring.Infrastructure.Api
{
    using ServiceControl.Monitoring.Http.Diagrams;

    public interface IEndpointMetricsApi
    {
        MonitoredEndpoint[] GetAllEndpointsMetrics(int? history = null);

        MonitoredEndpointDetails GetSingleEndpointMetrics(string endpointName, int? history = null);

        void DeleteEndpointInstance(string endpointName, string instanceId);

        int DisconnectedEndpointCount();
    }
}