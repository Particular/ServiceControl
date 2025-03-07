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
            var queueName = GetTestQueueName("queuelength");

            await CreateTestQueue(queueName);

            var onQueueLengthEntryReceived = CreateTaskCompletionSource<QueueLengthEntry>();

            await using var scope = await StartQueueLengthProvider(queueName, (qle) =>
            {
                if (qle.Value > 0)
                {
                    onQueueLengthEntryReceived.TrySetResult(qle);
                }
            });

            await Dispatcher.SendTestMessage(queueName, "some content", configuration.TransportCustomization);

            var queueLengthEntry = await onQueueLengthEntryReceived.Task;

            Assert.That(queueLengthEntry.Value, Is.EqualTo(1));
        }
    }
}