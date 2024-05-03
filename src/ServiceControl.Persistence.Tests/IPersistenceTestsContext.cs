namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

public interface IPersistenceTestsContext
{
    Task Setup(IHostApplicationBuilder hostBuilder);

    Task PostSetup(IHost host);

    Task TearDown();

    void CompleteDatabaseOperation();

    PersistenceSettings PersistenceSettings { get; }
}