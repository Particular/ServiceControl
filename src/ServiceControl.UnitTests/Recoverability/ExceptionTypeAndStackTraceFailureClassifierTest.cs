namespace ServiceControl.UnitTests.Operations
{
    using Contracts.Operations;
    using NUnit.Framework;
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
        public void Null_message_should_group_by_exception_type()
        {
            var classifier = new ExceptionTypeAndStackTraceFailureClassifier();
            var failureWithNullMessage = CreateFailureDetailsWithMessage(null);
            var classification = classifier.ClassifyFailure(failureWithNullMessage);

            Assert.AreEqual("exceptionType: 0", classification);
        }

        [Test]
        public void Empty_message_should_group_by_exception_type()
        {
            var classifier = new ExceptionTypeAndStackTraceFailureClassifier();
            var failureWithEmptyMessage = CreateFailureDetailsWithMessage(string.Empty);
            var classification = classifier.ClassifyFailure(failureWithEmptyMessage);

            Assert.AreEqual("exceptionType: 0", classification);
        }

        [Test]
        public void Whitespace_message_should_group_by_exception_type()
        {
            var classifier = new ExceptionTypeAndStackTraceFailureClassifier();
            var failureWithWhitespaceMessage = CreateFailureDetailsWithMessage("   ");
            var classification = classifier.ClassifyFailure(failureWithWhitespaceMessage);

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

        [Test]
        public void Stack_with_lots_of_inner_exceptions_should_group_by_exception_type_and_first_stack_frame()
        {
            const string stackTrace = @"System.InvalidOperationException: Handler was not found for request of type Particular.Messages.Events.OffsetFromNowUpdated.
Container or service locator not configured properly or handlers not registered with your container. ---> Autofac.Core.DependencyResolutionException: An error occurred during the activation of a particular registration. See the inner exception for details. Registration: Activator = INotificationHandler`1[] (DelegateActivator), Services = [System.Collections.Generic.IEnumerable`1[[MediatR.INotificationHandler`1[[Particular.Messages.Events.OffsetFromNowUpdated, Particular, Version=1.0.0.1687, Culture=neutral, PublicKeyToken=null]], MediatR, Version=2.1.0.0, Culture=neutral, PublicKeyToken=null]]], Lifetime = Autofac.Core.Lifetime.CurrentScopeLifetime, Sharing = None, Ownership = ExternallyOwned ---> An error occurred during the activation of a particular registration. See the inner exception for details. Registration: Activator = OffsetTimeoutUpdater (ReflectionActivator), Services = [MediatR.INotificationHandler`1[[Particular.Messages.Events.OffsetFromNowUpdated, Particular, Version=1.0.0.1687, Culture=neutral, PublicKeyToken=null]]], Lifetime = Autofac.Core.Lifetime.CurrentScopeLifetime, Sharing = None, Ownership = OwnedByLifetimeScope ---> An error occurred during the activation of a particular registration. See the inner exception for details. Registration: Activator = TimeoutRepository (ReflectionActivator), Services = [Particular.NServiceBus.NHibernate.Persistence.ITimeoutRepository], Lifetime = Autofac.Core.Lifetime.CurrentScopeLifetime, Sharing = None, Ownership = OwnedByLifetimeScope ---> None of the constructors found with 'Autofac.Core.Activators.Reflection.DefaultConstructorFinder' on type 'Particular.NServiceBus.NHibernate.Persistence.TimeoutRepository' can be invoked with the available services and parameters:
Cannot resolve parameter 'NHibernate.ISession session' of constructor 'Void .ctor(NHibernate.ISession)'. (See inner exception for details.) (See inner exception for details.) (See inner exception for details.) ---> Autofac.Core.DependencyResolutionException: An error occurred during the activation of a particular registration. See the inner exception for details. Registration: Activator = OffsetTimeoutUpdater (ReflectionActivator), Services = [MediatR.INotificationHandler`1[[Particular.Messages.Events.OffsetFromNowUpdated, Particular, Version=1.0.0.1687, Culture=neutral, PublicKeyToken=null]]], Lifetime = Autofac.Core.Lifetime.CurrentScopeLifetime, Sharing = None, Ownership = OwnedByLifetimeScope ---> An error occurred during the activation of a particular registration. See the inner exception for details. Registration: Activator = TimeoutRepository (ReflectionActivator), Services = [Particular.NServiceBus.NHibernate.Persistence.ITimeoutRepository], Lifetime = Autofac.Core.Lifetime.CurrentScopeLifetime, Sharing = None, Ownership = OwnedByLifetimeScope ---> None of the constructors found with 'Autofac.Core.Activators.Reflection.DefaultConstructorFinder' on type 'Particular.NServiceBus.NHibernate.Persistence.TimeoutRepository' can be invoked with the available services and parameters:
Cannot resolve parameter 'NHibernate.ISession session' of constructor 'Void .ctor(NHibernate.ISession)'. (See inner exception for details.) (See inner exception for details.) ---> Autofac.Core.DependencyResolutionException: An error occurred during the activation of a particular registration. See the inner exception for details. Registration: Activator = TimeoutRepository (ReflectionActivator), Services = [Particular.NServiceBus.NHibernate.Persistence.ITimeoutRepository], Lifetime = Autofac.Core.Lifetime.CurrentScopeLifetime, Sharing = None, Ownership = OwnedByLifetimeScope ---> None of the constructors found with 'Autofac.Core.Activators.Reflection.DefaultConstructorFinder' on type 'Particular.NServiceBus.NHibernate.Persistence.TimeoutRepository' can be invoked with the available services and parameters:
Cannot resolve parameter 'NHibernate.ISession session' of constructor 'Void .ctor(NHibernate.ISession)'. (See inner exception for details.) ---> Autofac.Core.DependencyResolutionException: None of the constructors found with 'Autofac.Core.Activators.Reflection.DefaultConstructorFinder' on type 'Particular.NServiceBus.NHibernate.Persistence.TimeoutRepository' can be invoked with the available services and parameters:
Cannot resolve parameter 'NHibernate.ISession session' of constructor 'Void .ctor(NHibernate.ISession)'.
   at Autofac.Core.Activators.Reflection.ReflectionActivator.ActivateInstance(IComponentContext context, IEnumerable`1 parameters)
   at Autofac.Core.Resolving.InstanceLookup.Activate(IEnumerable`1 parameters)
   --- End of inner exception stack trace ---";

            var classifier = new ExceptionTypeAndStackTraceFailureClassifier();
            var standardStackTrace = CreateFailureDetailsWithStackTrace(stackTrace);

            var classification = classifier.ClassifyFailure(standardStackTrace);
            Assert.AreEqual(@"exceptionType: Autofac.Core.Activators.Reflection.ReflectionActivator.ActivateInstance(IComponentContext context, IEnumerable`1 parameters)", classification);
        }

        [Test]
        public void Stack_with_source_code_path_starting_with_forward_slash_should_parse_correctly()
        {
            const string stackTrace = @"SomeException: Some error message
at Custom.Handlers.MyHandler.Handle(MyMessage message, IMessageHandlerContext context) in /source/Handlers/MyHandler.cs:line 32
at NServiceBus.InvokeHandlerTerminator.Terminate(IInvokeHandlerContext context)
at NServiceBus.SagaAudit.AuditInvokedSagaBehavior.Invoke(IInvokeHandlerContext context, Func`1 next)
at NServiceBus.SagaPersistenceBehavior.Invoke(IInvokeHandlerContext context, Func`2 next)";

            var classifier = new ExceptionTypeAndStackTraceFailureClassifier();
            var standardStackTrace = CreateFailureDetailsWithStackTrace(stackTrace);

            var classification = classifier.ClassifyFailure(standardStackTrace);
            Assert.AreEqual(@"exceptionType: Custom.Handlers.MyHandler.Handle(MyMessage message, IMessageHandlerContext context)", classification);
        }

        static ClassifiableMessageDetails CreateFailureDetailsWithStackTrace(string stackTrace)
        {
            var failure = new FailureDetails
            {
                Exception = new ExceptionDetails
                {
                    StackTrace = stackTrace,
                    ExceptionType = "exceptionType"
                }
            };
            return new ClassifiableMessageDetails(null, failure, null);
        }

        static ClassifiableMessageDetails CreateFailureDetailsWithMessage(string message)
        {
            var failure = new FailureDetails
            {
                Exception = new ExceptionDetails
                {
                    StackTrace = "Stack trace",
                    ExceptionType = "exceptionType",
                    Message = message
                }
            };
            return new ClassifiableMessageDetails(null, failure, null);
        }
    }
}