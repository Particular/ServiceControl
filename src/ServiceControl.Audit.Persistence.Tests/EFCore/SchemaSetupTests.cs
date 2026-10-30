namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;
    using ServiceControl.Audit.Persistence.EFCore.DbContexts;
    using ServiceControl.Audit.Persistence.EFCore.Implementation;
    using ServiceControl.Persistence.Tests;

    [TestFixture]
    class SchemaSetupTests
    {
        [Test]
        public async Task Setup_creates_the_configured_schema_when_it_does_not_exist()
        {
            var testsConfiguration = new PersistenceTestsConfiguration();
            var connectionString = await testsConfiguration.GetConnectionString();
            var schema = $"sc_absent_{Guid.NewGuid():n}";

            try
            {
                Assert.DoesNotThrowAsync(() => Setup(testsConfiguration, connectionString, schema));
            }
            finally
            {
                await TestSchema.Drop(connectionString, schema);
            }
        }

        [Test]
        public async Task Setup_fails_when_the_schema_holds_the_primary_migrations_history_table()
        {
            var testsConfiguration = new PersistenceTestsConfiguration();
            var connectionString = await testsConfiguration.GetConnectionString();
            var schema = $"sc_shared_{Guid.NewGuid():n}";
            await TestSchema.Create(connectionString, schema);

            try
            {
                await TestSchema.CreateTable(connectionString, schema, "__EFMigrationsHistory");

                var exception = Assert.ThrowsAsync<InvalidOperationException>(() => Setup(testsConfiguration, connectionString, schema));

                Assert.That(exception.Message, Does.Contain(schema).And.Contain("__EFMigrationsHistory"));
            }
            finally
            {
                await TestSchema.Drop(connectionString, schema);
            }
        }

        [Test]
        public async Task Setup_ignores_the_primary_migrations_history_table_in_another_schema()
        {
            var testsConfiguration = new PersistenceTestsConfiguration();
            var connectionString = await testsConfiguration.GetConnectionString();
            var schema = $"sc_audit_{Guid.NewGuid():n}";
            var primarySchema = $"sc_primary_{Guid.NewGuid():n}";
            await TestSchema.Create(connectionString, schema);
            await TestSchema.Create(connectionString, primarySchema);

            try
            {
                await TestSchema.CreateTable(connectionString, primarySchema, "__EFMigrationsHistory");

                Assert.DoesNotThrowAsync(() => Setup(testsConfiguration, connectionString, schema));
            }
            finally
            {
                await TestSchema.Drop(connectionString, schema);
                await TestSchema.Drop(connectionString, primarySchema);
            }
        }

        [Test]
        public async Task Pending_migrations_fail_the_startup_check_until_setup_has_applied_them()
        {
            var testsConfiguration = new PersistenceTestsConfiguration();
            var connectionString = await testsConfiguration.GetConnectionString();
            var schema = $"sc_pending_{Guid.NewGuid():n}";

            try
            {
                var exception = Assert.ThrowsAsync<InvalidOperationException>(() => CheckPendingMigrations(testsConfiguration, connectionString, schema));
                Assert.That(exception.Message, Does.Contain("pending migrations").And.Contain("--setup"));

                await Setup(testsConfiguration, connectionString, schema);

                Assert.DoesNotThrowAsync(() => CheckPendingMigrations(testsConfiguration, connectionString, schema));
            }
            finally
            {
                await TestSchema.Drop(connectionString, schema);
            }
        }

        static async Task CheckPendingMigrations(PersistenceTestsConfiguration testsConfiguration, string connectionString, string schema)
        {
            var settings = new PersistenceSettings(TimeSpan.FromDays(1), true, 100000);
            settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.ConnectionStringKey] = connectionString;
            settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.SchemaKey] = schema;

            var services = new ServiceCollection();
            testsConfiguration.CreateConfiguration().Create(settings).AddInstaller(services);

            await using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            await new PendingMigrationsStartupCheck(scope.ServiceProvider.GetRequiredService<AuditDbContext>()).Check();
        }

        static async Task Setup(PersistenceTestsConfiguration testsConfiguration, string connectionString, string schema)
        {
            var settings = new PersistenceSettings(TimeSpan.FromDays(1), true, 100000);
            settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.ConnectionStringKey] = connectionString;
            settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.SchemaKey] = schema;

            var hostBuilder = Host.CreateApplicationBuilder();
            testsConfiguration.CreateConfiguration().Create(settings).AddInstaller(hostBuilder.Services);
            using var host = hostBuilder.Build();

            await host.StartAsync();
            await host.StopAsync();
        }
    }
}
