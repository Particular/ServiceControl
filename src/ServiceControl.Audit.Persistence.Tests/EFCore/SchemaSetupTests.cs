namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;
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
                await TestSchema.CreateTable(connectionString, schema, "__ErrorMigrationsHistory");

                var exception = Assert.ThrowsAsync<InvalidOperationException>(() => Setup(testsConfiguration, connectionString, schema));

                Assert.That(exception.Message, Does.Contain(schema).And.Contain("__ErrorMigrationsHistory"));
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
                await TestSchema.CreateTable(connectionString, primarySchema, "__ErrorMigrationsHistory");

                Assert.DoesNotThrowAsync(() => Setup(testsConfiguration, connectionString, schema));
            }
            finally
            {
                await TestSchema.Drop(connectionString, schema);
                await TestSchema.Drop(connectionString, primarySchema);
            }
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
