﻿namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;

    public class InMemoryArchive // in memory
    {
        public InMemoryArchive(string requestId, ArchiveType archiveType, IDomainEvents domainEvents)
        {
            RequestId = requestId;
            ArchiveType = archiveType;
            this.domainEvents = domainEvents;
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

        public Task Start()
        {
            ArchiveState = ArchiveState.ArchiveStarted;
            CompletionTime = null;

            return domainEvents.Raise(new ArchiveOperationStarting
            {
                RequestId = RequestId,
                ArchiveType = ArchiveType,
                Progress = GetProgress(),
                StartTime = Started
            }, CancellationToken.None);
        }

        public Task BatchArchived(int numberOfMessagesArchivedInBatch)
        {
            ArchiveState = ArchiveState.ArchiveProgressing;
            NumberOfMessagesArchived += numberOfMessagesArchivedInBatch;
            CurrentBatch++;
            Last = DateTime.UtcNow;

            return domainEvents.Raise(new ArchiveOperationBatchCompleted
            {
                RequestId = RequestId,
                ArchiveType = ArchiveType,
                Progress = GetProgress(),
                StartTime = Started,
                Last = Last.Value
            }, CancellationToken.None);
        }

        public Task FinalizeArchive()
        {
            ArchiveState = ArchiveState.ArchiveFinalizing;
            NumberOfMessagesArchived = TotalNumberOfMessages;
            Last = DateTime.UtcNow;

            return domainEvents.Raise(new ArchiveOperationFinalizing
            {
                RequestId = RequestId,
                ArchiveType = ArchiveType,
                Progress = GetProgress(),
                StartTime = Started,
                Last = Last.Value
            }, CancellationToken.None);
        }

        public Task Complete()
        {
            ArchiveState = ArchiveState.ArchiveCompleted;
            NumberOfMessagesArchived = TotalNumberOfMessages;
            CompletionTime = DateTime.UtcNow;
            Last = DateTime.UtcNow;

            return domainEvents.Raise(new ArchiveOperationCompleted
            {
                RequestId = RequestId,
                ArchiveType = ArchiveType,
                Progress = GetProgress(),
                StartTime = Started,
                Last = Last.Value,
                CompletionTime = CompletionTime.Value,
                GroupName = GroupName
            }, CancellationToken.None);
        }

        public bool NeedsAcknowledgement()
        {
            return ArchiveState == ArchiveState.ArchiveCompleted;
        }

        IDomainEvents domainEvents;
    }
}