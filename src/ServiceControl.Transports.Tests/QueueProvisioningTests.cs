namespace ServiceControl.Transport.Tests
{
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    class QueueProvisioningTests : TransportTestFixture
    {
        [Test]
        public async Task Should_provision_queues()
        {
            var queueName = GetTestQueueName("provision");
            var errorQueue = queueName + ".error";
            var additionalQueue1 = queueName + ".extra1";
            var additionalQueue2 = queueName + ".extra2";

            await ProvisionQueues(queueName, errorQueue, [additionalQueue1, additionalQueue2]);

            Assert.DoesNotThrowAsync(async () => await Dispatcher.SendTestMessage(queueName, "some content", configuration.TransportCustomization));
            Assert.DoesNotThrowAsync(async () => await Dispatcher.SendTestMessage(errorQueue, "some content", configuration.TransportCustomization));
            Assert.DoesNotThrowAsync(async () => await Dispatcher.SendTestMessage(additionalQueue1, "some content", configuration.TransportCustomization));
            Assert.DoesNotThrowAsync(async () => await Dispatcher.SendTestMessage(additionalQueue2, "some content", configuration.TransportCustomization));
        }
    }
}