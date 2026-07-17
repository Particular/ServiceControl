namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.Extensions.Hosting;

public class FailedMessageViewIndexNotifications : IFailedMessageViewIndexNotifications, IHostedService
{
    public IDisposable Subscribe(Func<FailedMessageTotals, Task> callback) =>
        throw new NotImplementedException();

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
