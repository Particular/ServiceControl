namespace ServiceControl.Groups.Groupers
{
    using ServiceControl.MessageFailures.InternalMessages;

    public interface IFailedMessageGrouper
    {
        string GetGroupId(ImportFailedMessage failedMessage);
        string GetGroupName(ImportFailedMessage failedMessage);
    }
}