namespace ServiceControl.Infrastructure.BackgroundTasks
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

    class AsyncTimerHostedService : IHostedService
    {
        readonly AsyncTimer timer;

        public AsyncTimerHostedService(AsyncTimer timer) => this.timer = timer;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            timer.Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}