namespace ServiceControl.Audit.Infrastructure.SQL
{
    using System.IO;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;

    class SqlBodyStore : IBodyStorage
    {
        public Task Store(string bodyId, string contentType, int bodySize, MemoryStream bodyStream)
        {
            throw new System.NotImplementedException();
        }

        public Task Initialize()
        {
            return Task.CompletedTask;
        }

        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream) => throw new System.NotImplementedException();

        public Task<StreamResult> TryFetch(string bodyId) => throw new System.NotImplementedException();
    }
}