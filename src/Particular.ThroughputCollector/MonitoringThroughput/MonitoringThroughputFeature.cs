namespace Particular.ThroughputCollector.MonitoringThroughput;

using NServiceBus;
using System.Threading.Tasks;
using System.Threading;
using System;
using NServiceBus.Features;
using NServiceBus.Transport;
using Microsoft.Extensions.Logging;
using Particular.ThroughputCollector.Shared;
using Microsoft.Extensions.DependencyInjection;

class MonitoringThroughputFeature : Feature
{
    public MonitoringThroughputFeature()
    {
        EnableByDefault();
    }

    protected override void Setup(FeatureConfigurationContext context)
    {
        //https://docs.particular.net/nservicebus/satellites/
        var serviceControlThroughputDataQueue = ServiceControlSettings.ServiceControlThroughputDataQueue;

        context.AddSatelliteReceiver(
            name: ServiceControlSettings.ServiceControlThroughputDataQueueSetting,
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
        var monitoringService = serviceProvider.GetRequiredService<MonitoringService>();

        try
        {
            await monitoringService.RecordMonitoringThroughput(context.Body.ToArray(), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error receiving throughput data from Monitoring");
        }
    }
}