namespace ServiceControl.UnitTests.Operations
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
            var classification = classifier.ClassifyFailure(new ClassifiableMessageDetails
            {
                MessageType = GetType().ToString()
            });

            Assert.IsNotNull(classification);
        }

        [Test]
        public void Failure_Without_MessageType_should_not_group()
        {
            var classifier = new MessageTypeFailureClassifier();
            var classification = classifier.ClassifyFailure(new ClassifiableMessageDetails());

            Assert.IsNull(classification);
        }
    }
}