namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;

    public interface IFailedAuditStorage
    {
        Task Store(dynamic failure);

        Task SaveFailedAuditImport(FailedAuditImport message);

        Task ProcessFailedMessages(
            Func<FailedTransportMessage, Func<CancellationToken, Task>, CancellationToken, Task> onMessage,
            CancellationToken cancellationToken);

        Task<int> GetFailedAuditsCount();
    }
}