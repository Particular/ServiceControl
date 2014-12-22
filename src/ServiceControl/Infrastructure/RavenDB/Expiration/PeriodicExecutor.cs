namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class PeriodicExecutor
    {
        readonly Action action;
        readonly TimeSpan period;
        DateTime lastStartedAt;
        CancellationTokenSource tokenSource;
        ManualResetEventSlim resetEvent;

        public PeriodicExecutor(Action action, TimeSpan period)
        {
            this.action = action;
            this.period = period;
        }

        public void Start(bool delay)
        {
            lock (this)
            {
                if (tokenSource != null)
                {
                    throw new InvalidOperationException("Executor has already been started");
                }
                tokenSource = new CancellationTokenSource();
                resetEvent = new ManualResetEventSlim(false);
                if (delay)
                {
                    Task.Delay(period, tokenSource.Token).ContinueWith(task =>
                    {
                        if (task.Status == TaskStatus.RanToCompletion)
                        {
                            Trigger();
                        }
                        else
                        {
                            resetEvent.Set();
                        }
                    });
                }
                else
                {
                    Trigger();
                }
            }
        }

        public void Stop(CancellationToken token)
        {
            lock (this)
            {
                if (tokenSource == null)
                {
                    throw new InvalidOperationException("Executor has not been started");
                }
                tokenSource.Cancel();
                resetEvent.Wait(token);
            }
        }

        void Trigger()
        {
            Task.Factory.StartNew(() =>
            {
                lastStartedAt = DateTime.Now;
                action();
            })
                .ContinueWith(x =>
                {
                    if (tokenSource.IsCancellationRequested)
                    {
                        resetEvent.Set(); 
                        return;
                    }
                    var duration = DateTime.Now - lastStartedAt;
                    if (duration > period)
                    {
                        Trigger();
                    }
                    else
                    {
                        Task.Delay(period - duration, tokenSource.Token)
                            .ContinueWith(task =>
                            {
                                if (task.Status == TaskStatus.RanToCompletion)
                                {
                                    Trigger();
                                }
                                else
                                {
                                    resetEvent.Set();
                                }
                            });
                    }
                });
        }
    }
}