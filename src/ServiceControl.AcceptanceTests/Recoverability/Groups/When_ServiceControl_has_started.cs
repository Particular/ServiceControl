namespace ServiceBus.Management.AcceptanceTests.Recoverability.Groups
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Recoverability;

    public class When_ServiceControl_has_started : AcceptanceTest
    {
        [Test]
        public async Task All_classifiers_should_be_retrievable()
        {
            List<string> classifiers = null;

            await Define<Context>()
                .Done(async x =>
                {
                    var result =  await TryGetMany<string>("/api/recoverability/classifiers");
                    classifiers = result;
                    return result;
                })
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
