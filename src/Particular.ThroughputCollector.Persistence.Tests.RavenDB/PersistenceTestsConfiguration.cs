namespace Particular.ThroughputCollector.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Particular.ThroughputCollector.Persistence;
using Particular.ThroughputCollector.Persistence.RavenDb;
using Particular.ThroughputCollector.Persistence.Tests.RavenDb;
using Raven.Client.Documents;
using Raven.Client.ServerWide.Operations;

partial class PersistenceTestsConfiguration
{
    public IThroughputDataStore ThroughputDataStore { get; protected set; }

    public IDocumentStore DocumentStore { get; private set; }

    public string Name => "RavenDB";

    public async Task Configure()
    {
        var services = new ServiceCollection();
        services.AddThroughputRavenPersistence();

        DocumentStore = await SharedEmbeddedServer.GetInstance();

        // replace DatabaseConfiguration registration
        var databaseConfigurationServiceDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(DatabaseConfiguration));
        if (databaseConfigurationServiceDescriptor != null)
        {
            services.Remove(databaseConfigurationServiceDescriptor);
        }
        services.AddSingleton(new DatabaseConfiguration(DocumentStore.Database));

        // Register the IDocumentStore expected by the RavenDB persistence
        services.AddSingleton(new Lazy<IDocumentStore>(() => DocumentStore));

        var serviceProvider = services.BuildServiceProvider();

        var installer = serviceProvider.GetRequiredService<IPersistenceInstaller>();
        await installer.Install();

        ThroughputDataStore = serviceProvider.GetRequiredService<IThroughputDataStore>();
    }

    public Task CompleteDBOperation()
    {
        DocumentStore.WaitForIndexing();
        return Task.CompletedTask;
    }

    public async Task Cleanup()
    {
        if (DocumentStore == null)
        {
            return;
        }

        await DocumentStore.Maintenance.Server.SendAsync(new DeleteDatabasesOperation(
            new DeleteDatabasesOperation.Parameters
            {
                DatabaseNames = [DocumentStore.Database],
                HardDelete = true
            }));
    }
}