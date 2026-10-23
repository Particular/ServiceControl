// ReSharper disable once CheckNamespace
namespace ServiceControl.Persistence.Tests;

using EFCore.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using TestHelper;

[TestFixture]
class SlowMigrationOperationApprovals
{
    [Test]
    public void Verify_slow_migration_operations_are_unchanged()
    {
        var settings = new PostgreSqlPersisterSettings
        {
            ConnectionString = "Host=unused",
            BodyStorage = new FileSystemBodyStorageSettings { StoragePath = "unused" }
        };

        var services = new ServiceCollection();
        new PostgreSqlPersistenceConfiguration().Create(settings).AddInstaller(services);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        SlowMigrationOperations.Verify(scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>());
    }
}
