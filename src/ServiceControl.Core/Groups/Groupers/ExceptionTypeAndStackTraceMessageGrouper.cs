namespace ServiceControl.Groups.Groupers
{
    using ServiceControl.MessageFailures.InternalMessages;

    public class ExceptionTypeAndStackTraceMessageGrouper : IFailedMessageGrouper
    {
        public string GetGroupId(ImportFailedMessage failedMessage)
        {
            var exception = failedMessage.FailureDetails.Exception;
            var firstStackTraceFrame = StackTraceParser.Parse(exception.StackTrace)[0];
            return exception.ExceptionType + "." + firstStackTraceFrame.ToMethodIdentifier();
        }

        public string GetGroupName(ImportFailedMessage failedMessage)
        {
            var exception = failedMessage.FailureDetails.Exception;
            var firstStackTraceFrame = StackTraceParser.Parse(exception.StackTrace)[0];
            return exception.ExceptionType + " was thrown at " + firstStackTraceFrame.ToMethodIdentifier();
        }
    }
}