namespace ServiceControl.Persistence.Tests
{
    //WORK IN PROGRESS
    //using System.Data.SqlClient;
    //using System.Threading.Tasks;
    //using Dapper;
    //using NUnit.Framework;
    //using ServiceBus.Management.Infrastructure.Settings;

    //[SetUpFixture]
    //public class TestFixtureSetup
    //{
    //    public static string SqlDbConnectionString = SettingsReader<string>.Read("SqlStorageConnectionString", "Server=localhost,1433;Initial Catalog=TestSqlPersistence;Persist Security Info=False;User ID=sa;Password=p@ssword;MultipleActiveResultSets=False");

    //    [OneTimeSetUp]
    //    public Task GlobalSetup()
    //    {
    //        return Task.CompletedTask;
    //    }

    //    [OneTimeTearDown]
    //    public async Task GlobalTeardown()
    //    {
    //        //To cleanup SQL connection in case tests error (for tests not using a local TearDown)
    //        using (var connection = new SqlConnection(SqlDbConnectionString))
    //        {
    //            var catalog = new SqlConnectionStringBuilder(SqlDbConnectionString).InitialCatalog;

    //            var truncateCommand = $@"
    //                IF EXISTS (
    //                     SELECT *
    //                     FROM {catalog}.sys.objects
    //                     WHERE object_id = OBJECT_ID(N'KnownEndpoints') AND type in (N'U')
    //                   )
    //                   BEGIN
    //                       Truncate TABLE [dbo].[KnownEndpoints]
    //                   END";

    //            connection.Open();

    //            await connection.ExecuteAsync(truncateCommand).ConfigureAwait(false);
    //        }
    //    }
    //}
}