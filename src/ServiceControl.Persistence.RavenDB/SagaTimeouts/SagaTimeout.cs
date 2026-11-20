namespace ServiceControl.Persistence.RavenDB.SagaTimeouts
{
    using System;
    using System.Collections.Generic;
    using ServiceControl.Persistence.SagaTimeouts;

    // The type names give the collection names SagaTimeouts and CompletedSagas
    class SagaTimeout
    {
        public string Id { get; set; }
        public string TimeoutId { get; set; }
        public string EndpointName { get; set; }
        public string SagaType { get; set; }
        public Guid SagaId { get; set; }
        public string CorrelationValue { get; set; }
        public string TimeoutType { get; set; }
        public int Version { get; set; }
        public int Rank { get; set; }
        public string TriggerId { get; set; }
        public DateTime? DueTime { get; set; }
        public SagaTimeoutStatus Status { get; set; }
        public DateTime? RequestedAt { get; set; }
        public SagaTimeoutChangeKind LastChangeKind { get; set; }
        public SagaTimeoutChangeSource LastChangeSource { get; set; }
        public DateTime? LastChangedAt { get; set; }
        public DateTime? StatusChangedAt { get; set; }

        // ChangedAt of the newest report, which decides whether its host and management address replace the stored ones
        public DateTime? LastReportedAt { get; set; }
        public string Host { get; set; }
        public string HostId { get; set; }
        public string ManagementAddress { get; set; }

        public PendingOperation PendingOperation { get; set; }
        public bool HistoryTruncated { get; set; }
        public List<HistoryEntry> Changes { get; set; } = [];

        // The payload of each version is an attachment named by PayloadAttachmentName
        public List<PayloadVersion> Payloads { get; set; } = [];

        public static string MakeDocumentId(string timeoutId) => $"SagaTimeouts/{timeoutId}";

        public static string PayloadAttachmentName(int version) => $"payload-v{version}";
    }

    class PendingOperation
    {
        public Guid OperationId { get; set; }
        public SagaTimeoutOperation Change { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? RequestedDueTime { get; set; }
    }

    class HistoryEntry
    {
        public string DeduplicationKey { get; set; }
        public SagaTimeoutChangeKind Kind { get; set; }
        public int Version { get; set; }
        public string TriggerId { get; set; }
        public DateTime? DueTime { get; set; }
        public DateTime ChangedAt { get; set; }
        public DateTime ReceivedAt { get; set; }
        public SagaTimeoutChangeSource Source { get; set; }
        public Guid? OperationId { get; set; }
        public string RequestedChange { get; set; }
        public DateTime? RequestedDueTime { get; set; }
        public string Reason { get; set; }
    }

    class PayloadVersion
    {
        public int Version { get; set; }
        public string ContentType { get; set; }
    }

    class CompletedSaga
    {
        public string Id { get; set; }
        public Guid SagaId { get; set; }
        public string EndpointName { get; set; }
        public string SagaType { get; set; }
        public DateTime ChangedAt { get; set; }
        public DateTime ReceivedAt { get; set; }

        public static string MakeDocumentId(Guid sagaId) => $"CompletedSagas/{sagaId}";
    }
}
