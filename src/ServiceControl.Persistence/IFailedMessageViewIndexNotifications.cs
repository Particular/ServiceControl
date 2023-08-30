namespace ServiceControl.Persistence
{
    using System;
    using System.Threading.Tasks;

    public interface IFailedMessageViewIndexNotifications
    {
        IDisposable Subscribe(Func<FailedMessageTotals, Task> callback);
    }
}