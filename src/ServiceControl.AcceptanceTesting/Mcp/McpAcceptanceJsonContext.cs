namespace ServiceControl.AcceptanceTesting.Mcp;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(List<JsonElement>))]
[JsonSerializable(typeof(McpListToolsResponse))]
[JsonSerializable(typeof(McpCallToolResponse))]
[JsonSerializable(typeof(McpInitializeResponse))]
public partial class McpAcceptanceJsonContext : JsonSerializerContext;
