namespace ServiceControl.Recoverability.Groups.Groupers
{
    using System;
    using System.Linq;
    using NServiceBus.Logging;
    using ServiceControl.MessageFailures;

    public class ExceptionTypeAndStackTraceMessageGrouper : IFailedMessageGrouper
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(ExceptionTypeAndStackTraceMessageGrouper));

        public string GroupType
        {
            get { return "ExceptionTypeAndStackTrace"; }
        }

        public string GetGroupName(MessageFailureHistory messageFailure)
        {
            var defaultName = GroupType;

            if (messageFailure.ProcessingAttempts == null)
                return defaultName;
            
            var lastAttempt = messageFailure.ProcessingAttempts.Last();
            if (lastAttempt.FailureDetails == null)
                return defaultName;

            var exception = lastAttempt.FailureDetails.Exception;
            if (exception == null || String.IsNullOrWhiteSpace(exception.StackTrace))
                return defaultName;

            var firstStackTraceFrame = StackTraceParser.Parse(exception.StackTrace).FirstOrDefault();
            if (firstStackTraceFrame == null)
                return defaultName;


            var groupName = exception.ExceptionType + " was thrown at " + firstStackTraceFrame.ToMethodIdentifier();
            Logger.InfoFormat("Grouped message {0} into group with name \"{1}\".", lastAttempt.MessageId, groupName);

            return groupName;
        }
    }
}