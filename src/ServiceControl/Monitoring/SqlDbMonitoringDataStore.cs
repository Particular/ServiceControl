namespace ServiceControl.Monitoring
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using Dapper;
    using Infrastructure;

    class SqlDbMonitoringDataStore : IMonitoringDataStore
    {
        readonly string connectionString;
        readonly EndpointInstanceMonitoring monitoring;

        public SqlDbMonitoringDataStore(string connectionString, EndpointInstanceMonitoring monitoring)
        {
            this.connectionString = connectionString;
            this.monitoring = monitoring;
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

        public async Task CreateOrUpdate(EndpointDetails endpoint)
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
                        Monitored = monitoring.IsMonitored(id)
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

        public async Task WarmupMonitoringFromPersistence()
        {
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