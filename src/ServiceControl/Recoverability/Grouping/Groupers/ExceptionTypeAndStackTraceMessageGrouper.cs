namespace ServiceControl.Recoverability
{
    using System.Linq;
    using ServiceControl.Contracts.Operations;

    class ExceptionTypeAndStackTraceMessageGrouper : IFailureClassifier
    {
        public string Name { get { return "Exception Type and Stack Trace"; } }
        public string ClassifyFailure(FailureDetails failureDetails)
        {
            var exception = failureDetails.Exception;
            if (exception == null || string.IsNullOrWhiteSpace(exception.StackTrace))
                return null;

            var firstStackTraceFrame = StackTraceParser.Parse(exception.StackTrace).FirstOrDefault();
            if (firstStackTraceFrame == null)
                return null;

            return exception.ExceptionType + ": " + firstStackTraceFrame.ToMethodIdentifier();
        }
    }
}