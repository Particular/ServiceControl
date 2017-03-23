namespace ServiceBus.Management.AcceptanceTests.Recoverability.Groups
{
    using System.Collections.Generic;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Recoverability;

    public class When_ServiceControl_has_started : AcceptanceTest
    {
        [Test]
        public void All_classifiers_should_be_retrievable()
        {
            List<string> classifiers = null;

            Define<Context>()
                .Done(x => TryGetMany("/api/recoverability/classifiers", out classifiers))
                .Run();

            Assert.IsNotNull(classifiers, "classifiers is null");
            Assert.IsNotEmpty(classifiers, "No classifiers retrieved");
            Assert.Contains(ExceptionTypeAndStackTraceFailureClassifier.Id, classifiers, "ExceptionTypeAndStackTraceFailureClassifier was not found");
            Assert.Contains(MessageTypeFailureClassifier.Id, classifiers, "MessageTypeFailureClassifier was not found");
            Assert.Contains(AddressOfFailingEndpointClassifier.Id, classifiers, "AddressOfFailingEndpointClassifier was not found");
        }

        class Context : ScenarioContext
        { }
    }
}
