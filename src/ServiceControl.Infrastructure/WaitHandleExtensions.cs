namespace ServiceControl.Infrastructure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public static class WaitHandleExtensions
    {
        public static async Task<bool> WaitOneAsync(this WaitHandle handle, int millisecondsTimeout, CancellationToken cancellationToken = default)
        {
            RegisteredWaitHandle registeredHandle = null;
            var tokenRegistration = default(CancellationTokenRegistration);
            try
            {
                var tcs = new TaskCompletionSource<bool>();
                registeredHandle = ThreadPool.RegisterWaitForSingleObject(
                    handle,
                    (state, timedOut) => ((TaskCompletionSource<bool>)state).TrySetResult(!timedOut),
                    tcs,
                    millisecondsTimeout,
                    true);
                tokenRegistration = cancellationToken.Register(
                    state => ((TaskCompletionSource<bool>)state).TrySetCanceled(),
                    tcs);
                return await tcs.Task;
            }
            finally
            {
                registeredHandle?.Unregister(null);
                await tokenRegistration.DisposeAsync();
            }
        }

        public static Task<bool> WaitOneAsync(this WaitHandle handle, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return handle.WaitOneAsync((int)timeout.TotalMilliseconds, cancellationToken);
        }

        public static Task<bool> WaitOneAsync(this WaitHandle handle, CancellationToken cancellationToken = default)
        {
            return handle.WaitOneAsync(Timeout.Infinite, cancellationToken);
        }
    }
}