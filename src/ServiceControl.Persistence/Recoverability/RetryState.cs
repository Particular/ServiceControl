namespace ServiceControl.Recoverability
{
    public enum RetryState
    {
        Waiting,
        Preparing,
        Forwarding,
        Completed
    }
}