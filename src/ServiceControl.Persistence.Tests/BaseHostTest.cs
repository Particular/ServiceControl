using Microsoft.Extensions.Configuration;
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
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureLogging((hostingContext, logging) =>
            {
                logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                logging.AddConsole();
            });
}