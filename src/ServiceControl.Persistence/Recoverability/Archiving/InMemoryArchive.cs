namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using ServiceControl.Recoverability.Archiving.Metrics;

    public class InMemoryArchive // in memory
    {
        public InMemoryArchive(string requestId, ArchiveType archiveType, IDomainEvents domainEvents, ArchiveMetrics? metrics = null)
        {
            RequestId = requestId;
            ArchiveType = archiveType;
            this.domainEvents = domainEvents;
            operationMetrics = metrics?.CreateOperation(ArchiveOperationKind.Archive);
        }

        public int TotalNumberOfMessages { get; set; }
        public int NumberOfMessagesArchived { get; set; }
        public int NumberOfBatches { get; set; }
        public int CurrentBatch { get; set; }
        public DateTime? CompletionTime { get; set; }
        public DateTime? Last { get; set; }
        public DateTime Started { get; set; }
        public ArchiveState ArchiveState { get; set; }
        public string? GroupName { get; set; }
        public string RequestId { get; set; }
        public ArchiveType ArchiveType { get; set; }

        public static string MakeId(string requestId, ArchiveType archiveType)
        {
            return $"{archiveType}/{requestId}";
        }

        public ArchiveProgress GetProgress()
        {
            var percentage = OperationProgressCalculator.CalculateProgress(TotalNumberOfMessages, NumberOfMessagesArchived, ArchiveState);
            var roundedPercentage = Math.Round(percentage, 2);

            var remaining = TotalNumberOfMessages - NumberOfMessagesArchived;

            return new ArchiveProgress(roundedPercentage, TotalNumberOfMessages, NumberOfMessagesArchived, remaining);
        }

        public Task Start(CancellationToken cancellationToken = default)
        {
            ArchiveState = ArchiveState.ArchiveStarted;
            CompletionTime = null;
            operationMetrics?.Started();

            return domainEvents.Raise(new ArchiveOperationStarting
            {
                RequestId = RequestId,
                ArchiveType = ArchiveType,
                Progress = GetProgress(),
                StartTime = Started
            }, cancellationToken);
        }

        public Task BatchArchived(int numberOfMessagesArchivedInBatch, CancellationToken cancellationToken = default)
        {
            ArchiveState = ArchiveState.ArchiveProgressing;
            NumberOfMessagesArchived += numberOfMessagesArchivedInBatch;
            CurrentBatch++;
            Last = DateTime.UtcNow;
            operationMetrics?.BatchCompleted(numberOfMessagesArchivedInBatch);

            return domainEvents.Raise(new ArchiveOperationBatchCompleted
            {
                RequestId = RequestId,
                ArchiveType = ArchiveType,
                Progress = GetProgress(),
                StartTime = Started,
                Last = Last.Value
            }, cancellationToken);
        }

        public Task FinalizeArchive(CancellationToken cancellationToken = default)
        {
            ArchiveState = ArchiveState.ArchiveFinalizing;
            NumberOfMessagesArchived = TotalNumberOfMessages;
            Last = DateTime.UtcNow;
            operationMetrics?.Finalizing();

            return domainEvents.Raise(new ArchiveOperationFinalizing
            {
                RequestId = RequestId,
                ArchiveType = ArchiveType,
                Progress = GetProgress(),
                StartTime = Started,
                Last = Last.Value
            }, cancellationToken);
        }

        public Task Complete(CancellationToken cancellationToken = default)
        {
            ArchiveState = ArchiveState.ArchiveCompleted;
            NumberOfMessagesArchived = TotalNumberOfMessages;
            CompletionTime = DateTime.UtcNow;
            Last = DateTime.UtcNow;
            operationMetrics?.Completed();

            return domainEvents.Raise(new ArchiveOperationCompleted
            {
                RequestId = RequestId,
                ArchiveType = ArchiveType,
                Progress = GetProgress(),
                StartTime = Started,
                Last = Last.Value,
                CompletionTime = CompletionTime.Value,
                GroupName = GroupName
            }, cancellationToken);
        }

        public bool NeedsAcknowledgement()
        {
            return ArchiveState == ArchiveState.ArchiveCompleted;
        }

        IDomainEvents domainEvents;
        readonly ArchiveOperationMetrics? operationMetrics;
    }
}