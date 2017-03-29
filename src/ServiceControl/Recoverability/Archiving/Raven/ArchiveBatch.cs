namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;

    public class ArchiveBatch //raven
    {
        public string Id { get; set; }
        public List<string> DocumentIds { get; set; } = new List<string>();

        public static string MakeId(string requestId, ArchiveType archiveType, int batchNumber)
        {
            return $"ArchiveBatch/{(int)archiveType}/{requestId}/{batchNumber}";
        }
    }
}