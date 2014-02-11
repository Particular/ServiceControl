using System;

namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System.Threading;

    public static class CancellationTokenSourceExtensions
    {
        public static CancellationTimeout TimeoutAfter(this CancellationTokenSource cts, TimeSpan dueTime)
        {
            return new CancellationTimeout(cts, dueTime);
        }

        public class CancellationTimeout : IDisposable
        {
            private readonly CancellationTokenSource source;
            private readonly Timer timer;
            private readonly long dueTime;

            public CancellationTimeout(CancellationTokenSource source, TimeSpan dueTime)
            {
                if (source == null)
                    throw new ArgumentNullException("source");
                if (dueTime < TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException("dueTime");

                this.source = source;
                this.dueTime = (long)dueTime.TotalMilliseconds;
                timer = new Timer(self =>
                {
                    timer.Dispose();
                    try
                    {
                        this.source.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }, null, this.dueTime, -1);
            }

            public void Delay()
            {
                timer.Change(dueTime, -1);
            }

            public void Dispose()
            {
                timer.Dispose();
            }
        }

    }
}
