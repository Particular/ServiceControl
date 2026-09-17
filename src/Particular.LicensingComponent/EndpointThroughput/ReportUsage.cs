namespace NServiceBus;

using System.Text.Json.Serialization;
using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.Persistence;
using ServiceControl.Transports.BrokerThroughput;

public class EndpointUsageReport : IMessage
{
    public required string EndpointName { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
    public long MessagesSuccessfullyProcessed { get; set; }
}

[Handler]
class EndpointUsageReportHandler(
    ILicensingDataStore licensingDataStore,
    IBrokerThroughputQuery? brokerThroughputQuery = null
) : IHandleMessages<EndpointUsageReport>
{
    public async Task Handle(EndpointUsageReport message, IMessageHandlerContext context)
    {
        var endpointId = new EndpointIdentifier(message.EndpointName, ThroughputSource.Endpoint);

        var endpoint = await licensingDataStore.GetEndpoint(endpointId, context.CancellationToken);

        if (endpoint is null)
        {
            // TODO: Fill in more of the endpoint details if needed

            endpoint = new Particular.LicensingComponent.Contracts.Endpoint(endpointId)
            {
                EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()],
                SanitizedName = brokerThroughputQuery?.SanitizeEndpointName(endpointId.Name) ?? endpointId.Name
            };

            await licensingDataStore.SaveEndpoint(endpoint, context.CancellationToken);
        }

        await licensingDataStore.RecordEndpointThroughput(
            message.EndpointName,
            ThroughputSource.Endpoint,
            [new EndpointDailyThroughput(DateOnly.FromDateTime(message.TimeStamp.Date), message.MessagesSuccessfullyProcessed)],
            context.CancellationToken);
    }
}

[JsonSerializable(typeof(EndpointUsageReport))]
public partial class UsageReportingSerializationContext : JsonSerializerContext;
