namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using System.Text.Json.Serialization;

// Source generated serialization for the failed message's headers, which are stored verbatim as
// the HeadersJson column. Avoids the reflection-based serializer on the ingestion hot path.
[JsonSerializable(typeof(Dictionary<string, string>))]
partial class HeadersJsonContext : JsonSerializerContext;
