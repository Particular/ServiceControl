namespace ServiceControl.UnitTests.Operations
{
    using NUnit.Framework;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class ExceptionTypeAndStackTraceFailureClassifierTest
    {
        [Test]
        public void Failure_Without_ExceptionDetails_should_not_group()
        {
            var classifier = new ExceptionTypeAndStackTraceFailureClassifier();
            var classification = classifier.ClassifyFailure(new ClassifiableMessageDetails());

            Assert.IsNull(classification);
        }

        [Test]
        public void Empty_stack_trace_should_group_by_exception_type()
        {
            var classifier = new ExceptionTypeAndStackTraceFailureClassifier();
            var failureWithEmptyStackTrace = CreateFailureDetailsWithStackTrace(string.Empty);
            var classification = classifier.ClassifyFailure(failureWithEmptyStackTrace);

            Assert.AreEqual("exceptionType: 0", classification);
        }

        [Test]
        public void Null_stack_trace_should_group_by_exception_type()
        {
            var classifier = new ExceptionTypeAndStackTraceFailureClassifier();
            var failureWithNullStackTrace = CreateFailureDetailsWithStackTrace(null);
            var classification = classifier.ClassifyFailure(failureWithNullStackTrace);

            Assert.AreEqual("exceptionType: 0", classification);
        }

        [Test]
        public void Non_standard_stack_trace_format_should_group_by_exception_type()
        {
            var classifier = new ExceptionTypeAndStackTraceFailureClassifier();
            var failureWithNonStandardStackTrace = CreateFailureDetailsWithStackTrace("something other than a normal stack trace");
            var classification = classifier.ClassifyFailure(failureWithNonStandardStackTrace);

            Assert.AreEqual("exceptionType: 0", classification);
        }

        [Test]
        public void Standard_stack_trace_format_should_group_by_exception_type_and_first_stack_frame()
        {
            const string stackTrace = @"at System.Environment.GetStackTrace(Exception e)
   at System.Environment.GetStackTrace(Exception e)
   at System.Environment.get_StackTrace()
   at Sample.Main()";

            var classifier = new ExceptionTypeAndStackTraceFailureClassifier();
            var standardStackTrace = CreateFailureDetailsWithStackTrace(stackTrace);

            var classification = classifier.ClassifyFailure(standardStackTrace);
            Assert.AreEqual(@"exceptionType: System.Environment.GetStackTrace(Exception e)", classification);

        }

        static ClassifiableMessageDetails CreateFailureDetailsWithStackTrace(string stackTrace)
        {
            var failureWithEmptyStackTrace = new FailureDetails
            {
                Exception = new ExceptionDetails
                {
                    StackTrace = stackTrace,
                    ExceptionType = "exceptionType"
                }
            };
            return new ClassifiableMessageDetails
            {
                Details = failureWithEmptyStackTrace
            };
        }
    }
}
