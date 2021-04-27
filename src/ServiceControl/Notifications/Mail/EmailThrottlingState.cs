namespace ServiceControl.Notifications.Mail
{
    using System.Threading;

    public class EmailThrottlingState
    {
        public SemaphoreSlim Semaphore = new SemaphoreSlim(1);
        bool throttling;

        public bool IsThrottling() => Volatile.Read(ref throttling);

        public void ThrottlingOn() => Volatile.Write(ref throttling, true);
        public void ThrottlingOff() => Volatile.Write(ref throttling, false);
    }
}