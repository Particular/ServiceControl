namespace Particular.LicensingComponent.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserIndicator
{
    NServiceBusEndpoint,
    NotNServiceBusEndpoint,
    SendOnlyOrTransactionSessionEndpoint, // For backward compatibility with older versions of Usage Report in ServicePulse    
    TransactionalSessionProcessorEndpoint,
    SendOnlyEndpoint,
    NServiceBusEndpointNoLongerInUse,
    PlannedToDecommission
}