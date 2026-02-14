namespace ServiceControl.Audit.Persistence.Sql.Core.Implementation;

using ServiceControl.Audit.Auditing;

class EFFailedAuditStorage : IFailedAuditStorage
{
    public Task SaveFailedAuditImport(FailedAuditImport message)
        => Task.CompletedTask;

    public Task ProcessFailedMessages(
        Func<FailedTransportMessage, Func<CancellationToken, Task>, CancellationToken, Task> onMessage,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task<int> GetFailedAuditsCount()
        => Task.FromResult(0);
}
