namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;

    public class RetryOperationManager
    {
        public static void SetToForwarding(RetryOperation retryOperation)
        {
            if (retryOperation != null)
            {
                RetryGroupSummary.SetStatus(retryOperation.GroupId, RetryGroupStatus.Forwarding, retryOperation.GetCompletedBatchesInOperation(), retryOperation.BatchesInOperation);
            }
        }

        public static void Forward(RetryOperation retryOperation, out bool entireRetryOperationForwarded)
        {
            entireRetryOperationForwarded = false;

            if (retryOperation == null)
            {
                return;
            }

            retryOperation.ForwardBatch(out entireRetryOperationForwarded);

            if (entireRetryOperationForwarded)
            {
                RetryGroupSummary.SetStatus(retryOperation.GroupId, RetryGroupStatus.Forwarded, retryOperation.BatchesInOperation, retryOperation.BatchesInOperation);
            }
        }
    }

    public class RetryGroupSummary
    {
        public static Dictionary<string, RetryGroupSummary> CurrentRetryGroups = new Dictionary<string, RetryGroupSummary>();
        
        internal static void SetStatus(string groupId, RetryGroupStatus status, int batchesCompleted, int totalBatches)
        {
            RetryGroupSummary summary;
            if (!CurrentRetryGroups.TryGetValue(groupId, out summary))
            {
                CurrentRetryGroups[groupId] = new RetryGroupSummary { Status = status, BatchesCompleted = batchesCompleted, TotalBatches = totalBatches };
            }
            else
            {
                CurrentRetryGroups[groupId].Status = status;
                CurrentRetryGroups[groupId].BatchesCompleted = batchesCompleted;
                CurrentRetryGroups[groupId].TotalBatches = totalBatches;
            }
        }

        public static void SetStatus(string groupId, RetryGroupStatus status)
        {
            RetryGroupSummary summary;
            if (!CurrentRetryGroups.TryGetValue(groupId, out summary))
            {
                CurrentRetryGroups[groupId] = new RetryGroupSummary { Status = status };
            }
            else
            {
                CurrentRetryGroups[groupId].Status = status;
            }
        }

        public static RetryGroupSummary GetStatusForGroup(string groupId)
        {
            RetryGroupSummary summary = null;
            CurrentRetryGroups.TryGetValue(groupId, out summary);

            return summary;
        }

        public RetryGroupStatus Status { get; set; }
        public int? BatchesCompleted { get; private set; }
        public int? TotalBatches { get; private set; }
    }

    // A different status enum, as the RetryBatchStatus doesn't quite give the
    // granularity that I think is needed. Could potentially merge together
    // at a later stage once the code is fleshed out better
    public enum RetryGroupStatus
    {
        Open,
        MarkingDocuments,
        DocumentsMarked,
        Staging,
        Staged,
        Forwarding,
        Forwarded,
        Preparing
    }
}