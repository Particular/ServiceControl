namespace ServiceControl.Transport.Tests
{
    using System.Collections.Generic;
    using System.Security.Principal;
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
            var username = WindowsIdentity.GetCurrent().Name;

            await ProvisionQueues(username, queueName, errorQueue, new List<string> { additionalQueue1, additionalQueue2 });

            var dispatcher = await CreateDispatcher(GetTestQueueName("sender"));

            Assert.DoesNotThrowAsync(async () => await dispatcher.SendTestMessage(queueName, "some content"));
            Assert.DoesNotThrowAsync(async () => await dispatcher.SendTestMessage(errorQueue, "some content"));
            Assert.DoesNotThrowAsync(async () => await dispatcher.SendTestMessage(additionalQueue1, "some content"));
            Assert.DoesNotThrowAsync(async () => await dispatcher.SendTestMessage(additionalQueue2, "some content"));
        }
    }
}