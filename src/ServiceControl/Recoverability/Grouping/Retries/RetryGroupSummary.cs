
namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;

    public class RetryGroupSummary
    {
        public static Dictionary<string, RetryGroupSummary> CurrentRetryGroups = new Dictionary<string, RetryGroupSummary>();

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

        public static RetryGroupStatus GetStatusForGroup(string groupId)
        {
            RetryGroupSummary summary;
            if (CurrentRetryGroups.TryGetValue(groupId, out summary))
            {
                return summary.Status;
            }

            return RetryGroupStatus.Open;
        }

        public RetryGroupStatus Status { get; set; }
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
        Forwarded
    }
}