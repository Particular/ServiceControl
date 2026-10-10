namespace ServiceControl.AcceptanceTests
{
    using System;
    using System.Runtime.Loader;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;

    class MaintenanceModeTests : AcceptanceTest
    {
        // The refusal happens before any database is touched, so this needs no storage set up.
        [Test]
        public void Should_refuse_maintenance_mode()
        {
            var settings = new Settings(
                transportType: TransportIntegration.TypeName,
                persisterType: StorageConfiguration.PersistenceType,
                forwardErrorMessages: false,
                errorRetentionPeriod: TimeSpan.FromDays(1))
            {
                TransportConnectionString = TransportIntegration.ConnectionString,
                AssemblyLoadContextResolver = static _ => AssemblyLoadContext.Default
            };

            var exception = Assert.Throws<Exception>(() => new ServiceCollection().AddPersistence(settings, maintenanceMode: true));

            Assert.That(exception.Message, Does.Contain("Maintenance mode is not supported").And.Contain(StorageConfiguration.PersistenceType));
        }
    }
}