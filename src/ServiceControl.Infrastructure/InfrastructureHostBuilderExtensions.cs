namespace ServiceControl.Infrastructure.BackgroundTasks
{
    using Connection;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    public static class InfrastructureHostBuilderExtensions
    {
        public static IHostBuilder UseCommonInfrastructure(this IHostBuilder hostBuilder) =>
            hostBuilder.ConfigureServices(services =>
            {
                var asyncTimer = new AsyncTimer();
                services.AddSingleton<IAsyncTimer>(asyncTimer);

                services.AddSingleton<IPlatformConnectionBuilder, PlatformConnectionBuilder>();
            });
    }
}
