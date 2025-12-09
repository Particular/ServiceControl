namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using NUnit.Framework;
using Testcontainers.MsSql;

[SetUpFixture]
static class ManageDatabaseServer
{
    static MsSqlContainer sqlServerContainer;

    public static string ConnectionString { get; private set; }

    [OneTimeSetUp]
    public static async Task EnsureServerStarted()
    {
        sqlServerContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("YourStrong@Passw0rd")
            .WithPortBinding(11433, 1433)
            .Build();

        await sqlServerContainer.StartAsync();

        ConnectionString = sqlServerContainer.GetConnectionString();
    }

    [OneTimeTearDown]
    public static async Task EnsureServerStopped()
    {
        if (sqlServerContainer != null)
        {
            await sqlServerContainer.StopAsync();
            await sqlServerContainer.DisposeAsync();
        }
    }
}
