namespace ServiceControl.Monitoring
{
    using NServiceBus;
    using System.Threading.Tasks;
    using System.Threading;
    using System;
    using NServiceBus.Features;
    using NServiceBus.Transport;
    using ServiceControl.Infrastructure;
    using System.IO;
    using Newtonsoft.Json;
    using System.Diagnostics;
    using ServiceBus.Management.Infrastructure.Settings;

    class MonitoringDataFeature : Feature
    {
        public MonitoringDataFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            //https://docs.particular.net/nservicebus/satellites/
            context.Settings.TryGet<Settings>("ServiceControl.Settings", out var settings);
            var serviceControlMonitoringDataQueue = settings.ServiceControlMonitoringDataQueue;

            context.AddSatelliteReceiver(
                name: "ServiceControlMonitoringDataQueue",
                transportAddress: new QueueAddress(serviceControlMonitoringDataQueue),
                runtimeSettings: PushRuntimeSettings.Default,
                recoverabilityPolicy: (config, errorContext) =>
                {
                    return RecoverabilityAction.MoveToError(config.Failed.ErrorQueue);
                },
                onMessage: OnMessage);
        }

        Task OnMessage(IServiceProvider serviceProvider, MessageContext context, CancellationToken cancellationToken)
        {
            RecordEndpointMonitoringData message;
            using (var memoryStream = Memory.Manager.GetStream(context.NativeMessageId, context.Body.ToArray(), 0, context.Body.Length)) //TODO More ROM<byte> to array here
            using (var streamReader = new StreamReader(memoryStream))
            using (var reader = new JsonTextReader(streamReader))
            {
                message = endpointThroughputSerializer.Deserialize<RecordEndpointMonitoringData>(reader);
            }

            Debug.WriteLine($"Throughput data from {message.StartDateTime:yyyy-MM-dd HH:mm} to {message.EndDateTime:yyyy-MM-dd HH:mm} for {message.EndpointMonitoringData?.Length} endpoint(s)");

            return Task.CompletedTask;
        }

        readonly JsonSerializer endpointThroughputSerializer = new JsonSerializer();
    }
}