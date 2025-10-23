namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Primitives;
    using NServiceBus.CustomChecks;
    using ServiceControl.Audit.Persistence.InMemory;
    using UnitOfWork;

    class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; private set; }

        public IFailedAuditStorage FailedAuditStorage { get; private set; }

        public IBodyStorage BodyStorage { get; private set; }

        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; private set; }

        public IServiceProvider ServiceProvider => host.Services;

        public string Name => "InMemory";

        public async Task Configure(Action<PersistenceSettings, IDictionary<string,string>> setSettings)
        {
            var config = new InMemoryPersistenceConfiguration();
            var hostBuilder = Host.CreateApplicationBuilder();
            var settings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);

            setSettings(settings, null); // TODO: new Dictionary<string, string>();

            var configuration = new ConfigurationBuilder().Build();
            var persistence = config.Create(settings, configuration);
            persistence.AddPersistence(hostBuilder.Services);
            persistence.AddInstaller(hostBuilder.Services);

            var assembly = typeof(InMemoryPersistenceConfiguration).Assembly;

            foreach (var type in assembly.DefinedTypes)
            {
                if (type.IsAssignableTo(typeof(ICustomCheck)))
                {
                    hostBuilder.Services.AddTransient(typeof(ICustomCheck), type);
                }
            }

            host = hostBuilder.Build();
            await host.StartAsync();

            AuditDataStore = host.Services.GetRequiredService<IAuditDataStore>();
            FailedAuditStorage = host.Services.GetRequiredService<IFailedAuditStorage>();
            BodyStorage = host.Services.GetService<IBodyStorage>();
            AuditIngestionUnitOfWorkFactory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
        }

        public Task CompleteDBOperation() => Task.CompletedTask;

        public async Task Cleanup()
        {
            await host.StopAsync();
            host.Dispose();
        }

        IHost host;
    }
}