namespace ServiceControl.Persistence
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;

    interface IPersistenceConfiguration
    {
        void ConfigureServices(IServiceCollection serviceCollection);
    }

    //NOTE ideally once we only have one type of persistence (ie use ravendb or sql for all) then these could be refactored
    public static class SetupSqlPersistence
    {
        public static async Task SetupMonitoring(string connectionString)
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

                var cmd = new SqlCommand(createCommand, connection);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public static async Task SetupCustomChecks(string connectionString)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                var catalog = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

                var createCommand = $@"
                    IF NOT EXISTS (
                         SELECT *
                         FROM {catalog}.sys.objects
                         WHERE object_id = OBJECT_ID(N'CustomChecks') AND type in (N'U')
                       )
                       BEGIN
                           CREATE TABLE [dbo].[CustomChecks](
                               [Id] [uniqueidentifier] NOT NULL,
                               [CustomCheckId] nvarchar(300) NOT NULL,
                               [Category] nvarchar(300) NULL,
                               [Status] int NOT NULL,
                               [ReportedAt] datetime NOT NULL,
                               [FailureReason] nvarchar(300) NULL,
                               [OriginatingEndpointName] nvarchar(300) NOT NULL,
                               [OriginatingEndpointHostId] [uniqueidentifier] NOT NULL,
                               [OriginatingEndpointHost] nvarchar(300) NOT NULL
                           ) ON [PRIMARY]
                       END";

                connection.Open();

                var cmd = new SqlCommand(createCommand, connection);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
    }
}