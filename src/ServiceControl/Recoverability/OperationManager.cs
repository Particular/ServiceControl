namespace ServiceControl.Recoverability
{
    using System;
    using System.Linq;
    using System.Collections.Generic;

    public class OperationManager
    {
        internal static Dictionary<string, RetryOperation> RetryOperations = new Dictionary<string, RetryOperation>();
        internal static Dictionary<string, ArchiveOperationLogic> ArchiveOperations = new Dictionary<string, ArchiveOperationLogic>();

        public void Wait(string requestId, RetryType retryType, DateTime started, string originator = null, string classifier = null, DateTime? last = null)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = GetOrCreate(retryType, requestId);

            summary.Wait(started, originator, classifier, last);
        }

        public bool IsOperationInProgressFor(string requestId, RetryType retryType)
        {
            RetryOperation summary;
            if (!RetryOperations.TryGetValue(RetryOperation.MakeOperationId(requestId, retryType), out summary))
            {
                return false;
            }

            return summary.IsInProgress();
        }

        public bool IsRetryInProgressFor(string requestId)
        {
            return RetryOperations.Keys.Where(key => key.EndsWith($"/{requestId}")).Any(key => RetryOperations[key].IsInProgress());
        }

        public bool IsArchiveInProgressFor(string requestId)
        {
            return ArchiveOperations.Keys.Any(key => key.EndsWith($"/{requestId}"));
        }

        internal IEnumerable<ArchiveOperationLogic> GetArchivalOperations()
        {
            return ArchiveOperations.Values;
        }

        public void Prepairing(string requestId, RetryType retryType, int totalNumberOfMessages)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = GetOrCreate(retryType, requestId);

            summary.Prepare(totalNumberOfMessages);
        }

        public void PreparedAdoptedBatch(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages, string originator, string classifier, DateTime startTime, DateTime last)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = GetOrCreate(retryType, requestId);

            summary.Prepare(totalNumberOfMessages);
            summary.PrepareAdoptedBatch(numberOfMessagesPrepared, originator, classifier, startTime, last);
        }

        public void PreparedBatch(string requestId, RetryType retryType, int numberOfMessagesPrepared)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = GetOrCreate(retryType, requestId);

            summary.PrepareBatch(numberOfMessagesPrepared);
        }

        public void Forwarding(string requestId, RetryType retryType)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = Get(requestId, retryType);

            summary.Forwarding();
        }

        public void ForwardedBatch(string requestId, RetryType retryType, int numberOfMessagesForwarded)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = Get(requestId, retryType);

            summary.BatchForwarded(numberOfMessagesForwarded);
        }

        public void Fail(RetryType retryType, string requestId)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = GetOrCreate(retryType, requestId);

            summary.Fail();
        }

        public void Skip(string requestId, RetryType retryType, int numberOfMessagesSkipped)
        {
            if (requestId == null) //legacy support for batches created before operations were introduced
            {
                return;
            }

            var summary = GetOrCreate(retryType, requestId);
            summary.Skip(numberOfMessagesSkipped);
        }

        private RetryOperation GetOrCreate(RetryType retryType, string requestId)
        {
            RetryOperation summary;
            if (!RetryOperations.TryGetValue(RetryOperation.MakeOperationId(requestId, retryType), out summary))
            {
                summary = new RetryOperation(requestId, retryType);
                RetryOperations[RetryOperation.MakeOperationId(requestId, retryType)] = summary;
            }
            return summary;
        }

        private static RetryOperation Get(string requestId, RetryType retryType)
        {
            return RetryOperations[RetryOperation.MakeOperationId(requestId, retryType)];
        }

        public RetryOperation GetStatusForRetryOperation(string requestId, RetryType retryType)
        {
            RetryOperation summary;
            RetryOperations.TryGetValue(RetryOperation.MakeOperationId(requestId, retryType), out summary);

            return summary;
        }

        public bool IsOperationInProgressFor(string requestId, ArchiveType archiveType)
        {
            ArchiveOperationLogic summary;
            if (!ArchiveOperations.TryGetValue(ArchiveOperationLogic.MakeId(requestId, archiveType), out summary))
            {
                return false;
            }

            return summary.ArchiveState != ArchiveState.ArchiveCompleted;
        }

        private ArchiveOperationLogic GetOrCreate(ArchiveType archiveType, string requestId)
        {
            ArchiveOperationLogic summary;
            if (!ArchiveOperations.TryGetValue(ArchiveOperationLogic.MakeId(requestId, archiveType), out summary))
            {
                summary = new ArchiveOperationLogic(requestId, archiveType);
                ArchiveOperations[ArchiveOperationLogic.MakeId(requestId, archiveType)] = summary;
            }
            return summary;
        }

        public void StartArchiving(ArchiveOperation archiveOperation)
        {
            var summary = GetOrCreate(archiveOperation.ArchiveType, archiveOperation.RequestId);

            summary.TotalNumberOfMessages = archiveOperation.TotalNumberOfMessages;
            summary.NumberOfMessagesArchived = archiveOperation.NumberOfMessagesArchived;
            summary.Started = archiveOperation.Started;
            summary.GroupName = archiveOperation.GroupName;
            summary.NumberOfBatches = archiveOperation.NumberOfBatches;
            summary.CurrentBatch = archiveOperation.CurrentBatch;

            summary.Start();
        }

        public void StartArchiving(string requestId, ArchiveType archiveType)
        {
            var summary = GetOrCreate(archiveType, requestId);

            summary.TotalNumberOfMessages = 0;
            summary.NumberOfMessagesArchived = 0;
            summary.Started = DateTime.Now;
            summary.GroupName = "Undefined";
            summary.NumberOfBatches = 0;
            summary.CurrentBatch = 0;

            summary.Start();
        }

        public ArchiveOperationLogic GetStatusForArchiveOperation(string requestId, ArchiveType archiveType)
        {
            ArchiveOperationLogic summary;
            ArchiveOperations.TryGetValue(ArchiveOperationLogic.MakeId(requestId, archiveType), out summary);

            return summary;
        }

        public void BatchArchived(string requestId, ArchiveType archiveType, int numberOfMessagesArchivedInBatch)
        {
            var summary = GetOrCreate(archiveType, requestId);

            summary.BatchArchived(numberOfMessagesArchivedInBatch);
        }

        public void ArchiveOperationFinalizing(string requestId, ArchiveType archiveType)
        {
            var summary = GetOrCreate(archiveType, requestId);
            summary.FinalizeArchive();
        }

        public void ArchiveOperationCompleted(string requestId, ArchiveType archiveType)
        {
            var summary = GetOrCreate(archiveType, requestId);
            summary.Complete();
        }

        public void DismissArchiveOperation(string requestId, ArchiveType archiveType)
        {
            RemoveArchiveOperation(requestId, archiveType);
        }

        void RemoveArchiveOperation(string requestId, ArchiveType archiveType)
        {
            ArchiveOperations.Remove(ArchiveOperationLogic.MakeId(requestId, archiveType));
        }
    }
}