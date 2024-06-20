namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Particular.LicensingComponent.Persistence.InMemory;
using ServiceControl.Persistence.Tests.InMemory;

public class PersistenceTestsContext : IPersistenceTestsContext
{
    public PersistenceSettings PersistenceSettings { get; private set; }

    public Task Setup(IHostApplicationBuilder hostBuilder)
    {
        hostBuilder.Services.AddLicensingInMemoryPersistence();

        PersistenceSettings = new InMemoryPersistenceSettings();

        return Task.CompletedTask;
    }

    public Task PostSetup(IHost host) => Task.CompletedTask;

    public Task TearDown() => Task.CompletedTask;

    public void CompleteDatabaseOperation()
    {
    }
}