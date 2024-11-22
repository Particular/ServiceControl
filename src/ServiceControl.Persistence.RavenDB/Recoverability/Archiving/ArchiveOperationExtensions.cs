namespace ServiceControl.Persistence.RavenDB.Recoverability
{
    using ServiceControl.Recoverability;

    static class ArchiveOperationExtensions
    {
        public static ArchiveOperation ToArchiveOperation(this InMemoryArchive a)
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
                CurrentBatch = a.CurrentBatch
            };
        }

        public static UnarchiveOperation ToUnarchiveOperation(this InMemoryUnarchive u)
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
                CurrentBatch = u.CurrentBatch
            };
        }
    }
}