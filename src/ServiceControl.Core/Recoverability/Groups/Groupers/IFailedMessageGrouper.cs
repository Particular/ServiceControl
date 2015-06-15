namespace ServiceControl.Recoverability.Groups.Groupers
{
    using ServiceControl.MessageFailures.InternalMessages;

    public interface IFailedMessageGrouper
    {
        string GroupType { get; }
        string GetGroupName(ImportFailedMessage failedMessage);
    }
}