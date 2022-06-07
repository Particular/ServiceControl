namespace ServiceControl.Infrastructure.BackgroundTasks
{
    using Connection;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    public static class InfrastructureHostBuilderExtensions
    {
        public static IHostBuilder UseCommonInfrastructure(this IHostBuilder hostBuilder, RemoteInstanceSetting[] remoteInstances, string localApiUrl) =>
            hostBuilder.ConfigureServices(services =>
            {
                var asyncTimer = new AsyncTimer();
                services.AddSingleton<IAsyncTimer>(asyncTimer);

                services.AddSingleton<IPlatformConnectionBuilder, PlatformConnectionBuilder>();

                services.AddSingleton(sb =>
                {
                    return new RemoteInstanceSettings(remoteInstances, localApiUrl);
                });

                services.AddPlatformConnectionProvider<RemotePlatformConnectionDetailsProvider>();
            });
    }
}
