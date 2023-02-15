namespace ServiceControl.Transport.Tests
{
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    class QueueIngestionTests : TransportTestFixture
    {
        [Test]
        public async Task Should_ingest_all_messages()
        {
            var queueName = GetTestQueueName("ingestion");
            var numMessagesToIngest = 10;
            var onMessagesProcessed = CreateTaskCompletionSource<bool>();

            var numMessagesIngested = 0;

            var dispatcher = await StartQueueIngestor(
                queueName,
                (_) =>
                {
                    numMessagesIngested++;

                    if (numMessagesIngested == numMessagesToIngest)
                    {
                        onMessagesProcessed.SetResult(true);
                    }

                    return Task.CompletedTask;
                },
                (_) => { Assert.Fail("There should be no errors"); return Task.FromResult(NServiceBus.Transport.ErrorHandleResult.Handled); });

            for (int i = 0; i < numMessagesToIngest; i++)
            {
                await dispatcher.SendTestMessage(queueName, $"message{i}");
            }

            var allMessagesProcessed = await onMessagesProcessed.Task;

            Assert.True(allMessagesProcessed);
        }

        [Test]
        public async Task Should_trigger_on_error()
        {
            var queueName = GetTestQueueName("ingestionfailure");
            var onErrorCalled = CreateTaskCompletionSource<bool>();

            var dispatcher = await StartQueueIngestor(
                queueName,
                (_) => throw new System.Exception("Some failure"),
                (_) =>
                {
                    onErrorCalled.SetResult(true);
                    return Task.FromResult(NServiceBus.Transport.ErrorHandleResult.Handled);
                });


            await dispatcher.SendTestMessage(queueName, $"some failing message");

            var onErrorWasCalled = await onErrorCalled.Task;

            Assert.True(onErrorWasCalled);
        }
    }
}