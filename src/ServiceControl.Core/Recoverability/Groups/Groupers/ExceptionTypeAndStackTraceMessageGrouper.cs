namespace ServiceControl.Recoverability.Groups.Groupers
{
    using System;
    using System.Linq;
    using Particular.Operations.Ingestion.Api;
    using ServiceControl.Contracts.Operations;

    public class ExceptionTypeAndStackTraceMessageGrouper : IFailedMessageGrouper
    {
        public string GroupType
        {
            get { return "ExceptionTypeAndStackTrace"; }
        }

        public string GetGroupName(IngestedMessage actualMessage, FailureDetails failureDetails)
        {
            var exception = failureDetails.Exception;
            if (exception == null || String.IsNullOrWhiteSpace(exception.StackTrace))
                return null;

            var firstStackTraceFrame = StackTraceParser.Parse(exception.StackTrace).FirstOrDefault();
            if (firstStackTraceFrame == null)
                return null;

            return exception.ExceptionType + " was thrown at " + firstStackTraceFrame.ToMethodIdentifier();
        }
    }
}