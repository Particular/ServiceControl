namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;

    class UnarchiveBatch //raven
    {
        public string Id { get; set; }
        public List<string> DocumentIds { get; set; } = new List<string>();

        public static string MakeId(string requestId, ArchiveType archiveType, int batchNumber)
        {
            return $"UnarchiveOperations/{(int)archiveType}/{requestId}/{batchNumber}";
        }
    }
}