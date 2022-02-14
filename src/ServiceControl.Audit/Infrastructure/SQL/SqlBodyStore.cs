namespace ServiceControl.Audit.Infrastructure.SQL
{
    using System.Data;
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

        public async Task Initialize()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.ExecuteAsync(SqlConstants.CreateBodiesTable).ConfigureAwait(false);
            }
        }

        public async Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                string insertText = @"
IF NOT EXISTS (SELECT * FROM [dbo].[Bodies] 
               WHERE [MessageId] = @MessageId)
BEGIN 
    INSERT INTO [dbo].[Bodies] (MessageId, BodyText) VALUES (@MessageId, @BodyText)
END";

                using (var command = new SqlCommand(insertText, connection))
                {
                    command.Parameters.Add("@MessageId", SqlDbType.NVarChar, 100).Value = bodyId;
                    command.Parameters.Add("@BodyText", SqlDbType.NVarChar, -1).Value = new StreamReader(bodyStream);

                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        public Task<StreamResult> TryFetch(string bodyId) => throw new System.NotImplementedException();
    }
}