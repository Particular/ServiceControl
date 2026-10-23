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
        var settings = await CreateSettings($"sc_absent_{Guid.NewGuid():n}");

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => ApplyMigrations(settings));
        Assert.That(exception.Message, Does.Contain(settings.Schema).And.Contain("does not exist"));
    }

    static async Task<PostgreSqlPersisterSettings> CreateSettings(string schema) => new()
    {
        ConnectionString = await PostgreSqlSharedContainer.GetConnectionStringAsync(),
        Schema = schema,
        BodyStorage = new FileSystemBodyStorageSettings { StoragePath = Path.GetTempPath() }
    };

    static async Task ApplyMigrations(PostgreSqlPersisterSettings settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        new PostgreSqlPersistenceConfiguration().Create(settings).AddInstaller(services);

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();

        await migrator.ApplyMigrations();
    }
}
