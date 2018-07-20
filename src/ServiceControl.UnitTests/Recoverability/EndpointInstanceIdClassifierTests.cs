namespace ServiceControl.UnitTests.Operations
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.Recoverability;
    using static MessageFailures.FailedMessage;

    [TestFixture]
    public class EndpointInstanceIdClassifierTests
    {
        [Test]
        public void Failure_Without_ProcessingAttempt_should_not_group()
        {
            var classifier = new EndpointInstanceClassifier();
            var classification = classifier.ClassifyFailure(new ClassifiableMessageDetails());

            Assert.IsNull(classification);
        }

        [Test]
        public void Failure_With_Core_Headers_In_ProcessingAttempt_should_group()
        {
            var classifier = new EndpointInstanceClassifier();

            var id = Guid.NewGuid().ToString("N");
            var failure = new ProcessingAttempt
            {
                Headers = new Dictionary<string, string>
                {
                    {Headers.HostDisplayName, "Test Host Id"},
                    {"NServiceBus.FailedQ", "Test@machine"},
                    {Headers.HostId, id}
                }
            };

            var classification = classifier.ClassifyFailure(new ClassifiableMessageDetails(null, null, failure));

            Assert.AreEqual(id, classification);
        }
    }
}