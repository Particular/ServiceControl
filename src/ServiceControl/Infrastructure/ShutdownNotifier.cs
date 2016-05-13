namespace ServiceControl.Infrastructure
{
    using System;
    using System.Threading;

    public class ShutdownNotifier : IDisposable
    {
        CancellationTokenSource source = new CancellationTokenSource();
        private bool disposed;
        private int disposeSignaled;

        public void Register(Action callback)
        {
            ThrowIfDisposed();

            source.Token.Register(callback);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposeSignaled, 1) != 0)
            {
                return;
            }

            source.Cancel();
            source.Dispose();

            disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException("TimeKeeper");
            }
        }
    }
}
