namespace ServiceControl.Recoverability
{
    using System.Linq;
    using ServiceControl.Contracts.Operations;

    public class ExceptionTypeAndStackTraceMessageGrouper : IFailureClassifier
    {
        public string Name => "Exception Type and Stack Trace";

        public string ClassifyFailure(FailureDetails failureDetails)
        {
            var exception = failureDetails.Exception;
            
            if (exception == null)
                return null;

            if(string.IsNullOrWhiteSpace(exception.StackTrace))
                return GetNonStandardClassification(exception.ExceptionType);

            var firstStackTraceFrame = StackTraceParser.Parse(exception.StackTrace).FirstOrDefault();
            if (firstStackTraceFrame != null)
                return $"{exception.ExceptionType}: {firstStackTraceFrame.ToMethodIdentifier()}";

            return GetNonStandardClassification(exception.ExceptionType);
        }

        static string GetNonStandardClassification(string exceptionType)
        {
            return $"{exceptionType}: 0";
        }
    }
}