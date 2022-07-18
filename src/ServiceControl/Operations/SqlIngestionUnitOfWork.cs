namespace ServiceControl.Operations
{
    using System.Collections.Concurrent;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Dapper;
    using Monitoring;

    class SqlIngestionUnitOfWork : IIngestionUnitOfWork
    {
        readonly string connectionString;
        ConcurrentBag<KnownEndpoint> knownEndpoints = new ConcurrentBag<KnownEndpoint>();

        public SqlIngestionUnitOfWork(string connectionString)
        {
            this.connectionString = connectionString;
            Monitoring = new SqlMonitoringIngestionUnitOfWork(this);
        }

        public IMonitoringIngestionUnitOfWork Monitoring { get; }
        public IRecoverabilityIngestionUnitOfWork Recoverability { get; }

        public async Task Complete()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

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
        }

        internal void AddEndpoint(KnownEndpoint knownEndpoint) => knownEndpoints.Add(knownEndpoint);
    }
}