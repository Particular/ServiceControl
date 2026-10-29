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
class SchemaSetupTests
{
    [Test]
    public async Task Migration_creates_the_configured_schema_when_it_does_not_exist()
    {
        var settings = await CreateSettings($"sc_absent_{Guid.NewGuid():n}");

        try
        {
            Assert.DoesNotThrowAsync(() => ApplyMigrations(settings));
        }
        finally
        {
            await TestSchema.Drop(settings.ConnectionString, settings.Schema);
        }
    }

    [Test]
    public async Task Migration_fails_when_the_schema_holds_the_audit_migrations_history_table()
    {
        var settings = await CreateSettings($"sc_shared_{Guid.NewGuid():n}");
        await TestSchema.Create(settings.ConnectionString, settings.Schema);

        try
        {
            await TestSchema.CreateTable(settings.ConnectionString, settings.Schema, "__AuditMigrationsHistory");

            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => ApplyMigrations(settings));
            Assert.That(exception.Message, Does.Contain(settings.Schema).And.Contain("__AuditMigrationsHistory"));
        }
        finally
        {
            await TestSchema.Drop(settings.ConnectionString, settings.Schema);
        }
    }

    [Test]
    public async Task Migration_ignores_the_audit_migrations_history_table_in_another_schema()
    {
        var settings = await CreateSettings($"sc_primary_{Guid.NewGuid():n}");
        var auditSchema = $"sc_audit_{Guid.NewGuid():n}";
        await TestSchema.Create(settings.ConnectionString, settings.Schema);
        await TestSchema.Create(settings.ConnectionString, auditSchema);

        try
        {
            await TestSchema.CreateTable(settings.ConnectionString, auditSchema, "__AuditMigrationsHistory");

            Assert.DoesNotThrowAsync(() => ApplyMigrations(settings));
        }
        finally
        {
            await TestSchema.Drop(settings.ConnectionString, settings.Schema);
            await TestSchema.Drop(settings.ConnectionString, auditSchema);
        }
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
