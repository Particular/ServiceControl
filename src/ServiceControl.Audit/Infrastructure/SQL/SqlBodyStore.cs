namespace ServiceControl.Audit.Infrastructure.SQL
{
    using System.IO;
    using System.Threading.Tasks;

    class SqlBodyStore
    {
#pragma warning disable IDE0052 // Remove unread private members
        readonly string connectionString;
#pragma warning restore IDE0052 // Remove unread private members

        public SqlBodyStore(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public Task Store(string bodyId, string contentType, int bodySize, MemoryStream bodyStream)
        {
            throw new System.NotImplementedException();
        }

        public Task Initialize()
        {
            return Task.CompletedTask;
        }
    }
}