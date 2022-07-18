namespace ServiceControl.Operations
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Dapper;
    using Monitoring;

    class SqlMonitoringIngestionUnitOfWork : IMonitoringIngestionUnitOfWork
    {
        readonly SqlConnection connection;

        public SqlMonitoringIngestionUnitOfWork(SqlConnection connection)
            => this.connection = connection;

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
                });
    }
}