namespace ServiceControl.Persistence.Sql.Core.Infrastructure;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using ServiceControl.MessageFailures;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(List<FailedMessage.ProcessingAttempt>))]
[JsonSerializable(typeof(List<FailedMessage.FailureGroup>))]
partial class RecoverabilityJsonContext : JsonSerializerContext
{
}
