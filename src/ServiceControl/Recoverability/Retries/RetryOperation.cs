namespace ServiceControl.Recoverability
{
    public class RetryOperation
    {
        public string Id { get; set; }
        public string GroupId { get; set; }
        public int BatchesRemaining { get; set; }
        public int BatchesInOperation { get; set; }

        public static string MakeDocumentId(string messageUniqueId)
        {
            return "RetryOperations/" + messageUniqueId;
        }
    }
}
