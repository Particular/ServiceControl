namespace ServiceControl.Audit.Monitoring;

using System.Text.Json.Serialization;
using Contracts.EndpointControl;

[JsonSerializable(typeof(RegisterNewEndpoint))]
public partial class MonitoringMessagesSerializationContext : JsonSerializerContext;