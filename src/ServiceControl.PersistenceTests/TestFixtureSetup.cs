namespace ServiceControl.Persistence.Tests
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Dapper;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;

    [SetUpFixture]
    public class TestFixtureSetup
    {
        public static string SqlDbConnectionString = SettingsReader<string>.Read("SqlStorageConnectionString", "Server=localhost,1433;Initial Catalog=TestSqlPersistence;Persist Security Info=False;User ID=sa;Password=p@ssword;MultipleActiveResultSets=False");

        [OneTimeSetUp]
        public Task GlobalSetup()
        {
            return Task.CompletedTask;
        }

        [OneTimeTearDown]
        public Task GlobalTeardown()
        {
            return Task.CompletedTask;
        }
    }
}