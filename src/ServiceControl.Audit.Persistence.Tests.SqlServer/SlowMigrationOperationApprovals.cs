namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;
    using ServiceControl.Audit.Persistence.EFCore.DbContexts;
    using ServiceControl.Audit.Persistence.EFCore.SqlServer;
    using TestHelper;

    [TestFixture]
    class SlowMigrationOperationApprovals
    {
        [Test]
        public void Verify_slow_migration_operations_are_unchanged()
        {
            var settings = new PersistenceSettings(TimeSpan.FromDays(1), true, 100000);
            settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.ConnectionStringKey] = "Server=unused";

            var services = new ServiceCollection();
            new SqlServerPersistenceConfiguration().Create(settings).AddInstaller(services);

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();

            SlowMigrationOperations.Verify(scope.ServiceProvider.GetRequiredService<AuditDbContext>());
        }
    }
}
