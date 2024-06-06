namespace ServiceControl.Monitoring.HeartbeatMonitoring
{
    using System.Text.Json.Serialization;
    using Plugin.Heartbeat.Messages;
    using ServiceControl.HeartbeatMonitoring.InternalMessages;

    [JsonSerializable(typeof(EndpointHeartbeat))]
    [JsonSerializable(typeof(RegisterEndpointStartup))]
    [JsonSerializable(typeof(RegisterPotentiallyMissingHeartbeats))]
    partial class HeartbeatSerializationContext : JsonSerializerContext;
}