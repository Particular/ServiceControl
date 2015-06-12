namespace ServiceControl.Groups.Groupers
{
    using ServiceControl.MessageFailures.InternalMessages;

    public interface IFailedMessageGrouper
    {
        string GetGroupType(ImportFailedMessage failedMessage);
        string GetGroupName(ImportFailedMessage failedMessage);
    }
}