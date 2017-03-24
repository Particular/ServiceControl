namespace ServiceControl.Recoverability
{
    using ServiceControl.Infrastructure.DomainEvents;
    using System;

    public class ArchiveOperationLogic
    {
        public ArchiveOperationLogic(string requestId, ArchiveType archiveType)
        {
            RequestId = requestId;
            ArchiveType = archiveType;
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

        internal void Start()
        {
            ArchiveState = ArchiveState.ArchiveStarted;
            CompletionTime = null;

            DomainEvents.Raise(new ArchiveOperationStarting
            {
                RequestId = RequestId,
                ArchiveType = ArchiveType,
                Progress = GetProgress(),
                StartTime = Started
            });
        }

        internal void BatchArchived(int numberOfMessagesArchivedInBatch)
        {
            ArchiveState = ArchiveState.ArchiveProgressing;
            NumberOfMessagesArchived += numberOfMessagesArchivedInBatch;
            CurrentBatch++;
            Last = DateTime.Now;

            DomainEvents.Raise(new ArchiveOperationBatchCompleted
            {
                RequestId = RequestId,
                ArchiveType = ArchiveType,
                Progress = GetProgress(),
                StartTime = Started,
                Last = Last.Value
            });
        }

        internal void FinalizeArchive()
        {
            ArchiveState = ArchiveState.ArchiveFinalizing;
            NumberOfMessagesArchived = TotalNumberOfMessages;
            Last = DateTime.Now;

            DomainEvents.Raise(new ArchiveOperationFinalizing
            {
                RequestId = RequestId,
                ArchiveType = ArchiveType,
                Progress = GetProgress(),
                StartTime = Started,
                Last = Last.Value
            });
        }

        internal void Complete()
        {
            ArchiveState = ArchiveState.ArchiveCompleted;
            NumberOfMessagesArchived = TotalNumberOfMessages;
            CompletionTime = DateTime.Now;
            Last = DateTime.Now;

            DomainEvents.Raise(new ArchiveOperationCompleted
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

        internal ArchiveOperation ToArchiveOperation()
        {
            return new ArchiveOperation
            {
                ArchiveType = ArchiveType,
                GroupName = GroupName,
                Id = ArchiveOperation.MakeId(RequestId, ArchiveType),
                NumberOfMessagesArchived = NumberOfMessagesArchived,
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
    }
}