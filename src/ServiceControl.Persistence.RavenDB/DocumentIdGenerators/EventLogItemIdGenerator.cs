namespace ServiceControl.Persistence.RavenDB
{
    using System;

    static class EventLogItemIdGenerator
    {
        public const string DocumentIdPrefix = "EventLogItem";

        public static string MakeDocumentId(string category, string eventType, Guid eventId) =>
            $"{DocumentIdPrefix}/{category}/{eventType}/{eventId}";

        // The final segment is safe: a category is a namespace segment and an event type a type
        // name, so neither can contain a separator.
        public static Guid GetEventIdFromDocumentId(string documentId) =>
            Guid.Parse(documentId[(documentId.LastIndexOf('/') + 1)..]);
    }
}
