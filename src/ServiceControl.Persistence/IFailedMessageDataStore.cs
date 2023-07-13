namespace ServiceControl.Persistence
{
    using System;
    using System.Threading.Tasks;

    public interface IFailedMessageDataStore
    {
        IDisposable Subscribe(Func<FailedMessageTotals, Task> callback);
    }
}