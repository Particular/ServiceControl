namespace ServiceControl.PersistenceTests
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NUnit.Framework;
    using Persistence;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence.RavenDb;

    sealed class TestPersistenceImpl : TestPersistence
    {
        IDocumentStore documentStore;

        public const string DatabaseMaintenancePort = "55554";

        static PersistenceSettings CreateSettings()
        {
            var retentionPeriod = TimeSpan.FromMinutes(1);
            var settings = new PersistenceSettings(retentionPeriod, retentionPeriod, retentionPeriod, 100, false)
            {
                PersisterSpecificSettings =
                {
                    [RavenBootstrapper.RunInMemoryKey] = bool.TrueString,
                    [RavenBootstrapper.HostNameKey] = "localhost",
                    [RavenBootstrapper.DatabaseMaintenancePortKey] = DatabaseMaintenancePort
                }
            };

            if (Debugger.IsAttached)
            {
                settings.PersisterSpecificSettings[RavenBootstrapper.ExposeRavenDBKey] = bool.TrueString;
            }

            return settings;
        }

        public override void Configure(IServiceCollection services)
        {
            var config = PersistenceConfigurationFactory.LoadPersistenceConfiguration(DataStoreConfig.RavenDB35PersistenceTypeFullyQualifiedName);
            var settings = CreateSettings();

            var instance = config.Create(settings);
            PersistenceHostBuilderExtensions.CreatePersisterLifecyle(services, instance);


            services.AddHostedService(p => new Wrapper(this, p.GetRequiredService<IDocumentStore>()));
        }

        public override Task CompleteDatabaseOperation()
        {
            Assert.IsNotNull(documentStore);
            documentStore.WaitForIndexing();
            return Task.CompletedTask;
        }

        class Wrapper : IHostedService
        {
            public Wrapper(TestPersistenceImpl instance, IDocumentStore store)
            {
                instance.documentStore = store;
            }

            public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}