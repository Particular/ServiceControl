namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;

    class OperationsManager
    {
        public bool IsOperationInProgressFor(string requestId, ArchiveType archiveType)
        {
            var isUnarchiveOpration = UnarchiveOperations.TryGetValue(InMemoryUnarchive.MakeId(requestId, archiveType),
                out var unarchiveSummary);
            if (!ArchiveOperations.TryGetValue(InMemoryArchive.MakeId(requestId, archiveType), out var archiveSummary) && !isUnarchiveOpration)
            {
                return false;
            }

            return archiveSummary?.ArchiveState != ArchiveState.ArchiveCompleted && isUnarchiveOpration && unarchiveSummary.ArchiveState != ArchiveState.ArchiveCompleted;
        }

        public Dictionary<string, InMemoryUnarchive> UnarchiveOperations { get; } = new Dictionary<string, InMemoryUnarchive>();

        public Dictionary<string, InMemoryArchive> ArchiveOperations { get; } = new Dictionary<string, InMemoryArchive>();
    }
}