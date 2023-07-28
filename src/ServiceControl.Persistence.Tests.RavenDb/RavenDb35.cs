namespace ServiceControl.PersistenceTests
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence.RavenDb;

    sealed class TestPersistenceImpl : TestPersistence
    {
        static PersistenceSettings CreateSettings()
        {
            var retentionPeriod = TimeSpan.FromMinutes(1);
            var settings = new PersistenceSettings(retentionPeriod, retentionPeriod, retentionPeriod, 100, false)
            {
                PersisterSpecificSettings =
                {
                    [RavenBootstrapper.RunInMemoryKey] = bool.TrueString,
                    [RavenBootstrapper.HostNameKey] = "localhost",
                    [RavenBootstrapper.DatabaseMaintenancePortKey] = "55554",
                }
            };

            return settings;
        }

        public override void Configure(IServiceCollection services)
        {
            var config = PersistenceConfigurationFactory.LoadPersistenceConfiguration(DataStoreConfig.RavenDB35PersistenceTypeFullyQualifiedName);
            var settings = CreateSettings();

            var instance = config.Create(settings);
            PersistenceHostBuilderExtensions.CreatePersisterLifecyle(services, instance);
        }
    }
}