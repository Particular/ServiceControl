namespace ServiceControl.Audit.Persistence.Sql.Core.Implementation.UnitOfWork;

using System.Text.Json.Serialization;
using ServiceControl.SagaAudit;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[JsonSerializable(typeof(InitiatingMessage))]
[JsonSerializable(typeof(List<ResultingMessage>))]
partial class SagaSnapshotJsonContext : JsonSerializerContext
{
}
