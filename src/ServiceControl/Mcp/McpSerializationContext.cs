#nullable enable
namespace ServiceControl.Mcp;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using ServiceControl.Contracts.Operations;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

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
[JsonSerializable(typeof(FailedMessageView))]
[JsonSerializable(typeof(FailedMessage))]
[JsonSerializable(typeof(GroupOperation[]))]
[JsonSerializable(typeof(RetryHistory))]
public partial class McpSerializationContext : JsonSerializerContext;