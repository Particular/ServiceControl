namespace ServiceControl.Infrastructure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ITimeKeeper
    {
        Timer NewTimer(Func<Task<bool>> callback, TimeSpan dueTime, TimeSpan period);
        Timer NewTimer(Func<bool> callback, TimeSpan dueTime, TimeSpan period);
        Timer New(Action callback, TimeSpan dueTime, TimeSpan period);
        void Release(Timer timer);
    }
}