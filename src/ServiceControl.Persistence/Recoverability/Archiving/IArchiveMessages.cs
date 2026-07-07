namespace ServiceControl.Persistence.Recoverability
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Infrastructure.Auth;
    using ServiceControl.Recoverability;

    /// <summary>
    /// Implementers of this interface are expected to emit domain events as well
    /// </summary>
    public interface IArchiveMessages
    {
        Task ArchiveAllInGroup(string groupId, AuditUser? initiatedBy = null, string operationId = null);
        Task UnarchiveAllInGroup(string groupId, AuditUser? initiatedBy = null, string operationId = null);

        bool IsOperationInProgressFor(string groupId, ArchiveType archiveType);

        bool IsArchiveInProgressFor(string groupId);
        void DismissArchiveOperation(string groupId, ArchiveType archiveType);

        Task StartArchiving(string groupId, ArchiveType archiveType);
        Task StartUnarchiving(string groupId, ArchiveType archiveType);

        IEnumerable<InMemoryArchive> GetArchivalOperations();
    }
}
