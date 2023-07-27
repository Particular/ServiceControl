namespace ServiceControl.PersistenceTests
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;

    sealed class InMemory : TestPersistence
    {
        static PersistenceSettings CreateSettings()
        {
            throw new NotImplementedException();
        }

        public override void Configure(IServiceCollection services)
        {
            var config = PersistenceConfigurationFactory.LoadPersistenceConfiguration(DataStoreConfig.InMemoryPersistenceTypeFullyQualifiedName);
            var settings = CreateSettings();
            var instance = config.Create(settings);
            instance.Configure(services);
        }
    }
}