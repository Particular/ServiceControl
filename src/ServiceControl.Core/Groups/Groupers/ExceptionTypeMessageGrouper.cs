namespace ServiceControl.Groups.Groupers
{
    using ServiceControl.MessageFailures.InternalMessages;

    public class ExceptionTypeMessageGrouper : IFailedMessageGrouper
    {
        public string GetGroupId(ImportFailedMessage failedMessage)
        {
            return failedMessage.FailureDetails.Exception.ExceptionType; 
        }

        public string GetGroupName(ImportFailedMessage failedMessage)
        {
            return failedMessage.FailureDetails.Exception.ExceptionType; 
        }
    }
}