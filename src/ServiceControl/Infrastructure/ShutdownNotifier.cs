namespace ServiceControl.Infrastructure
{
    using System;
    using System.Threading;

    class ShutdownNotifier : IDisposable
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

        void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException("TimeKeeper");
            }
        }

        CancellationTokenSource source = new CancellationTokenSource();
        bool disposed;
        int disposeSignaled;
    }
}