namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;

    public class RetryOperationManager
    {
        public static void Stage(RetryOperation retryOperation)
        {
            if (retryOperation == null)
            {
                return;
            }
            
            if (RetryOperationSummary.GetStatusForRetryOperation(retryOperation.GroupId).Status != RetryOperationStatus.Forwarding)
            {
                RetryOperationSummary.SetStatus(retryOperation.GroupId, RetryOperationStatus.Staging, retryOperation.GetCompletedBatchesInOperation(), retryOperation.BatchesInOperation);
            }
        }

        public static void ReadyToForward(RetryOperation retryOperation)
        {
            if (retryOperation == null)
            {
                return;
            }

            RetryOperationSummary.SetStatus(retryOperation.GroupId, RetryOperationStatus.Forwarding, retryOperation.GetCompletedBatchesInOperation(), retryOperation.BatchesInOperation);
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
                RetryOperationSummary.SetStatus(retryOperation.GroupId, RetryOperationStatus.Forwarded, retryOperation.BatchesInOperation, retryOperation.BatchesInOperation);
            }
        }
    }

    public class RetryOperationSummary
    {
        public static Dictionary<string, RetryOperationSummary> CurrentRetryOperations = new Dictionary<string, RetryOperationSummary>();
        
        internal static void SetStatus(string operationId, RetryOperationStatus status, int batchesCompleted, int totalBatches)
        {
            RetryOperationSummary summary;
            if (!CurrentRetryOperations.TryGetValue(operationId, out summary))
            {
                CurrentRetryOperations[operationId] = new RetryOperationSummary { Status = status, BatchesCompleted = batchesCompleted, TotalBatches = totalBatches };
            }
            else
            {
                CurrentRetryOperations[operationId].Status = status;
                CurrentRetryOperations[operationId].BatchesCompleted = batchesCompleted;
                CurrentRetryOperations[operationId].TotalBatches = totalBatches;
            }
        }

        public static void SetStatus(string operationId, RetryOperationStatus status)
        {
            RetryOperationSummary summary;
            if (!CurrentRetryOperations.TryGetValue(operationId, out summary))
            {
                CurrentRetryOperations[operationId] = new RetryOperationSummary { Status = status };
            }
            else
            {
                CurrentRetryOperations[operationId].Status = status;
            }
        }

        public static RetryOperationSummary GetStatusForRetryOperation(string operationId)
        {
            RetryOperationSummary summary;
            CurrentRetryOperations.TryGetValue(operationId, out summary);

            return summary;
        }

        public RetryOperationStatus Status { get; set; }
        public int? BatchesCompleted { get; private set; }
        public int? TotalBatches { get; private set; }
    }

    // A different status enum, as the RetryBatchStatus doesn't quite give the
    // granularity that I think is needed. Could potentially merge together
    // at a later stage once the code is fleshed out better
    public enum RetryOperationStatus
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