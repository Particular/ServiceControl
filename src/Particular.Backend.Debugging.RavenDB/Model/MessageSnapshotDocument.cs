namespace Particular.Backend.Debugging.RavenDB.Model
{
    using System;

    public class MessageSnapshotDocument : MessageSnapshot
    {
        public string Id { get; set; }
        public DateTime ProcessedAt { get; set; }

        public static string MakeDocumentId(string messageUniqueId)
        {
            return "AuditMessageSnapshots/" + messageUniqueId;
        }
    }
}