namespace ServiceControl.Infrastructure.BackgroundTasks
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    static class AsyncTimerHostBuilderExtensions
    {
        public static IHostBuilder UseAsyncTimer(this IHostBuilder hostBuilder) =>
            hostBuilder.ConfigureServices(services =>
            {
                var asyncTimer = new AsyncTimer();
                services.AddSingleton<IAsyncTimer>(asyncTimer);
            });
    }
}
