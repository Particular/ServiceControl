namespace ServiceControl.Transports.PostgreSql
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;

    public class DatabaseDetails
    {
        readonly string connectionString;

        public string DatabaseName { get; }

        public DatabaseDetails(string connectionString)
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder
                {
                    ConnectionString = connectionString
                };
                DatabaseName = builder.Database;
                this.connectionString = builder.ToString();
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                throw new Exception("PostgreSQL Connection String could not be parsed.", ex);
            }
        }

        public Task<string> TestConnection(CancellationToken cancellationToken)
        {
            try
            {
                return GetPostgreSqlVersion(cancellationToken);
            }
            catch (NpgsqlException ex) when (IsConnectionOrLoginIssue(ex))
            {
                throw new Exception($"Could not connect to '{DatabaseName}' PostgreSQL database.", ex);
            }
        }

        static bool IsConnectionOrLoginIssue(NpgsqlException x)
        {
            //TODO postgres - any other errors?
            // Reference is here: https://www.postgresql.org/docs/current/errcodes-appendix.html

            return x.SqlState switch
            {
                //28000   invalid_authorization_specification
                //28P01   invalid_password
                "28000" or "28P01" => true,

                //08000   connection_exception
                //08003   connection_does_not_exist
                //08006   connection_failure
                //08001   sqlclient_unable_to_establish_sqlconnection
                //08004   sqlserver_rejected_establishment_of_sqlconnection
                //08007   transaction_resolution_unknown
                //08P01   protocol_violation
                "08000" or "08003" or "08006" or "08001" or "08004" or "08007" or "08P01" => true,

                // Everything else
                _ => false
            };
        }

        async Task<string> GetPostgreSqlVersion(CancellationToken cancellationToken)
        {
            await using var conn = await OpenConnectionAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT version()";

            return (string)await cmd.ExecuteScalarAsync(cancellationToken);
        }

        public async Task<List<BrokerQueueTable>> GetTables(CancellationToken cancellationToken = default)
        {
            List<BrokerQueueTable> tables = [];

            await using var conn = await OpenConnectionAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = GetQueueListCommandText;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var schema = reader.GetString(0);
                var name = reader.GetString(1);
                tables.Add(new BrokerQueueTable(this, new QueueAddress(name, schema)));
            }

            return tables;
        }

        public async Task<BrokerQueueTableSnapshot> GetSnapshot(BrokerQueueTable brokerQueueTable,
            CancellationToken cancellationToken = default)
        {
            var table = new BrokerQueueTableSnapshot(brokerQueueTable);

            await using var conn = await OpenConnectionAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"select last_value from \"{table.SequenceName}\";";
            var value = await cmd.ExecuteScalarAsync(cancellationToken);

            if (value is long longValue)
            {
                table.RowVersion = longValue;
            }

            return table;
        }

        async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);
            return conn;
        }


        /// <summary>
        /// Query works by finidng all the columns in any table that *could* be from an NServiceBus
        /// queue table, grouping by schema+name, and then using the HAVING COUNT(*) = 5 clause
        /// to ensure that all 5 columns are represented. Delay tables, for example, will match
        /// on 3 of the columns (Headers, Body, RowVersion) and many user tables might have an
        /// Id column, but the HAVING clause filters these out.
        /// </summary>        /// 
        const string GetQueueListCommandText = @"
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT C.TABLE_SCHEMA as TableSchema, C.TABLE_NAME as TableName
FROM information_schema.columns C
WHERE
    (C.COLUMN_NAME = 'id' AND C.DATA_TYPE = 'uuid') OR
    (C.COLUMN_NAME = 'expires' AND C.DATA_TYPE = 'timestamp without time zone') OR
    (C.COLUMN_NAME = 'headers' AND C.DATA_TYPE = 'text') OR
    (C.COLUMN_NAME = 'body' AND C.DATA_TYPE = 'bytea') OR
    (C.COLUMN_NAME = 'seq' AND C.DATA_TYPE = 'integer')
GROUP BY C.TABLE_SCHEMA, C.TABLE_NAME
HAVING COUNT(*) = 5
";
    }
}