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
    class SchemaMustExistTests
    {
        [Test]
        public async Task Setup_fails_when_the_configured_schema_does_not_exist()
        {
            var testsConfiguration = new PersistenceTestsConfiguration();
            var connectionString = await testsConfiguration.GetConnectionString();
            var schema = $"sc_absent_{Guid.NewGuid():n}";

            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => Setup(testsConfiguration, connectionString, schema));

            Assert.That(exception.Message, Does.Contain(schema).And.Contain("does not exist"));
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
