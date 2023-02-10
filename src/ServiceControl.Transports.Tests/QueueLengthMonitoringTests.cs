namespace ServiceControl.Transport.Tests
{
    using System.Threading.Tasks;
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
                    onQueueLengthEntryReceived.TrySetResult(qle);
                }
            });

            await dispatcher.SendTestMessage(queueName, "some content");

            var queueLengthEntry = await onQueueLengthEntryReceived.Task;

            Assert.AreEqual(1, queueLengthEntry.Value);
        }
    }
}