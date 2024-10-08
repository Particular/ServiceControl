﻿namespace ServiceControl.UnitTests.Operations
{
    using NUnit.Framework;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class MessageTypeFailureClassifierTest
    {
        [Test]
        public void Failure_With_MessageType_should_group()
        {
            var classifier = new MessageTypeFailureClassifier();
            var classification = classifier.ClassifyFailure(new ClassifiableMessageDetails(GetType().ToString(), null, null));

            Assert.That(classification, Is.Not.Null);
        }

        [Test]
        public void Failure_Without_MessageType_should_not_group()
        {
            var classifier = new MessageTypeFailureClassifier();
            var classification = classifier.ClassifyFailure(new ClassifiableMessageDetails());

            Assert.That(classification, Is.Null);
        }
    }
}