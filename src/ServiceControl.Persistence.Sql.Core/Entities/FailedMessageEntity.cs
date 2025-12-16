namespace ServiceControl.Persistence.Sql.Core.Entities;

using System;
using ServiceControl.MessageFailures;

public class FailedMessageEntity
{
    public Guid Id { get; set; }
    public string UniqueMessageId { get; set; } = null!;
    public FailedMessageStatus Status { get; set; }

    // JSON columns for complex nested data
    public string ProcessingAttemptsJson { get; set; } = null!;
    public string FailureGroupsJson { get; set; } = null!;
    public string HeadersJson { get; set; } = null!;

    // Denormalized fields from FailureGroups for efficient filtering
    // PrimaryFailureGroupId is the first group ID from FailureGroupsJson array
    public string? PrimaryFailureGroupId { get; set; }

    // Denormalized fields from the last processing attempt for efficient querying
    public string? MessageId { get; set; }
    public string? MessageType { get; set; }
    public DateTime? TimeSent { get; set; }
    public string? SendingEndpointName { get; set; }
    public string? ReceivingEndpointName { get; set; }
    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? QueueAddress { get; set; }
    public int? NumberOfProcessingAttempts { get; set; }
    public DateTime? LastProcessedAt { get; set; }
    public string? ConversationId { get; set; }
}
