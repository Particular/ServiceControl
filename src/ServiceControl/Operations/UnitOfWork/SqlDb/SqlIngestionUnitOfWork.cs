namespace ServiceControl.Operations
{
    using System.Collections.Concurrent;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Dapper;
    using Monitoring;

    class SqlIngestionUnitOfWork : IIngestionUnitOfWork
    {
        readonly SqlConnection connection;
        ConcurrentBag<KnownEndpoint> knownEndpoints = new ConcurrentBag<KnownEndpoint>();

        public SqlIngestionUnitOfWork(SqlConnection connection)
        {
            this.connection = connection;
            Monitoring = new SqlMonitoringIngestionUnitOfWork(connection);
        }

        public IMonitoringIngestionUnitOfWork Monitoring { get; }
        public IRecoverabilityIngestionUnitOfWork Recoverability { get; }

        public async Task Complete()
        {
            foreach (var endpoint in knownEndpoints)
            {
                await connection.ExecuteAsync(
                    @"IF NOT EXISTS(SELECT * FROM [KnownEndpoints] WHERE Id = @Id)
                      BEGIN
                        INSERT INTO [KnownEndpoints](Id, HostId, Host, HostDisplayName, Monitored) 
                        VALUES(@Id, @HostId, @Host, @HostDisplayName, @Monitored)
                      END",
                    new
                    {
                        endpoint.Id,
                        endpoint.EndpointDetails.HostId,
                        endpoint.EndpointDetails.Host,
                        HostDisplayName = endpoint.EndpointDetails.Name,
                        endpoint.Monitored
                    }).ConfigureAwait(false);
            }
        }

        internal void AddEndpoint(KnownEndpoint knownEndpoint) => knownEndpoints.Add(knownEndpoint);

        public void Dispose() => connection?.Dispose();
    }
}