namespace ServiceControl.Monitoring.Infrastructure.BackgroundTasks
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using ServiceControl.Infrastructure.BackgroundTasks;

    static class AsyncTimerHostBuilderExtensions
    {
        public static void AddAsyncTimer(this IHostApplicationBuilder hostBuilder) =>
            hostBuilder.Services.AddSingleton<IAsyncTimer, AsyncTimer>();
    }
}