namespace ServiceControl.Notifications.Email
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

    class EmailNotificationHostedService : IHostedService
    {
        EmailThrottlingState throttlingState;

        public EmailNotificationHostedService(EmailThrottlingState throttlingState) => this.throttlingState = throttlingState;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            throttlingState.CancellationTokenSource.Cancel();

            return Task.CompletedTask;
        }
    }
}