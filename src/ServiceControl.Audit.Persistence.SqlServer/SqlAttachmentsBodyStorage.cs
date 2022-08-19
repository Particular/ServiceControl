namespace ServiceControl.Audit.Persistence.SqlServer
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Dapper;
    using ServiceControl.Audit.Auditing.BodyStorage;

    class SqlAttachmentsBodyStorage : IBodyStorage
    {
        readonly SqlDbConnectionManager connectionManager;

        public SqlAttachmentsBodyStorage(SqlDbConnectionManager connectionManager)
        {
            this.connectionManager = connectionManager;
        }


        public async Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
        {
            await connectionManager.Perform(connection =>
            {
                throw new NotImplementedException();
            }).ConfigureAwait(false);
        }

        public async Task<StreamResult> TryFetch(string bodyId)
        {
            StreamResult result = default;
            await connectionManager.Perform(async connection =>
            {
                var rows = await connection.QueryAsync(
                    @"SELECT * FROM [dbo].[MessageBodies] WHERE BodyId] = @Id",
                    new
                    {
                        Id = bodyId
                    }).ConfigureAwait(false);

                result = rows.AsList().Count > 0
                ? new StreamResult
                {
                    HasResult = false,
                    Stream = null
                }
                : new StreamResult
                {
                    HasResult = true,
                    Stream = rows.AsList()[0].BodyStream,
                    ContentType = rows.AsList()[0].ContentType,
                    BodySize = rows.AsList()[0].BodySize,
                    Etag = string.Empty
                };

            }).ConfigureAwait(false);

            return result;
        }
    }
}