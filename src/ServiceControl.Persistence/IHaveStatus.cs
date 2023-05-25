namespace ServiceControl.MessageFailures
{
    public interface IHaveStatus
    {
        FailedMessageStatus Status { get; }
    }
}