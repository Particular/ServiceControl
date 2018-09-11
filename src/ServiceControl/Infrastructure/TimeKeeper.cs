﻿namespace ServiceControl.Infrastructure
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;

    class TimeKeeper : IDisposable
    {
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposeSignaled, 1) != 0)
            {
                return;
            }

            foreach (var pair in timers)
            {
                WaitAndDispose(pair.Key);
            }

            disposed = true;
        }

        public Timer NewTimer(Func<Task<bool>> callback, TimeSpan dueTime, TimeSpan period)
        {
            ThrowIfDisposed();

            Timer[] timer =
            {
                null
            };

            timer[0] = new Timer(_ =>
            {
                var reschedule = false;

                try
                {
                    reschedule = callback().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    log.Error("Reoccurring timer task failed.", ex);
                }

                var timerSelf = timer[0];
                if (reschedule && timers.ContainsKey(timerSelf))
                {
                    try
                    {
                        timerSelf.Change(period, Timeout.InfiniteTimeSpan);
                    }
                    catch (ObjectDisposedException)
                    {
                        // timer has been disposed already, safe to ignore
                    }
                }
            }, null, dueTime, Timeout.InfiniteTimeSpan);

            timers.TryAdd(timer[0], null);
            return timer[0];
        }

        public Timer NewTimer(Func<bool> callback, TimeSpan dueTime, TimeSpan period)
        {
            ThrowIfDisposed();

            return NewTimer(() => callback() ? TrueTask : FalseTask, dueTime, period);
        }

        public Timer New(Action callback, TimeSpan dueTime, TimeSpan period)
        {
            ThrowIfDisposed();

            return NewTimer(() =>
            {
                callback();
                return true;
            }, dueTime, period);
        }

        public Timer New(Func<Task> callback, TimeSpan dueTime, TimeSpan period)
        {
            ThrowIfDisposed();

            return NewTimer(async () =>
            {
                await callback().ConfigureAwait(false);
                return true;
            }, dueTime, period);
        }

        public void Release(Timer timer)
        {
            ThrowIfDisposed();

            timers.TryRemove(timer, out var _);
            WaitAndDispose(timer);
        }

        static void WaitAndDispose(Timer timer)
        {
            using (var manualResetEvent = new ManualResetEvent(false))
            {
                timer.Dispose(manualResetEvent);
                manualResetEvent.WaitOne();
            }
        }

        void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException("TimeKeeper");
            }
        }

        ConcurrentDictionary<Timer, object> timers = new ConcurrentDictionary<Timer, object>();
        ILog log = LogManager.GetLogger<TimeKeeper>();
        bool disposed;
        int disposeSignaled;
        static Task<bool> TrueTask = Task.FromResult(true);
        static Task<bool> FalseTask = Task.FromResult(true);
    }
}