namespace Particular.LicensingComponent.MonitoringThroughput;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Contracts;
using Persistence;
using ServiceControl.Transports.BrokerThroughput;
using Shared;

public class MonitoringService(
    ILicensingDataStore dataStore,
    ServiceControlSettings serviceControlSettings,
    IBrokerThroughputQuery? brokerThroughputQuery = null
)
{
    public async Task RecordMonitoringThroughput(byte[] throughputMessage, CancellationToken cancellationToken)
    {
        RecordEndpointThroughputData? message;
        using (Stream stream = new MemoryStream(throughputMessage))
        {
            message = await JsonSerializer.DeserializeAsync<RecordEndpointThroughputData>(stream, cancellationToken: cancellationToken);
        }

        if (message != null && message.EndpointThroughputData != null)
        {
            Debug.WriteLine($"Throughput data from {message.StartDateTime:yyyy-MM-dd HH:mm} to {message.EndDateTime:yyyy-MM-dd HH:mm} for {message.EndpointThroughputData?.Length} endpoint(s)");

            message.EndpointThroughputData?.ToList().ForEach(async e =>
            {
                var endpoint = await dataStore.GetEndpoint(e.Name, ThroughputSource.Monitoring, cancellationToken);
                if (endpoint == null)
                {
                    endpoint = new Endpoint(e.Name, ThroughputSource.Monitoring)
                    {
                        SanitizedName = brokerThroughputQuery != null ? brokerThroughputQuery.SanitizeEndpointName(e.Name) : e.Name,
                        EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()]
                    };
                    await dataStore.SaveEndpoint(endpoint, cancellationToken);
                }

                if (e.Throughput > 0)
                {
                    var endpointThroughput = new EndpointDailyThroughput(DateOnly.FromDateTime(message.EndDateTime), e.Throughput);

                    await dataStore.RecordEndpointThroughput(e.Name, ThroughputSource.Monitoring, [endpointThroughput], cancellationToken);
                }
            });
        }
    }

    public async Task<ConnectionSettingsTestResult> TestMonitoringConnection(CancellationToken cancellationToken)
    {
        //NOTE can't actually test the monitoring connection apart from seeing if there has been any throughput recorded from Monitoring

        var connectionTestResult = new ConnectionSettingsTestResult { ConnectionSuccessful = true };

        var diagnostics = new StringBuilder();
        if (await dataStore.IsThereThroughputForLastXDaysForSource(30, ThroughputSource.Monitoring, true, cancellationToken))
        {
            diagnostics.AppendLine("Throughput from Monitoring recorded in the last 30 days");
        }
        else
        {
            diagnostics.AppendLine("No throughput from Monitoring recorded in the last 30 days");
            connectionTestResult.ConnectionSuccessful = false;
        }
        diagnostics.AppendLine($"Listening on queue {serviceControlSettings.ServiceControlThroughputDataQueue}");

        connectionTestResult.Diagnostics = diagnostics.ToString();

        return connectionTestResult;
    }
}