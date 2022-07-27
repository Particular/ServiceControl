namespace ServiceControl.Persistence.SqlServer
{
    using System;
    using System.Collections.Generic;
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
        readonly SqlDbConnectionManager connectionManager;

        public SqlDbMonitoringDataStore(SqlDbConnectionManager connectionManager)
        {
            this.connectionManager = connectionManager;
        }

        public async Task CreateIfNotExists(EndpointDetails endpoint)
        {
            await connectionManager.Perform(async connection =>
            {
                await connection.ExecuteAsync(
                    @"IF NOT EXISTS(SELECT * FROM [KnownEndpoints] WHERE Id = @Id)
                          BEGIN
                            INSERT INTO [KnownEndpoints](Id, HostId, Host, HostDisplayName, Monitored) 
                            VALUES(@Id, @HostId, @Host, @HostDisplayName, @Monitored)
                          END",
                    new
                    {
                        Id = endpoint.GetDeterministicId(),
                        endpoint.HostId,
                        endpoint.Host,
                        HostDisplayName = endpoint.Name,
                        Monitored = false
                    }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        public async Task CreateOrUpdate(EndpointDetails endpoint, EndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            var id = endpoint.GetDeterministicId();

            await connectionManager.Perform(async connection =>
            {
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
            }).ConfigureAwait(false);
        }

        public async Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored)
        {
            await connectionManager.Perform(async connection =>
            {
                await connection.ExecuteAsync(
                    @"UPDATE [KnownEndpoints] SET [Monitored] = @Monitored WHERE [Id] = @Id",
                    new
                    {
                        Id = endpoint.GetDeterministicId(),
                        Monitored = isMonitored
                    }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        public async Task WarmupMonitoringFromPersistence(EndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            await connectionManager.Perform(async connection =>
            {
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
            }).ConfigureAwait(false);
        }

        public async Task Delete(Guid endpointId)
        {
            await connectionManager.Perform(async connection =>
            {
                await connection.ExecuteAsync(
                    @"DELETE FROM [dbo].[KnownEndpoints] WHERE [Id] = @Id",
                    new
                    {
                        Id = endpointId
                    }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<KnownEndpoint>> GetAllKnownEndpoints()
        {
            var endpoints = new List<KnownEndpoint>();

            await connectionManager.Perform(async connection =>
            {
                var rows = await connection.QueryAsync("SELECT * FROM [KnownEndpoints]").ConfigureAwait(false);

                foreach (var row in rows)
                {
                    endpoints.Add(new KnownEndpoint
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
                    });
                }
            }).ConfigureAwait(false);

            return endpoints;
        }
    }
}