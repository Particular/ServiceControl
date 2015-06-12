namespace ServiceControl.Groups.Groupers
{
    using ServiceControl.MessageFailures.InternalMessages;

    public class ExceptionTypeMessageGrouper : IFailedMessageGrouper
    {
        public string GetGroupType(ImportFailedMessage failedMessage)
        {
            return "ExceptionType";
        }

        public string GetGroupName(ImportFailedMessage failedMessage)
        {
            return failedMessage.FailureDetails.Exception.ExceptionType; 
        }
    }
}