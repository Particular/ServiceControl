// ReSharper disable once CheckNamespace
namespace ServiceControl.Persistence.Tests;

using System;
using System.IO;
using System.Threading.Tasks;
using EFCore.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;

[TestFixture]
class SchemaMustExistTests
{
    [Test]
    public async Task Migration_fails_when_the_configured_schema_does_not_exist()
    {
        var settings = new SqlServerPersisterSettings
        {
            ConnectionString = await SqlServerSharedContainer.GetConnectionStringAsync(),
            Schema = $"sc_absent_{Guid.NewGuid():n}",
            BodyStorage = new FileSystemBodyStorageSettings { StoragePath = Path.GetTempPath() }
        };

        var services = new ServiceCollection();
        services.AddLogging();
        new SqlServerPersistenceConfiguration().Create(settings).AddInstaller(services);

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => migrator.ApplyMigrations());
        Assert.That(exception.Message, Does.Contain(settings.Schema).And.Contain("does not exist"));
    }
}
