namespace ServiceControl.Persistence.Tests.AuditCapable
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using ServiceControl.MessageAuditing;
    using ServiceControl.Operations;
    using ServiceControl.SagaAudit;

    public class InMemoryAuditStore
    {
        readonly ConcurrentQueue<ProcessedMessage> processedMessages = new();
        readonly ConcurrentQueue<SagaSnapshot> sagaSnapshots = new();
        readonly ConcurrentDictionary<string, FailedAuditImport> failedImports = new();

        public void Record(ProcessedMessage processedMessage) => processedMessages.Enqueue(processedMessage);

        public void Record(SagaSnapshot sagaSnapshot) => sagaSnapshots.Enqueue(sagaSnapshot);

        public void Record(FailedAuditImport failedImport) => failedImports[failedImport.Id] = failedImport;

        public IReadOnlyList<ProcessedMessage> ProcessedMessages => [.. processedMessages];

        public IReadOnlyList<FailedAuditImport> FailedImports => [.. failedImports.Values];

        public bool RemoveFailedImport(string id) => failedImports.TryRemove(id, out _);

        public IReadOnlyList<(DateTime UtcDate, long Count)> CountsFor(string endpointName) =>
        [
            .. processedMessages
                .Where(message => EndpointOf(message) == endpointName)
                .GroupBy(message => message.ProcessedAt.Date)
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
    }
}
