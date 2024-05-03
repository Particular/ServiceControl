namespace ServiceControl.SagaAudit
{
    using System.Text.Json.Serialization;
    using EndpointPlugin.Messages.SagaState;

    [JsonSerializable(typeof(SagaChangeInitiator))]
    [JsonSerializable(typeof(SagaChangeOutput))]
    [JsonSerializable(typeof(SagaUpdatedMessage))]
    public partial class SagaAuditMessagesSerializationContext : JsonSerializerContext;
}