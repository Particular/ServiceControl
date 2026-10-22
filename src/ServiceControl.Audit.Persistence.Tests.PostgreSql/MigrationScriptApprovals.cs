namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Migrations;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;
    using ServiceControl.Audit.Persistence.EFCore.DbContexts;
    using ServiceControl.Audit.Persistence.EFCore.PostgreSql;
    using TestHelper;

    // A changed index can block writes on a large table for a long time. Record it in the upgrade guide and never edit an existing migration, see docs/testing.md.
    [TestFixture]
    class MigrationScriptApprovals
    {
        [TestCase(null)]
        [TestCase("custom_schema")]
        public void Verify_migration_script_is_unchanged(string schema)
        {
            var settings = new PersistenceSettings(TimeSpan.FromDays(1), true, 100000);
            settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.ConnectionStringKey] = "Host=unused";
            if (schema is not null)
            {
                settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.SchemaKey] = schema;
            }

            var services = new ServiceCollection();
            new PostgreSqlPersistenceConfiguration().Create(settings).AddInstaller(services);

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var script = scope.ServiceProvider.GetRequiredService<AuditDbContext>().GetService<IMigrator>().GenerateScript();

            Approver.Verify(script, MigrationScript.WithoutProductVersion, schema ?? "default_schema");
        }
    }
}
