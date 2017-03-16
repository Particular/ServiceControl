namespace ServiceControl.Recoverability
{
    using ServiceControl.Infrastructure.DomainEvents;
    using System;

    public class ArchiveOperationLogic
    {
        public ArchiveOperationLogic(string requestId, ArchiveType archiveType)
        {
            this.requestId = requestId;
            this.archiveType = archiveType;
        }

        public int TotalNumberOfMessages { get; set; }
        public int NumberOfMessagesArchived { get; set; }
        public int NumberOfBatches { get; set; }
        public int CurrentBatch { get; set; }
        public DateTime? CompletionTime { get; set; }
        public DateTime? Last { get; set; }
        public DateTime Started { get; set; }
        public ArchiveState ArchiveState { get; set; }
        public string GroupName { get; set; }

        private readonly string requestId;
        private readonly ArchiveType archiveType;

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

        internal void Start()
        {
            ArchiveState = ArchiveState.Started;
            CompletionTime = null;

            DomainEvents.Raise(new ArchiveOperationStarting
            {
                RequestId = requestId,
                ArchiveType = archiveType,
                Progress = GetProgress(),
                StartTime = Started
            });
        }

        internal void BatchArchived(int numberOfMessagesArchivedInBatch)
        {
            ArchiveState = ArchiveState.Archiving;
            NumberOfMessagesArchived += numberOfMessagesArchivedInBatch;
            CurrentBatch++;
            Last = DateTime.Now;

            DomainEvents.Raise(new ArchiveOperationBatchCompleted
            {
                RequestId = requestId,
                ArchiveType = archiveType,
                Progress = GetProgress(),
                StartTime = Started,
                Last = Last.Value
            });
        }

        internal void Complete()
        {
            ArchiveState = ArchiveState.Completed;
            NumberOfMessagesArchived = TotalNumberOfMessages;
            CompletionTime = DateTime.Now;
            Last = DateTime.Now;

            DomainEvents.Raise(new ArchiveOperationCompleted
            {
                RequestId = requestId,
                ArchiveType = archiveType,
                Progress = GetProgress(),
                StartTime = Started,
                Last = Last.Value,
                CompletionTime = CompletionTime.Value
            });
        }

        internal ArchiveOperation ToArchiveOperation()
        {
            return new ArchiveOperation
            {
                ArchiveType = archiveType,
                GroupName = GroupName,
                Id = ArchiveOperation.MakeId(requestId, archiveType),
                NumberOfMessagesArchived = NumberOfMessagesArchived,
                RequestId = requestId,
                Started = Started,
                TotalNumberOfMessages = TotalNumberOfMessages,
                NumberOfBatches = NumberOfBatches,
                CurrentBatch = CurrentBatch
            };
        }
    }
}