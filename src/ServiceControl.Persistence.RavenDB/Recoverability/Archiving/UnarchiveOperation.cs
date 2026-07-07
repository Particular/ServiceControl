namespace ServiceControl.Recoverability
{
    using System;

    class UnarchiveOperation // raven
    {
        public string Id { get; set; }
        public string RequestId { get; set; }
        public string GroupName { get; set; }
        public ArchiveType ArchiveType { get; set; }
        public int TotalNumberOfMessages { get; set; }
        public int NumberOfMessagesUnarchived { get; set; }
        public DateTime Started { get; set; }
        public int NumberOfBatches { get; set; }
        public int CurrentBatch { get; set; }

        // Audit attribution for the initiating operation, carried so per-message audit entries can be
        // emitted (and correlated to the operation) as each batch is unarchived, including after a restart.
        public string InitiatedById { get; set; }
        public string InitiatedByName { get; set; }
        public string OperationId { get; set; }

        public static string MakeId(string requestId, ArchiveType archiveType)
        {
            return $"UnarchiveOperations/{(int)archiveType}/{requestId}";
        }
    }
}