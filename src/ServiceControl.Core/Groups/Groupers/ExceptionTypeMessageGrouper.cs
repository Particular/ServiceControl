namespace ServiceControl.Groups.Groupers
{
    using ServiceControl.MessageFailures.InternalMessages;

    public class ExceptionTypeMessageGrouper : IFailedMessageGrouper
    {
        public string GroupType
        {
            get { return "ExceptionType"; }
        }

        public string GetGroupName(ImportFailedMessage failedMessage)
        {
            return failedMessage.FailureDetails.Exception.ExceptionType;
        }
    }
}