namespace ServiceControl.Mcp;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using MessageFailures;
using ServiceControl.Contracts.Operations;
using ServiceControl.Infrastructure.Mcp;
using ServiceControl.MessageFailures.Api;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(McpCollectionResult<FailedMessageView>))]
[JsonSerializable(typeof(McpErrorsSummaryResult))]
[JsonSerializable(typeof(McpFailedMessageResult))]
[JsonSerializable(typeof(McpFailedMessageViewResult))]
[JsonSerializable(typeof(McpOperationResult))]
[JsonSerializable(typeof(List<McpMessageMetadataEntryResult>))]
[JsonSerializable(typeof(McpMessageMetadataEntryResult))]
[JsonSerializable(typeof(McpFailedProcessingAttemptResult))]
[JsonSerializable(typeof(McpFailedFailureGroupResult))]
[JsonSerializable(typeof(FailedMessageStatus))]
public partial class McpSerializationContext : JsonSerializerContext;
