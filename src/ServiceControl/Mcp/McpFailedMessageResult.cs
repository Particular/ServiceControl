#nullable enable
namespace ServiceControl.Mcp;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using ServiceControl.Contracts.Operations;
using ServiceControl.MessageFailures;

public sealed class McpFailedMessageResult
{
    public string? Error { get; init; }
    public string Id { get; init; } = string.Empty;
    public List<McpFailedProcessingAttemptResult> ProcessingAttempts { get; init; } = [];
    public List<McpFailedFailureGroupResult> FailureGroups { get; init; } = [];
    public string UniqueMessageId { get; init; } = string.Empty;
    public FailedMessageStatus Status { get; init; }

    public static McpFailedMessageResult From(FailedMessage message) => new()
    {
        Id = message.Id ?? string.Empty,
        ProcessingAttempts = message.ProcessingAttempts.Select(McpFailedProcessingAttemptResult.From).ToList(),
        FailureGroups = message.FailureGroups.Select(McpFailedFailureGroupResult.From).ToList(),
        UniqueMessageId = message.UniqueMessageId,
        Status = message.Status
    };
}

public sealed class McpFailedProcessingAttemptResult
{
    public List<McpMessageMetadataEntryResult> MessageMetadata { get; init; } = [];
    public FailureDetails? FailureDetails { get; init; }
    public DateTime AttemptedAt { get; init; }
    public string? MessageId { get; init; }
    public string? Body { get; init; }
    public Dictionary<string, string> Headers { get; init; } = [];

    public static McpFailedProcessingAttemptResult From(FailedMessage.ProcessingAttempt attempt) => new()
    {
        MessageMetadata = attempt.MessageMetadata.Select(entry => McpMessageMetadataEntryResult.From(entry.Key, entry.Value)).ToList(),
        FailureDetails = attempt.FailureDetails,
        AttemptedAt = attempt.AttemptedAt,
        MessageId = attempt.MessageId,
        Body = attempt.Body,
        Headers = attempt.Headers
    };
}

public sealed class McpMessageMetadataEntryResult
{
    public string Key { get; init; } = string.Empty;
    public string? Value { get; init; }
    public string Type { get; init; } = string.Empty;

    public static McpMessageMetadataEntryResult From(string key, object? value) => new()
    {
        Key = key,
        Value = FormatValue(value),
        Type = GetTypeName(value)
    };

    static string? FormatValue(object? value) => value switch
    {
        null => null,
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
        bool boolean => boolean ? "true" : "false",
        string text => text,
        sbyte number => number.ToString(CultureInfo.InvariantCulture),
        byte number => number.ToString(CultureInfo.InvariantCulture),
        short number => number.ToString(CultureInfo.InvariantCulture),
        ushort number => number.ToString(CultureInfo.InvariantCulture),
        int number => number.ToString(CultureInfo.InvariantCulture),
        uint number => number.ToString(CultureInfo.InvariantCulture),
        long number => number.ToString(CultureInfo.InvariantCulture),
        ulong number => number.ToString(CultureInfo.InvariantCulture),
        float number => number.ToString(CultureInfo.InvariantCulture),
        double number => number.ToString(CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        Enum enumValue => enumValue.ToString(),
        _ => JsonSerializer.Serialize(value, value.GetType(), McpJsonOptions.Default)
    };

    static string GetTypeName(object? value) => value switch
    {
        null => "null",
        string => "string",
        bool => "boolean",
        sbyte or byte or short or ushort or int or uint or long or ulong => "integer",
        float or double or decimal => "number",
        DateTime or DateTimeOffset => "date-time",
        TimeSpan => "time-span",
        Enum => "enum",
        _ => "json"
    };
}

public sealed class McpFailedFailureGroupResult
{
    public string Id { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string? Type { get; init; }

    public static McpFailedFailureGroupResult From(FailedMessage.FailureGroup group) => new()
    {
        Id = group.Id,
        Title = group.Title,
        Type = group.Type
    };
}