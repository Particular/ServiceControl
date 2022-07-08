﻿namespace ServiceControl.Monitoring
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using Dapper;
    using Infrastructure;

    class SqlDbMonitoringDataStore : IMonitoringDataStore
    {
        readonly string connectionString;

        public SqlDbMonitoringDataStore(string connectionString)
        {
            this.connectionString = connectionString;
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

        // NOTE: Temp implementation. Need to find a bulk insert
        public async Task BulkCreate(EndpointDetails[] endpoints)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                foreach (var endpoint in endpoints)
                {
                    var id = DeterministicGuid.MakeId(endpoint.Name, endpoint.HostId.ToString());

                    await connection.ExecuteAsync(
                        @"IF NOT EXISTS(SELECT * FROM [KnownEndpoints] WHERE Id = @Id)
                                INSERT INTO [KnownEndpoints](Id, HostId, Host, HostDisplayName, Monitored) 
                                VALUES(@Id, @HostId, @Host, @HostDisplayName, @Monitored)
                        )",
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
        }

        public static async Task Setup(string connectionString)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                var catalog = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

                var createCommand = $@"
                    IF NOT EXISTS (
                         SELECT *
                         FROM {catalog}.sys.objects
                         WHERE object_id = OBJECT_ID(N'KnownEndpoints') AND type in (N'U')
                       )
                       BEGIN
                           CREATE TABLE [dbo].[KnownEndpoints](
	                           [Id] [uniqueidentifier] NOT NULL,
	                           [HostId] [uniqueidentifier] NOT NULL,
	                           [Host] [nvarchar](300) NULL,
	                           [HostDisplayName] [nvarchar](300) NULL,
	                           [Monitored] [bit] NOT NULL
                           ) ON [PRIMARY]
                       END";

                connection.Open();

                await connection.ExecuteAsync(createCommand).ConfigureAwait(false);
            }
        }
    }
}