namespace ServiceControl.Groups.Groupers
{
    using ServiceControl.MessageFailures.InternalMessages;

    public class ExceptionMessageAndExceptionTypeMessageGrouper : IFailedMessageGrouper
    {
        public string GetGroupId(ImportFailedMessage failedMessage)
        {
            return failedMessage.FailureDetails.Exception.ExceptionType + failedMessage.FailureDetails.Exception.Message; 
        }

        public string GetGroupName(ImportFailedMessage failedMessage)
        {
            return failedMessage.FailureDetails.Exception.ExceptionType + " : " + failedMessage.FailureDetails.Exception.Message; 
        }
    }
}