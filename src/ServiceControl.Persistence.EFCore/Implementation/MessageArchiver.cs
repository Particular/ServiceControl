namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Infrastructure.Auth;
using ServiceControl.Persistence.Recoverability;
using ServiceControl.Recoverability;

public class MessageArchiver : IArchiveMessages
{
    public Task ArchiveAllInGroup(string groupId, AuditUser? initiatedBy = null, string? operationId = null) =>
        throw new NotImplementedException();

    public Task UnarchiveAllInGroup(string groupId, AuditUser? initiatedBy = null, string? operationId = null) =>
        throw new NotImplementedException();

    public bool IsOperationInProgressFor(string groupId, ArchiveType archiveType) =>
        throw new NotImplementedException();

    public bool IsArchiveInProgressFor(string groupId) =>
        throw new NotImplementedException();

    public void DismissArchiveOperation(string groupId, ArchiveType archiveType) =>
        throw new NotImplementedException();

    public Task StartArchiving(string groupId, ArchiveType archiveType) =>
        throw new NotImplementedException();

    public Task StartUnarchiving(string groupId, ArchiveType archiveType) =>
        throw new NotImplementedException();

    public IEnumerable<InMemoryArchive> GetArchivalOperations() =>
        throw new NotImplementedException();
}
