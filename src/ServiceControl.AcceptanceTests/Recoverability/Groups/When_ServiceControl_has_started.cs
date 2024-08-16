﻿namespace ServiceControl.AcceptanceTests.Recoverability.Groups
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Recoverability;

    class When_ServiceControl_has_started : AcceptanceTest
    {
        [Test]
        public async Task All_classifiers_should_be_retrievable()
        {
            List<string> classifiers = null;

            await Define<Context>()
                .Done(async x =>
                {
                    var result = await this.TryGetMany<string>("/api/recoverability/classifiers");
                    classifiers = result;
                    return result;
                })
                .Run();

            Assert.That(classifiers, Is.Not.Null, "classifiers is null");
            Assert.That(classifiers, Is.Not.Empty, "No classifiers retrieved");
            Assert.Contains(ExceptionTypeAndStackTraceFailureClassifier.Id, classifiers, "ExceptionTypeAndStackTraceFailureClassifier was not found");
            Assert.Contains(MessageTypeFailureClassifier.Id, classifiers, "MessageTypeFailureClassifier was not found");
            Assert.Contains(AddressOfFailingEndpointClassifier.Id, classifiers, "AddressOfFailingEndpointClassifier was not found");
        }

        class Context : ScenarioContext
        {
        }
    }
}