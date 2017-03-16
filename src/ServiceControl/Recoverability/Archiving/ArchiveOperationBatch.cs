namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;

    public class ArchiveOperationBatch
    {
        public string Id { get; set; }
        public List<string> DocumentIds { get; set; } = new List<string>();

        public static string MakeId(string requestId, ArchiveType archiveType, int batchNumber)
        {
            return $"ArchiveOperations/{(int)archiveType}/{requestId}/{batchNumber}";
        }
    }
}