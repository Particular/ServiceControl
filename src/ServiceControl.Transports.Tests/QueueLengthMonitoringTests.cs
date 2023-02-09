namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.Transports;

    [TestFixture]
    class QueueLengthMonitoringTests : TransportTestFixture
    {
        [Test]
        public async Task Should_report_queue_length()
        {
            var queueName = GetTestQueueName("queuelenght");

            var onQueueLengthEntryReceived = CreateTaskCompletionSource<QueueLengthEntry>();

            var dispatcher = await StartQueueLengthProvider(queueName, (qle) =>
            {
                if (qle.Value > 0)
                {
                    onQueueLengthEntryReceived.SetResult(qle);
                }
            });

            var transportOperation = new TransportOperation(
                new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(),
                    Encoding.UTF8.GetBytes("test")),
                new UnicastAddressTag(queueName), DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(transportOperation), new TransportTransaction(), new ContextBag());

            var queueLengthEntry = await onQueueLengthEntryReceived.Task;

            Assert.AreEqual(1, queueLengthEntry.Value);
        }
    }
}