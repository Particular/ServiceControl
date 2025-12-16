namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Threading.Tasks;
using ServiceControl.Persistence;

public class FailedMessageViewIndexNotifications : IFailedMessageViewIndexNotifications
{
    public IDisposable Subscribe(Func<FailedMessageTotals, Task> callback)
    {
        // For SQL persistence, we don't have real-time index change notifications
        // like RavenDB does. The callback would need to be triggered manually
        // when failed message data changes. For now, return a no-op disposable.
        return new NoOpDisposable();
    }

    class NoOpDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
