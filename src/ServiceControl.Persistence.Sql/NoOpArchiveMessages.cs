namespace ServiceControl.Persistence.Sql;

using System.Collections.Generic;
using System.Threading.Tasks;
using ServiceControl.Persistence.Recoverability;
using ServiceControl.Recoverability;

class NoOpArchiveMessages : IArchiveMessages
{
    public Task ArchiveAllInGroup(string groupId) => Task.CompletedTask;

    public Task UnarchiveAllInGroup(string groupId) => Task.CompletedTask;

    public bool IsOperationInProgressFor(string groupId, ArchiveType archiveType) => false;

    public bool IsArchiveInProgressFor(string groupId) => false;

    public void DismissArchiveOperation(string groupId, ArchiveType archiveType)
    {
    }

    public Task StartArchiving(string groupId, ArchiveType archiveType) => Task.CompletedTask;

    public Task StartUnarchiving(string groupId, ArchiveType archiveType) => Task.CompletedTask;

    public IEnumerable<InMemoryArchive> GetArchivalOperations() => [];
}
