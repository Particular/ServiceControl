namespace ServiceControl.Audit.Persistence.Sql.Core.Implementation.UnitOfWork;

using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[JsonSerializable(typeof(Dictionary<string, string>))]
partial class ProcessedMessageJsonContext : JsonSerializerContext
{
}
