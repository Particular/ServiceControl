namespace ServiceControl.Infrastructure.BackgroundTasks
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    static class AsyncTimerHostBuilderExtensions
    {
        public static void AddAsyncTimer(this IHostApplicationBuilder hostBuilder) =>
            hostBuilder.Services.AddSingleton<IAsyncTimer, AsyncTimer>();
    }
}