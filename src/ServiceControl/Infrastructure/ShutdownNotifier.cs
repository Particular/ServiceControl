namespace ServiceControl.Infrastructure
{
    using System;
    using System.Threading;

    public class ShutdownNotifier : IDisposable
    {
        CancellationTokenSource source = new CancellationTokenSource();

        public void Register(Action callback)
        {
            source.Token.Register(callback);
        }

        public void Dispose()
        {
            source.Cancel();
            source.Dispose();
        }
    }
}
