namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;

    class InMemoryUnarchive // in memory
    {
        public InMemoryUnarchive(string requestId, ArchiveType archiveType, IDomainEvents domainEvents)
        {
            RequestId = requestId;
            ArchiveType = archiveType;
            this.domainEvents = domainEvents;
        }

        public int TotalNumberOfMessages { get; set; }
        public int NumberOfMessagesUnarchived { get; set; }
        public int NumberOfBatches { get; set; }
        public int CurrentBatch { get; set; }
        public DateTime? CompletionTime { get; set; }
        public DateTime? Last { get; set; }
        public DateTime Started { get; set; }
        public ArchiveState ArchiveState { get; set; }
        public string GroupName { get; set; }
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

        internal Task Start()
        {
            ArchiveState = ArchiveState.ArchiveStarted;
            CompletionTime = null;

            return domainEvents.Raise(new UnarchiveOperationStarting
            {
                RequestId = RequestId,
                ArchiveType = ArchiveType,
                Progress = GetProgress(),
                StartTime = Started
            });
        }

        internal Task BatchUnarchived(int numberOfMessagesUnarchivedInBatch)
        {
            ArchiveState = ArchiveState.ArchiveProgressing;
            NumberOfMessagesUnarchived += numberOfMessagesUnarchivedInBatch;
            CurrentBatch++;
            Last = DateTime.Now;

            return domainEvents.Raise(new UnarchiveOperationBatchCompleted
            {
                RequestId = RequestId,
                ArchiveType = ArchiveType,
                Progress = GetProgress(),
                StartTime = Started,
                Last = Last.Value
            });
        }

        internal Task FinalizeUnarchive()
        {
            ArchiveState = ArchiveState.ArchiveFinalizing;
            NumberOfMessagesUnarchived = TotalNumberOfMessages;
            Last = DateTime.Now;

            return domainEvents.Raise(new UnarchiveOperationFinalizing
            {
                RequestId = RequestId,
                ArchiveType = ArchiveType,
                Progress = GetProgress(),
                StartTime = Started,
                Last = Last.Value
            });
        }

        internal Task Complete()
        {
            ArchiveState = ArchiveState.ArchiveCompleted;
            NumberOfMessagesUnarchived = TotalNumberOfMessages;
            CompletionTime = DateTime.Now;
            Last = DateTime.Now;

            return domainEvents.Raise(new UnarchiveOperationCompleted
            {
                RequestId = RequestId,
                ArchiveType = ArchiveType,
                Progress = GetProgress(),
                StartTime = Started,
                Last = Last.Value,
                CompletionTime = CompletionTime.Value,
                GroupName = GroupName
            });
        }

        internal UnarchiveOperation ToUnarchiveOperation()
        {
            return new UnarchiveOperation
            {
                ArchiveType = ArchiveType,
                GroupName = GroupName,
                Id = UnarchiveOperation.MakeId(RequestId, ArchiveType),
                NumberOfMessagesUnarchived = NumberOfMessagesUnarchived,
                RequestId = RequestId,
                Started = Started,
                TotalNumberOfMessages = TotalNumberOfMessages,
                NumberOfBatches = NumberOfBatches,
                CurrentBatch = CurrentBatch
            };
        }

        internal bool NeedsAcknowledgement()
        {
            return ArchiveState == ArchiveState.ArchiveCompleted;
        }

        IDomainEvents domainEvents;
    }
}