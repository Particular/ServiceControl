namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.Extensions.Hosting;

public class FailedMessageViewIndexNotifications : IFailedMessageViewIndexNotifications, IHostedService
{
    public IDisposable Subscribe(Func<FailedMessageTotals, Task> callback)
    {
        //todo:
        return Task.CompletedTask;
    }


    public Task StartAsync(CancellationToken cancellationToken)
    {
        //todo:
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        //todo:
        return Task.CompletedTask;
    }
}
