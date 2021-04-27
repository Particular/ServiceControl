namespace ServiceControl.Notifications.Email
{
    using System.Threading;

    public class EmailThrottlingState
    {
        public SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(1);
        public string RetriedMessageId { get; set; }
        bool throttling;

        public bool IsThrottling() => Volatile.Read(ref throttling);

        public void ThrottlingOn() => Volatile.Write(ref throttling, true);
        public void ThrottlingOff() => Volatile.Write(ref throttling, false);
    }
}