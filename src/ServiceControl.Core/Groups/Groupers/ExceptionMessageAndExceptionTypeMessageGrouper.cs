namespace ServiceControl.Groups.Groupers
{
    using ServiceControl.MessageFailures.InternalMessages;

    public class ExceptionMessageAndExceptionTypeMessageGrouper : IFailedMessageGrouper
    {
        public string GroupType
        {
            get { return "ExceptionMessageAndExceptionType"; }
        }

        public string GetGroupName(ImportFailedMessage failedMessage)
        {
            return failedMessage.FailureDetails.Exception.ExceptionType + " : " + failedMessage.FailureDetails.Exception.Message;
        }
    }
}