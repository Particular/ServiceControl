namespace Particular.ThroughputCollector.MonitoringThroughput;

using Particular.ThroughputCollector.Contracts;
using System.Diagnostics;
using System.Threading;
using Particular.ThroughputCollector.Persistence;
using Particular.ThroughputCollector.Shared;
using System.Text.Json;

public class MonitoringService(IThroughputDataStore dataStore)
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
                        SanitizedName = e.Name,
                        EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()],
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

        var connectionTestResult = new ConnectionSettingsTestResult
        {
            ConnectionSuccessful = await dataStore.IsThereThroughputForLastXDaysForSource(30, ThroughputSource.Monitoring)
        };

        if (!connectionTestResult.ConnectionSuccessful)
        {
            connectionTestResult.ConnectionErrorMessages.Add($"No throughput from Monitoring recorded in the last 30 days. Listening on queue {PlatformEndpointHelper.ServiceControlThroughputDataQueue}");
        }

        return connectionTestResult;
    }
}
