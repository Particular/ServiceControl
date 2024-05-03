namespace ServiceControl.Audit.Monitoring;

using System.Text.Json.Serialization;
using Contracts.MessageFailures;

[JsonSerializable(typeof(MarkMessageFailureResolvedByRetry))]
public partial class RecoverabilityessagesSerializationContext : JsonSerializerContext;