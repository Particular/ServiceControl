namespace ServiceControl.Persistence.Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EFCore.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;

[TestFixture]
class EntraIdAuthenticationTests
{
    const string ConnectionStringVariable = "ServiceControl_Persistence_SqlServer_TokenConnectionString";

    static string connectionString;

    [OneTimeSetUp]
    public static void RequireTokenConnectionString()
    {
        connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);

        if (string.IsNullOrEmpty(connectionString))
        {
            Assert.Ignore($"{ConnectionStringVariable} is not set. The cloud database tests set it for the azure-sqlserver target, see docs/cloud-database-tests.md.");
        }
    }

    [Test]
    public async Task Migrations_and_a_round_trip_work_over_an_Entra_ID_login()
    {
        var settings = new SqlServerPersisterSettings
        {
            ConnectionString = connectionString,
            Schema = $"sc_token_{Guid.NewGuid():n}",
            BodyStorage = new FileSystemBodyStorageSettings { StoragePath = Path.GetTempPath() }
        };

        var services = new ServiceCollection();
        services.AddLogging();

        var persistence = new SqlServerPersistenceConfiguration().Create(settings);
        persistence.AddPersistence(services);
        persistence.AddInstaller(services);

        await using var provider = services.BuildServiceProvider();

        try
        {
            using (var scope = provider.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>().ApplyMigrations();
            }

            var store = provider.GetRequiredService<IEndpointSettingsStore>();
            await store.UpdateEndpointSettings(new EndpointSettings { Name = "Sales", TrackInstances = true });

            var stored = await store.GetAllEndpointSettings().ToListAsync();

            Assert.That(stored.Select(s => (s.Name, s.TrackInstances)), Is.EqualTo(new[] { ("Sales", true) }));
        }
        finally
        {
            await TestSchema.Drop(connectionString, settings.Schema);
        }
    }
}
