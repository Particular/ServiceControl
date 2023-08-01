namespace NServiceBus
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Transport.Msmq.DelayedDelivery.Sql;

    //TODO: Either we should expect that the connection created by the factory is open of we should make it non-async
    /// <summary>
    /// Factory method for creating SQL Server connections.
    /// </summary>
    public delegate Task<SqlConnection> CreateSqlConnection(CancellationToken cancellationToken = default);

    /// <summary>
    /// Implementation of the delayed message store based on the SQL Server.
    /// </summary>
    public class SqlServerDelayedMessageStore : IDelayedMessageStore
    {
        string schema;
        string tableName;
        CreateSqlConnection createSqlConnection;

        string insertCommand;
        string removeCommand;
        string bumpFailureCountCommand;
        string nextCommand;
        string fetchCommand;

        /// <summary>
        /// Creates a new instance of the SQL Server delayed message store.
        /// </summary>
        /// <param name="connectionString">Connection string to the SQL Server database.</param>
        /// <param name="schema">(optional) schema to use. Defaults to dbo</param>
        /// <param name="tableName">(optional) name of the table where delayed messages are stored. Defaults to name of the endpoint with .Delayed suffix.</param>
        public SqlServerDelayedMessageStore(string connectionString, string schema = null, string tableName = null)
            : this(token => Task.FromResult(new SqlConnection(connectionString)), schema, tableName)
        {
        }

        /// <summary>
        /// Creates a new instance of the SQL Server delayed message store.
        /// </summary>
        /// <param name="connectionFactory">Factory for database connections.</param>
        /// <param name="schema">(optional) schema to use. Defaults to dbo</param>
        /// <param name="tableName">(optional) name of the table where delayed messages are stored. Defaults to name of the endpoint with .Delayed suffix.</param>
        public SqlServerDelayedMessageStore(CreateSqlConnection connectionFactory, string schema = null, string tableName = null)
        {
            createSqlConnection = connectionFactory;
            this.tableName = tableName;
            this.schema = schema ?? "dbo";
        }

        /// <inheritdoc />
        public async Task Store(DelayedMessage timeout, CancellationToken cancellationToken = default)
        {
            using (var cn = await createSqlConnection(cancellationToken).ConfigureAwait(false))
            using (var cmd = new SqlCommand(insertCommand, cn))
            {
                cmd.Parameters.AddWithValue("@id", timeout.MessageId);
                cmd.Parameters.AddWithValue("@destination", timeout.Destination);
                cmd.Parameters.AddWithValue("@time", timeout.Time);
                cmd.Parameters.AddWithValue("@headers", timeout.Headers);
                cmd.Parameters.AddWithValue("@state", timeout.Body);
                await cn.OpenAsync(cancellationToken).ConfigureAwait(false);
                _ = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }


        /// <inheritdoc />
        public async Task<bool> Remove(DelayedMessage timeout, CancellationToken cancellationToken = default)
        {
            using (var cn = await createSqlConnection(cancellationToken).ConfigureAwait(false))
            using (var cmd = new SqlCommand(removeCommand, cn))
            {
                cmd.Parameters.AddWithValue("@id", timeout.MessageId);
                await cn.OpenAsync(cancellationToken).ConfigureAwait(false);
                var affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return affected == 1;
            }
        }

        /// <inheritdoc />
        public async Task<bool> IncrementFailureCount(DelayedMessage timeout, CancellationToken cancellationToken = default)
        {
            using (var cn = await createSqlConnection(cancellationToken).ConfigureAwait(false))
            using (var cmd = new SqlCommand(bumpFailureCountCommand, cn))
            {
                cmd.Parameters.AddWithValue("@id", timeout.MessageId);
                await cn.OpenAsync(cancellationToken).ConfigureAwait(false);
                var affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return affected == 1;
            }
        }

        /// <inheritdoc />
        public async Task Initialize(string queueName, TransportTransactionMode transactionMode, CancellationToken cancellationToken = default)
        {
            if (tableName == null)
            {
                tableName = $"{queueName}.timeouts";
            }

            var quotedFullName = $"{SqlNameHelper.Quote(schema)}.{SqlNameHelper.Quote(tableName)}";

            var creator = new TimeoutTableCreator(createSqlConnection, quotedFullName);
            await creator.CreateIfNecessary(cancellationToken).ConfigureAwait(false);

            insertCommand = string.Format(SqlConstants.SqlInsert, quotedFullName);
            removeCommand = string.Format(SqlConstants.SqlDelete, quotedFullName);
            bumpFailureCountCommand = string.Format(SqlConstants.SqlUpdate, quotedFullName);
            nextCommand = string.Format(SqlConstants.SqlGetNext, quotedFullName);
            fetchCommand = string.Format(SqlConstants.SqlFetch, quotedFullName);
        }

        /// <inheritdoc />
        public async Task<DateTimeOffset?> Next(CancellationToken cancellationToken = default)
        {
            using (var cn = await createSqlConnection(cancellationToken).ConfigureAwait(false))
            using (var cmd = new SqlCommand(nextCommand, cn))
            {
                await cn.OpenAsync(cancellationToken).ConfigureAwait(false);
                var result = (DateTime?)await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                return result.HasValue ? (DateTimeOffset?)new DateTimeOffset(result.Value, TimeSpan.Zero) : null;
            }
        }

        /// <inheritdoc />
        public async Task<DelayedMessage> FetchNextDueTimeout(DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            DelayedMessage result = null;
            using (var cn = await createSqlConnection(cancellationToken).ConfigureAwait(false))
            using (var cmd = new SqlCommand(fetchCommand, cn))
            {
                cmd.Parameters.AddWithValue("@time", at.UtcDateTime);

                await cn.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        result = new DelayedMessage
                        {
                            MessageId = (string)reader[0],
                            Destination = (string)reader[1],
                            Time = (DateTime)reader[2],
                            Headers = (byte[])reader[3],
                            Body = (byte[])reader[4],
                            NumberOfRetries = (int)reader[5]
                        };
                    }
                }
            }

            return result;
        }
    }
}