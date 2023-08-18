using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

[Parallelizable(ParallelScope.All)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public abstract class BaseHostTest
{
    readonly IHost testHost;

    protected BaseHostTest() => testHost = CreateHostBuilder().Build();

    [SetUp]
    public async Task SetUp() => await testHost.StartAsync();

    [TearDown]
    public async Task TearDown() => await testHost.StopAsync();

    protected T GetRequiredService<T>() => testHost.Services.GetRequiredService<T>();

    protected virtual IHostBuilder CreateHostBuilder() => Host.CreateDefaultBuilder()
            .ConfigureLogging((hostingContext, logging) =>
            {
                logging.AddConsole();
            });
}