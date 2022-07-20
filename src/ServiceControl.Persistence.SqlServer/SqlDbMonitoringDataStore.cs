namespace ServiceControl.Persistence.SqlServer
{
    using System;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using Dapper;
    using Infrastructure;
    using ServiceControl.Monitoring;
    using ServiceControl.Persistence;

    class SqlDbMonitoringDataStore : IMonitoringDataStore
    {
        readonly string connectionString;

        public SqlDbMonitoringDataStore(SqlDbConnectionManager connectionManager)
        {
            connectionString = connectionManager.ConnectionString;
        }

        public async Task CreateIfNotExists(EndpointDetails endpoint)
        {
            var id = DeterministicGuid.MakeId(endpoint.Name, endpoint.HostId.ToString());

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                await connection.ExecuteAsync(
                    @"IF NOT EXISTS(SELECT * FROM [KnownEndpoints] WHERE Id = @Id)
                          BEGIN
                            INSERT INTO [KnownEndpoints](Id, HostId, Host, HostDisplayName, Monitored) 
                            VALUES(@Id, @HostId, @Host, @HostDisplayName, @Monitored)
                          END",
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

        public async Task CreateOrUpdate(EndpointDetails endpoint, EndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            var id = DeterministicGuid.MakeId(endpoint.Name, endpoint.HostId.ToString());

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                await connection.ExecuteAsync(
                    @"IF EXISTS(SELECT * FROM [KnownEndpoints] WHERE Id = @Id)
                            UPDATE [KnownEndpoints] SET [Monitored] = @Monitored WHERE [Id] = @Id
                          ELSE
                            INSERT INTO [KnownEndpoints](Id, HostId, Host, HostDisplayName, Monitored) 
                            VALUES(@Id, @HostId, @Host, @HostDisplayName, 1)",
                    new
                    {
                        Id = id,
                        endpoint.HostId,
                        endpoint.Host,
                        HostDisplayName = endpoint.Name,
                        Monitored = endpointInstanceMonitoring.IsMonitored(id)
                    }).ConfigureAwait(false);
            }
        }

        public async Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored)
        {
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

        public async Task WarmupMonitoringFromPersistence(EndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                var rows = await connection.QueryAsync("SELECT * FROM [KnownEndpoints]").ConfigureAwait(false);

                foreach (dynamic row in rows)
                {
                    endpointInstanceMonitoring.DetectEndpointFromPersistentStore(new EndpointDetails
                    {
                        HostId = row.HostId,
                        Host = row.Host,
                        Name = row.HostDisplayName
                    }, row.Monitored);
                }
            }
        }

        public async Task Delete(Guid endpointId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                await connection.ExecuteAsync(
                    @"DELETE FROM [dbo].[KnownEndpoints] WHERE [Id] = @Id",
                    new
                    {
                        Id = endpointId
                    }).ConfigureAwait(false);
            }
        }
        public async Task<KnownEndpoint[]> GetAllKnownEndpoints()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                var rows = await connection.QueryAsync("SELECT * FROM [KnownEndpoints]").ConfigureAwait(false);

                return (from row in rows
                        select new KnownEndpoint
                        {
                            EndpointDetails = new EndpointDetails
                            {
                                Host = row.Host,
                                HostId = row.HostId,
                                Name = row.HostDisplayName
                            },
                            HostDisplayName = row.HostDisplayName,
                            Id = row.Id,
                            Monitored = row.Monitored
                        }).ToArray();
            }
        }
    }
}