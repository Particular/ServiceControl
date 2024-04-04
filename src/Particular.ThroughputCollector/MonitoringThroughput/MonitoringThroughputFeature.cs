namespace Particular.ThroughputCollector.MonitoringThroughput;

using NServiceBus;
using System.Threading.Tasks;
using System.Threading;
using System;
using NServiceBus.Features;
using NServiceBus.Transport;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Particular.ThroughputCollector.Persistence;
using ServiceControl.Configuration;
using System.Text.Json;
using Particular.ThroughputCollector.Contracts;
using Endpoint = Contracts.Endpoint;
using Particular.ThroughputCollector.Shared;
using Microsoft.Extensions.DependencyInjection;

class MonitoringThroughputFeature : Feature
{
    MonitoringThroughputFeature()
    {
        EnableByDefault();
    }

    protected override void Setup(FeatureConfigurationContext context)
    {
        //https://docs.particular.net/nservicebus/satellites/
        var serviceControlThroughputDataQueue = SettingsReader.Read(new SettingsRootNamespace(SettingsNamespace), "ServiceControlThroughputDataQueue", "ServiceControl.ThroughputData");

        context.AddSatelliteReceiver(
            name: "ServiceControlThroughputDataQueue",
            transportAddress: new QueueAddress(serviceControlThroughputDataQueue),
            runtimeSettings: PushRuntimeSettings.Default,
            recoverabilityPolicy: (config, errorContext) =>
            {
                return RecoverabilityAction.MoveToError(config.Failed.ErrorQueue);
            },
            onMessage: OnMessage);
    }

    async Task OnMessage(IServiceProvider serviceProvider, MessageContext context, CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<MonitoringThroughputFeature>>();

        try
        {
            RecordEndpointThroughputData? message;
            using (Stream stream = new MemoryStream(context.Body.ToArray()))
            {
                message = await JsonSerializer.DeserializeAsync<RecordEndpointThroughputData>(stream, cancellationToken: cancellationToken);
            }

            if (message != null && message.EndpointThroughputData != null)
            {
                Debug.WriteLine($"Throughput data from {message.StartDateTime:yyyy-MM-dd HH:mm} to {message.EndDateTime:yyyy-MM-dd HH:mm} for {message.EndpointThroughputData?.Length} endpoint(s)");

                message.EndpointThroughputData?.ToList().ForEach(async e =>
                {
                    var dataStore = serviceProvider.GetRequiredService<IThroughputDataStore>();
                    var endpoint = await dataStore.GetEndpoint(e.Name, ThroughputSource.Monitoring, cancellationToken);
                    if (endpoint == null)
                    {
                        endpoint = new Endpoint(e.Name, ThroughputSource.Monitoring)
                        {
                            SanitizedName = EndpointNameSanitizer.SanitizeEndpointName(e.Name, serviceProvider.GetRequiredService<ThroughputSettings>().Broker),
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
        catch (Exception ex)
        {
            logger.LogError(ex, "Error receiving throughput data from Monitoring");
        }
    }

    static readonly string SettingsNamespace = "ThroughputCollector";
}