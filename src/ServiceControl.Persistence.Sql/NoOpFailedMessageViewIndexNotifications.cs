namespace ServiceControl.Persistence.Sql;

using System;
using System.Threading.Tasks;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence;

class NoOpFailedMessageViewIndexNotifications : IFailedMessageViewIndexNotifications
{
    public IDisposable Subscribe(Func<FailedMessageTotals, Task> callback) => new NoOpDisposable();

    class NoOpDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
