#nullable enable
namespace ServiceControl.UnitTests.Recoverability;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Infrastructure.Auth;
using ServiceControl.Persistence.Recoverability;
using ServiceControl.Recoverability;

sealed class NoopArchiveMessages : IArchiveMessages
{
    public bool OperationInProgress { get; init; }

    public Task ArchiveAllInGroup(string groupId, AuditUser? initiatedBy = null, string? operationId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UnarchiveAllInGroup(string groupId, AuditUser? initiatedBy = null, string? operationId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public bool IsOperationInProgressFor(string groupId, ArchiveType archiveType) => OperationInProgress;

    public bool IsArchiveInProgressFor(string groupId) => false;

    public void DismissArchiveOperation(string groupId, ArchiveType archiveType)
    {
    }

    public Task StartArchiving(string groupId, ArchiveType archiveType, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartUnarchiving(string groupId, ArchiveType archiveType, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IEnumerable<InMemoryArchive> GetArchivalOperations() => [];
}
