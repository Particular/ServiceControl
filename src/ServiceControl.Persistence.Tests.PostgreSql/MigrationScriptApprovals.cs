// ReSharper disable once CheckNamespace
namespace ServiceControl.Persistence.Tests;

using EFCore.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.Approvals;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using TestHelper;

// A changed index can block writes on a large table for a long time. Record it in the upgrade guide and never edit an existing migration, see docs/testing.md.
[TestFixture]
class MigrationScriptApprovals
{
    [TestCase(null)]
    [TestCase("custom_schema")]
    public void Verify_migration_script_is_unchanged(string schema)
    {
        var settings = new PostgreSqlPersisterSettings
        {
            ConnectionString = "Host=unused",
            Schema = schema,
            BodyStorage = new FileSystemBodyStorageSettings { StoragePath = "unused" }
        };

        var services = new ServiceCollection();
        new PostgreSqlPersistenceConfiguration().Create(settings).AddInstaller(services);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var script = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().GetService<IMigrator>().GenerateScript();

        Approver.Verify(script, MigrationScript.WithoutProductVersion, schema ?? "default_schema");
    }
}
