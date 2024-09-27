namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Transport;
    using NUnit.Framework;

    // We have to ensure this test runs first because this test initializes the dispatcher, and the dispatchers initialize the logging framework as static fields :(
    // The logging cannot be reinitialized, so subsequent test that utilize the Scenario.Define.Run will use a different log factory that depends on more statics :(
    // The end result is a null reference exception :(
    [TestFixture]
    [Order(1)]
    class QueueIngestionTests : TransportTestFixture
    {
        [Test]
        public async Task Should_ingest_all_messages()
        {
            var queueName = GetTestQueueName("ingestion");

            await CreateTestQueue(queueName);

            var numMessagesToIngest = 10;
            var onMessagesProcessed = CreateTaskCompletionSource<bool>();

            var numMessagesIngested = 0;

            await StartQueueIngestor(
                 queueName,
                 (_, __) =>
                 {
                     numMessagesIngested++;

                     if (numMessagesIngested == numMessagesToIngest)
                     {
                         onMessagesProcessed.SetResult(true);
                     }

                     return Task.CompletedTask;
                 },
                 (_, __) => { Assert.Fail("There should be no errors"); return Task.FromResult(ErrorHandleResult.Handled); });

            for (int i = 0; i < numMessagesToIngest; i++)
            {
                await Dispatcher.SendTestMessage(queueName, $"message{i}", configuration.TransportCustomization);
            }

            var allMessagesProcessed = await onMessagesProcessed.Task;

            Assert.That(allMessagesProcessed, Is.True);
        }

        [Test]
        public async Task Should_trigger_on_error()
        {
            var queueName = GetTestQueueName("failure");

            await CreateTestQueue(queueName);

            var onErrorCalled = CreateTaskCompletionSource<bool>();

            await StartQueueIngestor(
                queueName,
                (_, __) => throw new Exception("Some failure"),
                (_, __) =>
                {
                    onErrorCalled.SetResult(true);
                    return Task.FromResult(ErrorHandleResult.Handled);
                });

            await Dispatcher.SendTestMessage(queueName, $"some failing message", configuration.TransportCustomization);

            var onErrorWasCalled = await onErrorCalled.Task;

            Assert.That(onErrorWasCalled, Is.True);
        }
    }
}