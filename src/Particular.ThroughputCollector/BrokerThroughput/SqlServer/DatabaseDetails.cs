namespace Particular.ThroughputQuery.SqlTransport
{
    using Microsoft.Data.SqlClient;

    public class DatabaseDetails
    {
        private readonly string connectionString;

        public string DatabaseName { get; }

        public DatabaseDetails(string connectionString)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder
                {
                    ConnectionString = connectionString,
                    TrustServerCertificate = true
                };
                DatabaseName = builder.InitialCatalog;
                this.connectionString = builder.ToString();
            }
            catch (Exception x) when (x is FormatException or ArgumentException)
            {
                throw new QueryException(QueryFailureReason.InvalidEnvironment, "There's something wrong with the SQL connection string and it could not be parsed.", x);
            }
        }

        public Task<string> TestConnection(CancellationToken cancellationToken = default)
        {
            try
            {
                return GetSqlVersion(cancellationToken);
            }
            catch (SqlException x) when (IsConnectionOrLoginIssue(x))
            {
                throw new QueryException(QueryFailureReason.Auth, "Could not access SQL database. Is the connection string correct?", x);
            }
        }

        static bool IsConnectionOrLoginIssue(SqlException x)
        {
            // Reference is here: https://learn.microsoft.com/en-us/previous-versions/sql/sql-server-2008-r2/cc645603(v=sql.105)?redirectedfrom=MSDN

            return x.Number switch
            {
                // Unproven or negative codes that need further "proof" here. If we get a false negative because of a localized exception message, so be it.
                // -2: Microsoft.Data.SqlClient.SqlException (0x80131904): Connection Timeout Expired.  The timeout period elapsed while attempting to consume the pre-login handshake acknowledgement.  This could be because the pre-login handshake failed or the server was unable to respond back in time.  The duration spent while attempting to connect to this server was - [Pre-Login] initialization=21041; handshake=4;
                -2 => x.Message.Contains("Connection Timeout Expired"),
                0 => x.Message.Contains("Failed to authenticate") || x.Message.Contains("server was not found"),


                // 10060: A network-related or instance-specific error occurred while establishing a connection to SQL Server. The server was not found or was not accessible. Verify that the instance name is correct and that SQL Server is configured to allow remote connections.
                // 10061: A network-related or instance-specific error occurred while establishing a connection to SQL Server. The server was not found or was not accessible. Verify that the instance name is correct and that SQL Server is configured to allow remote connections. (provider: TCP Provider, error: 0 - No connection could be made because the target machine actively refused it.
                10060 or 10061 => true,

                // 233: Named pipes: No process is on the other end of the pipe
                // 53: A network-related or instance-specific error occurred while establishing a connection to SQL Server
                // -2146893022: The target principal name is incorrect
                // 4060: Cannot open database "%.*ls" requested by the login. The login failed.
                // 4064: Cannot open user default database. Login failed.
                // 9758: Login protocol negotiation error occurred.
                // 14520: %s is not a valid SQL Server standard login, Windows NT user, Windows NT group, or msdb database role.
                // 15007: '%s' is not a valid login or you do not have permission.
                // 15537: Login '%.*ls' does not have access to server.
                // 15538: Login '%.*ls' does not have access to database.
                // 17197: Login failed due to timeout; the connection has been closed. This error may indicate heavy server load. Reduce the load on the server and retry login.%.*ls
                // 17892: Logon failed for login '%.*ls' due to trigger execution.%.*ls
                233 or 53 or -2146893022 or 4060 or 4064 or 9758 or 14520 or 15007 or 15537 or 15538 or 17197 or 17892 => true,

                // Pretty much every error in this range is login-related
                // https://learn.microsoft.com/en-us/previous-versions/sql/sql-server-2008-r2/cc645934(v=sql.105)
                >= 18301 and <= 18496 => true,

                // 21142: The SQL Server '%s' could not obtain Windows group membership information for login '%s'. Verify that the Windows account has access to the domain of the login.
                // 28034: Connection handshake failed.The login '%.*ls' does not have CONNECT permission on the endpoint.State % d.
                // 33041: Cannot create login token for existing authenticators. If dbo is a windows user make sure that its windows account information is accessible to SQL Server.
                21142 or 28034 or 33041 => true,

                // Everything else
                _ => false
            };
        }

        private async Task<string> GetSqlVersion(CancellationToken cancellationToken)
        {
            await using var conn = await OpenConnectionAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "select @@VERSION";

            return (string)await cmd.ExecuteScalarAsync(cancellationToken);
        }

        public async Task<List<QueueTableName>> GetTables(CancellationToken cancellationToken = default)
        {
            List<QueueTableName> tables = [];

            await using var conn = await OpenConnectionAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = GetQueueListCommandText;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var schema = reader.GetString(0);
                var name = reader.GetString(1);
                tables.Add(new QueueTableName(this, schema, name));
            }

            return tables;
        }

        public async Task<QueueTableSnapshot> GetSnapshot(QueueTableName queueTableName,
            CancellationToken cancellationToken = default)
        {
            var table = new QueueTableSnapshot(queueTableName);

            await using var conn = await OpenConnectionAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"select IDENT_CURRENT('{table.FullName}')";
            var value = await cmd.ExecuteScalarAsync(cancellationToken);

            if (value is decimal decimalValue) // That's the return type of IDENT_CURRENT
            {
                table.RowVersion = (long)decimalValue;
            }

            return table;
        }

        private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);
            return conn;
        }

        /// <summary>
        /// Query works by finidng all the columns in any table that *could* be from an NServiceBus
        /// queue table, grouping by schema+name, and then using the HAVING COUNT(*) = 8 clause
        /// to ensure that all 8 columns are represented. Delay tables, for example, will match
        /// on 3 of the columns (Headers, Body, RowVersion) and many user tables might have an
        /// Id column, but the HAVING clause filters these out.
        /// </summary>
        const string GetQueueListCommandText = @"SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED

SELECT C.TABLE_SCHEMA as TableSchema, C.TABLE_NAME as TableName
FROM [INFORMATION_SCHEMA].[COLUMNS] C
WHERE
    (C.COLUMN_NAME = 'Id' AND C.DATA_TYPE = 'uniqueidentifier') OR
    (C.COLUMN_NAME = 'CorrelationId' AND C.DATA_TYPE = 'varchar') OR
    (C.COLUMN_NAME = 'ReplyToAddress' AND C.DATA_TYPE = 'varchar') OR
    (C.COLUMN_NAME = 'Recoverable' AND C.DATA_TYPE = 'bit') OR
    (C.COLUMN_NAME = 'Expires' AND C.DATA_TYPE = 'datetime') OR
    (C.COLUMN_NAME = 'Headers') OR
    (C.COLUMN_NAME = 'Body' AND C.DATA_TYPE = 'varbinary') OR
    (C.COLUMN_NAME = 'RowVersion' AND C.DATA_TYPE = 'bigint')
GROUP BY C.TABLE_SCHEMA, C.TABLE_NAME
HAVING COUNT(*) = 8";
    }
}