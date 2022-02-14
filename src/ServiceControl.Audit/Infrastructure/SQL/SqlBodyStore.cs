namespace ServiceControl.Audit.Infrastructure.SQL
{
    using System.Data.SqlClient;
    using System.IO;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Dapper;

    class SqlBodyStore : IBodyStorage
    {
        readonly string connectionString;

        public SqlBodyStore(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public Task Initialize()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                return connection.ExecuteAsync(SqlConstants.CreateBodiesTable);
            }
        }

        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream) => throw new System.NotImplementedException();

        public Task<StreamResult> TryFetch(string bodyId) => throw new System.NotImplementedException();
    }
}