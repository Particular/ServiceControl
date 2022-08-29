namespace ServiceControl.Audit.Persistence
{
    using System.Threading;
    using System;
    using System.Threading.Tasks;
    using Auditing;

    interface IFailedAuditStorage
    {
        Task Store(dynamic failure);

        Task ProcessFailedMessages(
            Func<FailedTransportMessage, Func<CancellationToken, Task>, CancellationToken, Task> onMessage,
            CancellationToken cancellationToken);
    }
}