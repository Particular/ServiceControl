namespace ServiceControl.Recoverability
{
    using System.Linq;

    public class ExceptionTypeAndStackTraceFailureClassifier : IFailureClassifier
    {
        public const string Id = "Exception Type and Stack Trace";

        public string Name => Id;

        public string ClassifyFailure(ClassifiableMessageDetails failure)
        {
            var exception = failure.Details?.Exception;

            if (exception == null)
                return null;

            if(string.IsNullOrWhiteSpace(exception.StackTrace))
                return GetNonStandardClassification(exception.ExceptionType);

            var firstStackTraceFrame = StackTraceParser.Parse(exception.StackTrace).FirstOrDefault();
            if (firstStackTraceFrame != null)
                return exception.ExceptionType + ": " + firstStackTraceFrame.ToMethodIdentifier();

            return GetNonStandardClassification(exception.ExceptionType);
        }

        public bool ApplyToNewFailures => true;

        static string GetNonStandardClassification(string exceptionType)
        {
            return exceptionType + ": 0";
        }
    }
}