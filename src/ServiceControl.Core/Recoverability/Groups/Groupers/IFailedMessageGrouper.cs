namespace ServiceControl.Recoverability.Groups.Groupers
{
    using ServiceControl.MessageFailures;

    public interface IFailedMessageGrouper
    {
        string GroupType { get; }
        string GetGroupName(MessageFailureHistory messageFailure);
    }
}