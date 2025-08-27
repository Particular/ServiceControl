namespace ServiceControl.Audit.Persistence.PostgreSQL.BodyStorage
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Auditing.BodyStorage;

    class PostgreSQLAttachmentsBodyStorage : IBodyStorage
    {
        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken) => throw new System.NotImplementedException();
        public Task<StreamResult> TryFetch(string bodyId, CancellationToken cancellationToken) => throw new System.NotImplementedException();
    }
}
