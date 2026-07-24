namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using MessageFailures;
using Microsoft.Extensions.Hosting;
using Particular.LicensingComponent.Persistence.InMemory;
using ServiceControl.Persistence.Tests.InMemory;

public class PersistenceTestsContext : IPersistenceTestsContext
{
    public PersistenceSettings PersistenceSettings { get; private set; }
    public string GenerateFailedMessageRecordId(string messageId) => throw new System.NotImplementedException();
    public Task InsertFailedMessages(params FailedMessage[] messages) => throw new System.NotImplementedException();

    public Task Setup(IHostApplicationBuilder hostBuilder)
    {
        hostBuilder.Services.AddLicensingInMemoryPersistence();

        PersistenceSettings = new InMemoryPersistenceSettings();

        return Task.CompletedTask;
    }

    public Task PostSetup(IHost host) => Task.CompletedTask;

    public Task TearDown() => Task.CompletedTask;

    public Task CompleteDatabaseOperation() => Task.CompletedTask;
}