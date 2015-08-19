namespace ServiceControl.UnitTests.Operations
{
    using NUnit.Framework;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class ExceptionTypeAndStackTraceMessageGrouperTest
    {
        [Test]
        public void Failure_Without_ExceptionDetails_should_not_group()
        {
            var grouper = new ExceptionTypeAndStackTraceMessageGrouper();
            var failureWithoutExceptionDetails =  new FailureDetails();
            var classification = grouper.ClassifyFailure(failureWithoutExceptionDetails);

            Assert.IsNull(classification);
        }

        [Test]
        public void Empty_stack_trace_should_group_by_exception_type()
        {
            var grouper = new ExceptionTypeAndStackTraceMessageGrouper();
            var failureWithEmptyStackTrace = CreateFailureDetailsWithStackTrace(string.Empty);
            var classification = grouper.ClassifyFailure(failureWithEmptyStackTrace);

            Assert.AreEqual("exceptionType: 0", classification);
        }

        [Test]
        public void Null_stack_trace_should_group_by_exception_type()
        {
            var grouper = new ExceptionTypeAndStackTraceMessageGrouper();
            var failureWithNullStackTrace = CreateFailureDetailsWithStackTrace(null);
            var classification = grouper.ClassifyFailure(failureWithNullStackTrace);

            Assert.AreEqual("exceptionType: 0", classification);
        }
        
        [Test]
        public void Non_standard_stack_trace_format_should_group_by_exception_type()
        {
            var grouper = new ExceptionTypeAndStackTraceMessageGrouper();
            var failureWithNonStandardStackTrace = CreateFailureDetailsWithStackTrace("something other than a normal stack trace");
            var classification = grouper.ClassifyFailure(failureWithNonStandardStackTrace);
            
            Assert.AreEqual("exceptionType: 0", classification);
        }

        [Test]
        public void Standard_stack_trace_format_should_group_by_exception_type_and_first_stack_frame()
        {
            const string stackTrace = @"at System.Environment.GetStackTrace(Exception e)
   at System.Environment.GetStackTrace(Exception e)
   at System.Environment.get_StackTrace()
   at Sample.Main()";
            
            var grouper = new ExceptionTypeAndStackTraceMessageGrouper();
            var standardStackTrace = CreateFailureDetailsWithStackTrace(stackTrace);

            var classification = grouper.ClassifyFailure(standardStackTrace);
            Assert.AreEqual(@"exceptionType: System.Environment.GetStackTrace(Exception e)", classification);

        }

        static FailureDetails CreateFailureDetailsWithStackTrace(string stackTrace)
        {
            var failureWithEmptyStackTrace = new FailureDetails
            {
                Exception = new ExceptionDetails
                {
                    StackTrace = stackTrace,
                    ExceptionType = "exceptionType"
                }
            };
            return failureWithEmptyStackTrace;
        }
    }
}
