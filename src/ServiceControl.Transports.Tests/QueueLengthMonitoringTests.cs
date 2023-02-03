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
            var queueName = "testqueue";

            var onQueueLengthEntryReceived = CreateTaskCompletionSource<QueueLengthEntry>();

            await StartQueueLengthProvider(queueName, (qle) => onQueueLengthEntryReceived.SetResult(qle));

            var queueLengthEntry = await onQueueLengthEntryReceived.Task;
            Assert.AreEqual(0, queueLengthEntry.Value);
        }
    }
}