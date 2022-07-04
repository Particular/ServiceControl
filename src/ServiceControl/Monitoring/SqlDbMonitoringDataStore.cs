namespace ServiceControl.Monitoring
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using Dapper;
    using Infrastructure;

    class SqlDbMonitoringDataStore : IMonitoringDataStore
    {
        readonly EndpointInstanceMonitoring monitoring;

        public SqlDbMonitoringDataStore(EndpointInstanceMonitoring monitoring)
        {
            this.monitoring = monitoring;
        }

        public async Task CreateIfNotExists(EndpointDetails endpoint)
        {
            var connectionString = Environment.GetEnvironmentVariable("SQLServerConnectionString");

            var id = DeterministicGuid.MakeId(endpoint.Name, endpoint.HostId.ToString());

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                await connection.ExecuteAsync(
                    @"INSERT INTO [KnownEndpoints](Id, HostId, Host, HostDisplayName, Monitored) 
                          VALUES(@Id, @HostId, @Host, @HostDisplayName, @Monitored)",
                    new
                    {
                        Id = id,
                        endpoint.HostId,
                        endpoint.Host,
                        HostDisplayName = endpoint.Name,
                        Monitored = false
                    }).ConfigureAwait(false);
            }
        }

        public async Task CreateOrUpdate(EndpointDetails endpoint)
        {
            var connectionString = Environment.GetEnvironmentVariable("SQLServerConnectionString");

            var id = DeterministicGuid.MakeId(endpoint.Name, endpoint.HostId.ToString());

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                await connection.ExecuteAsync(
                    @"IF EXISTS(SELECT * FROM [KnownEndpoints] WHERE Id = @Id)
                            UPDATE [KnownEndpoints] SET [Monitored] = @Monitored WHERE [Id] = @Id
                          ELSE
                            INSERT INTO [KnownEndpoints](Id, HostId, Host, HostDisplayName, Monitored) 
                            VALUES(@Id, @HostId, @Host, @HostDisplayName, 1)
                          END",
                    new
                    {
                        Id = id,
                        endpoint.HostId,
                        endpoint.Host,
                        HostDisplayName = endpoint.Name,
                        Monitored = monitoring.IsMonitored(id)
                    }).ConfigureAwait(false);
            }
        }

        public async Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored)
        {
            var connectionString = Environment.GetEnvironmentVariable("SQLServerConnectionString");

            var id = DeterministicGuid.MakeId(endpoint.Name, endpoint.HostId.ToString());

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                await connection.ExecuteAsync(
                    @"UPDATE [KnownEndpoints] SET [Monitored] = @Monitored WHERE [Id] = @Id",
                    new
                    {
                        Id = id,
                        Monitored = isMonitored
                    }).ConfigureAwait(false);
            }
        }

        public async Task WarmupMonitoringFromPersistence()
        {
            var connectionString = Environment.GetEnvironmentVariable("SQLServerConnectionString");

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                var rows = await connection.QueryAsync("SELECT * FROM [KnownEndpoints]").ConfigureAwait(false);

                foreach (dynamic row in rows)
                {
                    monitoring.DetectEndpointFromPersistentStore(new EndpointDetails
                    {
                        HostId = row.HostId,
                        Host = row.Host,
                        Name = row.HostDisplayName
                    }, row.Monitored);
                }
            }
        }
    }
}