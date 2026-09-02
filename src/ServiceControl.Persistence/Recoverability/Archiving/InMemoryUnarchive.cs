namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using ServiceControl.Recoverability.Archiving.Metrics;

    public class InMemoryUnarchive // in memory
    {
        public InMemoryUnarchive(string requestId, ArchiveType archiveType, IDomainEvents domainEvents, ArchiveMetrics? metrics = null)
        {
            RequestId = requestId;
            ArchiveType = archiveType;
            this.domainEvents = domainEvents;
            operationMetrics = metrics?.CreateOperation(ArchiveOperationKind.Unarchive);
        }

        public int TotalNumberOfMessages { get; set; }
        public int NumberOfMessagesUnarchived { get; set; }
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

        public UnarchiveProgress GetProgress()
        {
            var percentage = OperationProgressCalculator.CalculateProgress(TotalNumberOfMessages, NumberOfMessagesUnarchived, ArchiveState);
            var roundedPercentage = Math.Round(percentage, 2);

            var remaining = TotalNumberOfMessages - NumberOfMessagesUnarchived;

            return new UnarchiveProgress(roundedPercentage, TotalNumberOfMessages, NumberOfMessagesUnarchived, remaining);
        }

        public Task Start(CancellationToken cancellationToken = default)
        {
            ArchiveState = ArchiveState.ArchiveStarted;
            CompletionTime = null;
            operationMetrics?.Started();

            return domainEvents.Raise(new UnarchiveOperationStarting
            {
                RequestId = RequestId,
                ArchiveType = ArchiveType,
                Progress = GetProgress(),
                StartTime = Started
            }, cancellationToken);
        }

        public Task BatchUnarchived(int numberOfMessagesUnarchivedInBatch, CancellationToken cancellationToken = default)
        {
            ArchiveState = ArchiveState.ArchiveProgressing;
            NumberOfMessagesUnarchived += numberOfMessagesUnarchivedInBatch;
            CurrentBatch++;
            Last = DateTime.UtcNow;
            operationMetrics?.BatchCompleted(numberOfMessagesUnarchivedInBatch);

            return domainEvents.Raise(new UnarchiveOperationBatchCompleted
            {
                RequestId = RequestId,
                ArchiveType = ArchiveType,
                Progress = GetProgress(),
                StartTime = Started,
                Last = Last.Value
            }, cancellationToken);
        }

        public Task FinalizeUnarchive(CancellationToken cancellationToken = default)
        {
            ArchiveState = ArchiveState.ArchiveFinalizing;
            NumberOfMessagesUnarchived = TotalNumberOfMessages;
            Last = DateTime.UtcNow;
            operationMetrics?.Finalizing();

            return domainEvents.Raise(new UnarchiveOperationFinalizing
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
            NumberOfMessagesUnarchived = TotalNumberOfMessages;
            CompletionTime = DateTime.UtcNow;
            Last = DateTime.UtcNow;
            operationMetrics?.Completed();

            return domainEvents.Raise(new UnarchiveOperationCompleted
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

        internal bool NeedsAcknowledgement()
        {
            return ArchiveState == ArchiveState.ArchiveCompleted;
        }

        IDomainEvents domainEvents;
        readonly ArchiveOperationMetrics? operationMetrics;
    }
}