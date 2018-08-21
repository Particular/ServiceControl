namespace ServiceControl.Infrastructure
{
    using System;
    using System.Threading;

    public class ShutdownNotifier : IDisposable
    {
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

        public void Register(Action callback)
        {
            ThrowIfDisposed();

            source.Token.Register(callback);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException("TimeKeeper");
            }
        }

        CancellationTokenSource source = new CancellationTokenSource();
        private bool disposed;
        private int disposeSignaled;
    }
}