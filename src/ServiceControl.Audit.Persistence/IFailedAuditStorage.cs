namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;

    public interface IFailedAuditStorage
    {
        Task SaveFailedAuditImport(FailedAuditImport message, CancellationToken cancellationToken = default);

        Task ProcessFailedMessages(
            Func<FailedTransportMessage, Func<CancellationToken, Task>, CancellationToken, Task> onMessage,
            CancellationToken cancellationToken = default
        );

        Task<int> GetFailedAuditsCount(CancellationToken cancellationToken = default);
    }
}