using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence;

[Parallelizable(ParallelScope.None)] // RavenDB5 Cannot do in memory so tests need to run sequentially
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public abstract class BaseHostTest
{
    readonly IHost testHost;

    protected BaseHostTest() => testHost = CreateHostBuilder().Build();

    [SetUp]
    public async Task SetUp()
    {
        await GetRequiredService<IPersistenceLifecycle>().Initialize();
        await testHost.StartAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await testHost.StopAsync();
        testHost.Dispose();
    }

    protected T GetRequiredService<T>() => testHost.Services.GetRequiredService<T>();

    protected virtual IHostBuilder CreateHostBuilder() => Host.CreateDefaultBuilder()
            .ConfigureLogging((hostingContext, logging) =>
            {
                logging.AddConsole();
            });
}