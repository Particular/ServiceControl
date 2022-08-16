namespace ServiceControl.Audit.Persistence
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
        public static async Task SetupAuditTables(string connectionString)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                var catalog = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

                var createKnownEndpointsCommand = $@"
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
                            [Name] [nvarchar](300) NULL,
                            [LastSeend] [datetime] NOT NULL
                           ) ON [PRIMARY]
                       END";

                //TODO!! What other tables are needed for this?                           
                var createMessageViewCommand = $@"
                    IF NOT EXISTS (
                         SELECT *
                         FROM {catalog}.sys.objects
                         WHERE object_id = OBJECT_ID(N'MessageView') AND type in (N'U')
                       )
                       BEGIN
                           CREATE TABLE [dbo].[MessageView](                            
                            [Id] [uniqueidentifier] NOT NULL,
                            [HostId] [uniqueidentifier] NOT NULL,
                            [Host] [nvarchar](300) NULL,
                            [Name] [nvarchar](300) NULL,
                            [LastSeend] [datetime] NOT NULL
                           ) ON [PRIMARY]
                       END";

                connection.Open();

                var cmd = new SqlCommand(createKnownEndpointsCommand, connection);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
    }
}