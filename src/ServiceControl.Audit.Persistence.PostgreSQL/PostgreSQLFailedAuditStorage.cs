namespace ServiceControl.Audit.Persistence.PostgreSQL;

using System;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Audit.Auditing;
using ServiceControl.Audit.Persistence;
class PostgreSQLFailedAuditStorage : IFailedAuditStorage
{
    public Task<int> GetFailedAuditsCount() => throw new NotImplementedException();
    public Task ProcessFailedMessages(Func<FailedTransportMessage, Func<CancellationToken, Task>, CancellationToken, Task> onMessage, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task SaveFailedAuditImport(FailedAuditImport message) => throw new NotImplementedException();
}
