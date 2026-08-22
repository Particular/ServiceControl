namespace ServiceControl.Persistence.Tests.AuditCapable
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.MessageAuditing;
    using ServiceControl.Operations;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.SagaAudit;

    public class InMemoryAuditStore
    {
        readonly ConcurrentQueue<AuditRecord> processedMessages = new();
        readonly ConcurrentQueue<SagaSnapshot> sagaSnapshots = new();
        readonly ConcurrentDictionary<string, FailedAuditImport> failedImports = new();

        public void Record(ProcessedMessage processedMessage, byte[] body) =>
            processedMessages.Enqueue(new AuditRecord(processedMessage, body));

        public void Record(SagaSnapshot sagaSnapshot) => sagaSnapshots.Enqueue(sagaSnapshot);

        public void Record(FailedAuditImport failedImport) => failedImports[failedImport.Id] = failedImport;

        public IReadOnlyList<FailedAuditImport> FailedImports => [.. failedImports.Values];

        public bool RemoveFailedImport(string id) => failedImports.TryRemove(id, out _);

        public IReadOnlyList<MessagesView> MessageViews => [.. processedMessages.Select(record => ToMessagesView(record.Message))];

        public byte[]? BodyFor(string uniqueMessageId) =>
            processedMessages.FirstOrDefault(record => record.Message.UniqueMessageId == uniqueMessageId)?.Body;

        public IReadOnlyList<(DateTime UtcDate, long Count)> CountsFor(string endpointName) =>
        [
            .. processedMessages
                .Where(record => EndpointOf(record.Message) == endpointName)
                .GroupBy(record => record.Message.ProcessedAt.Date)
                .Select(group => (UtcDate: group.Key, Count: (long)group.Count()))
                .OrderBy(count => count.UtcDate)
        ];

        public SagaHistory? HistoryFor(Guid sagaId)
        {
            var snapshots = sagaSnapshots.Where(snapshot => snapshot.SagaId == sagaId).ToList();

            if (snapshots.Count == 0)
            {
                return null;
            }

            return new SagaHistory
            {
                Id = sagaId,
                SagaId = sagaId,
                SagaType = snapshots[0].SagaType,
                Changes = [.. snapshots.OrderByDescending(snapshot => snapshot.FinishTime).Select(ToStateChange)]
            };
        }

        static MessagesView ToMessagesView(ProcessedMessage message) => new()
        {
            Id = message.Id,
            MessageId = Metadata<string>(message, "MessageId"),
            MessageType = Metadata<string>(message, "MessageType"),
            SendingEndpoint = Metadata<EndpointDetails>(message, "SendingEndpoint"),
            ReceivingEndpoint = Metadata<EndpointDetails>(message, "ReceivingEndpoint"),
            TimeSent = Metadata<DateTime?>(message, "TimeSent"),
            ProcessedAt = message.ProcessedAt,
            CriticalTime = Metadata<TimeSpan>(message, "CriticalTime"),
            ProcessingTime = Metadata<TimeSpan>(message, "ProcessingTime"),
            DeliveryTime = Metadata<TimeSpan>(message, "DeliveryTime"),
            IsSystemMessage = Metadata<bool>(message, "IsSystemMessage"),
            ConversationId = Metadata<string>(message, "ConversationId"),
            Headers = [.. message.Headers.Select(header => new KeyValuePair<string, object>(header.Key, header.Value))],
            Status = MessageStatus.Successful,
            MessageIntent = Metadata<MessageIntent>(message, "MessageIntent"),
            BodyUrl = $"/messages/{message.UniqueMessageId}/body"
        };

        static T? Metadata<T>(ProcessedMessage message, string key) =>
            message.MessageMetadata.TryGetValue(key, out var value) && value is T typed ? typed : default;

        static SagaStateChange ToStateChange(SagaSnapshot snapshot) => new()
        {
            StartTime = snapshot.StartTime,
            FinishTime = snapshot.FinishTime,
            Status = snapshot.Status,
            StateAfterChange = snapshot.StateAfterChange,
            InitiatingMessage = snapshot.InitiatingMessage,
            OutgoingMessages = snapshot.OutgoingMessages,
            Endpoint = snapshot.Endpoint
        };

        static string? EndpointOf(ProcessedMessage message) =>
            message.Headers.GetValueOrDefault(Headers.ProcessingEndpoint);

        sealed record AuditRecord(ProcessedMessage Message, byte[] Body);
    }
}
