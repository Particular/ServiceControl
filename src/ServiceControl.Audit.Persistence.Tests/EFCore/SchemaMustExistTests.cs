namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;

    [TestFixture]
    class SchemaMustExistTests
    {
        [Test]
        public async Task Setup_fails_when_the_configured_schema_does_not_exist()
        {
            var testsConfiguration = new PersistenceTestsConfiguration();
            var schema = $"sc_absent_{Guid.NewGuid():n}";

            var settings = new PersistenceSettings(TimeSpan.FromDays(1), true, 100000);
            settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.ConnectionStringKey] = await testsConfiguration.GetConnectionString();
            settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.SchemaKey] = schema;

            var hostBuilder = Host.CreateApplicationBuilder();
            testsConfiguration.CreateConfiguration().Create(settings).AddInstaller(hostBuilder.Services);
            using var host = hostBuilder.Build();

            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

            Assert.That(exception.Message, Does.Contain(schema).And.Contain("does not exist"));
        }
    }
}
