﻿namespace Particular.ThroughputCollector.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserIndicator
{
    NServiceBusEndpoint,
    NotNServiceBusEndpoint,
    NServiceBusEndpointSendOnly,
    NServiceBusEndpointNoLongerInUse,
    TransactionSessionEndpoint,
    PlannedToDecommission
}
