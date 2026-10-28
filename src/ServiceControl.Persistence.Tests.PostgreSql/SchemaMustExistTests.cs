// ReSharper disable once CheckNamespace
namespace ServiceControl.Persistence.Tests;

using System;
using System.IO;
using System.Threading.Tasks;
using EFCore.PostgreSql;
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
        var settings = new PostgreSqlPersisterSettings
        {
            ConnectionString = await PostgreSqlSharedContainer.GetConnectionStringAsync(),
            Schema = $"sc_absent_{Guid.NewGuid():n}",
            BodyStorage = new FileSystemBodyStorageSettings { StoragePath = Path.GetTempPath() }
        };

        var services = new ServiceCollection();
        services.AddLogging();
        new PostgreSqlPersistenceConfiguration().Create(settings).AddInstaller(services);

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => migrator.ApplyMigrations());
        Assert.That(exception.Message, Does.Contain(settings.Schema).And.Contain("does not exist"));
    }
}
