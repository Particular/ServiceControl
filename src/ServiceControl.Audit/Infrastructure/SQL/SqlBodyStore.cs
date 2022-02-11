namespace ServiceControl.Audit.Infrastructure.SQL
{
    using System.IO;
    using System.Threading.Tasks;

    class SqlBodyStore
    {
        public Task Store(string bodyId, string contentType, int bodySize, MemoryStream bodyStream)
        {
            throw new System.NotImplementedException();
        }
    }
}