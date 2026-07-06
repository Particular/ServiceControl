namespace ServiceControl.Persistence.RavenDB.Recoverability
{
    using ServiceControl.Recoverability;

    static class ArchiveOperationExtensions
    {
        // The in-memory progress state does not carry the audit attribution, so it is passed in
        // explicitly — the rebuilt document is stored over the original and must keep attributing
        // the operation (per-message audit entries are emitted from it when resuming after a restart).
        public static ArchiveOperation ToArchiveOperation(this InMemoryArchive a, string initiatedById, string initiatedByName, string operationId)
        {
            return new ArchiveOperation
            {
                ArchiveType = a.ArchiveType,
                GroupName = a.GroupName,
                Id = ArchiveOperation.MakeId(a.RequestId, a.ArchiveType),
                NumberOfMessagesArchived = a.NumberOfMessagesArchived,
                RequestId = a.RequestId,
                Started = a.Started,
                TotalNumberOfMessages = a.TotalNumberOfMessages,
                NumberOfBatches = a.NumberOfBatches,
                CurrentBatch = a.CurrentBatch,
                InitiatedById = initiatedById,
                InitiatedByName = initiatedByName,
                OperationId = operationId
            };
        }

        public static UnarchiveOperation ToUnarchiveOperation(this InMemoryUnarchive u, string initiatedById, string initiatedByName, string operationId)
        {
            return new UnarchiveOperation
            {
                ArchiveType = u.ArchiveType,
                GroupName = u.GroupName,
                Id = UnarchiveOperation.MakeId(u.RequestId, u.ArchiveType),
                NumberOfMessagesUnarchived = u.NumberOfMessagesUnarchived,
                RequestId = u.RequestId,
                Started = u.Started,
                TotalNumberOfMessages = u.TotalNumberOfMessages,
                NumberOfBatches = u.NumberOfBatches,
                CurrentBatch = u.CurrentBatch,
                InitiatedById = initiatedById,
                InitiatedByName = initiatedByName,
                OperationId = operationId
            };
        }
    }
}
