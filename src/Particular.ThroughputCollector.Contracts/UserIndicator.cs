namespace Particular.ThroughputCollector.Contracts;
using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserIndicator
{
    NServicebusEndpoint,
    NotNServicebusEndpoint,
    NServicebusEndpointSendOnly,
    NServicebusEndpointScaledOut,
    NServicebusEndpointNoLongerInUse
}
