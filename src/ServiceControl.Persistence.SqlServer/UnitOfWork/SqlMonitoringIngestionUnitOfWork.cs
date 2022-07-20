namespace ServiceControl.Persistence.SqlServer
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Dapper;
    using Monitoring;
    using Operations;

    class SqlMonitoringIngestionUnitOfWork : IMonitoringIngestionUnitOfWork
    {
        readonly SqlConnection connection;
        readonly SqlTransaction transaction;

        public SqlMonitoringIngestionUnitOfWork(SqlConnection connection, SqlTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
        }

        public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint)
            => connection.ExecuteAsync(
                @"IF NOT EXISTS(SELECT * FROM [KnownEndpoints] WHERE Id = @Id)
                      BEGIN
                        INSERT INTO [KnownEndpoints](Id, HostId, Host, HostDisplayName, Monitored) 
                        VALUES(@Id, @HostId, @Host, @HostDisplayName, @Monitored)
                      END",
                new
                {
                    knownEndpoint.Id,
                    knownEndpoint.EndpointDetails.HostId,
                    knownEndpoint.EndpointDetails.Host,
                    HostDisplayName = knownEndpoint.EndpointDetails.Name,
                    knownEndpoint.Monitored
                }, transaction);
    }
}