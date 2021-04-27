namespace ServiceControl.Notifications.Mail
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class EmailThrottlingState
    {
        SemaphoreSlim semaphore = new SemaphoreSlim(1);
        int latestFailureNumber = 0;
        DateTime? lastSendError;

        public async Task<int> NextFailure()
        {
            try
            {
                await semaphore.WaitAsync().ConfigureAwait(false);

                return ++latestFailureNumber;
            }
            finally
            {
                semaphore.Release(1);
            }
        }

        public Task Wait() => semaphore.WaitAsync();

        public void Release() => semaphore.Release(1);

        public async bool Throttling()
        {

        }
        public async Task RegisterSendError()
        {
            try
            {
                await semaphore.WaitAsync().ConfigureAwait(false);

                lastSendError = DateTime.UtcNow;
            }
            finally
            {
                semaphore.Release(1);
            }
        }
    }
}