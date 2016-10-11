namespace ServiceControl.Recoverability
{
    public class RetryOperation
    {
        public string Id { get; set; }
        public string GroupId { get; set; }
        public int BatchesRemaining { get; set; }
        public int BatchesInOperation { get; set; }
        
        public static string MakeDocumentIdForFailureGroup(string groupId)
        {
            return "RetryOperations/fmg-" + groupId;
        }

        public int GetCompletedBatchesInOperation()
        {
            return BatchesInOperation - BatchesRemaining;;
        }
        
        public void ForwardBatch(out bool allBatchesForwarded)
        {
            BatchesRemaining--;
            allBatchesForwarded = BatchesRemaining == 0;
        }
    
    }
}