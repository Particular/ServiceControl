namespace ServiceControl.Monitoring
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using NServiceBus;
    using Particular.LicensingComponent.Contracts;
    using Particular.LicensingComponent.Persistence;
    using ServiceControl.Plugin.Heartbeat.Messages;
    using ServiceControl.Transports.BrokerThroughput;
    using Endpoint = Particular.LicensingComponent.Contracts.Endpoint;

    class EndpointThroughputHandler(ILicensingDataStore dataStore, IBrokerThroughputQuery brokerThroughputQuery) : IHandleMessages<EndpointThroughput>
    {
        public async Task Handle(EndpointThroughput message, IMessageHandlerContext context)
        {
            var endpoint = await dataStore.GetEndpoint(message.EndpointName, ThroughputSource.Heartbeat, context.CancellationToken);
            if (endpoint == null)
            {
                endpoint = new Endpoint(message.EndpointName, ThroughputSource.Heartbeat)
                {
                    SanitizedName = brokerThroughputQuery != null ? brokerThroughputQuery.SanitizeEndpointName(message.EndpointName) : message.EndpointName,
                    EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()]
                };
                await dataStore.SaveEndpoint(endpoint, context.CancellationToken);
            }

            foreach (var throughput in message.Throughput)
            {
                Debug.WriteLine($"{throughput.Value} messages on {throughput.Key:yyyy-MM-dd} from {message.EndpointName}");
                var endpointThroughput = new EndpointDailyThroughput(DateOnly.FromDateTime(throughput.Key), throughput.Value);
                await dataStore.RecordEndpointThroughput(message.EndpointName, ThroughputSource.Heartbeat, [endpointThroughput], context.CancellationToken);
            }
        }
    }
}
