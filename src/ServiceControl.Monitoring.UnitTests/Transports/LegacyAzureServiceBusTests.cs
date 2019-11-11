namespace ServiceControl.Monitoring.UnitTests.Transports
{
    using NUnit.Framework;
    using ServiceControl.Transports.LegacyAzureServiceBus;

    [TestFixture]
    class LegacyAzureServiceBusTests
    {
        const string ConnectionString = "Endpoint=sb://unit-test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=N3d5f3bqQzZBKx";

        [Test]
        public void QueueLengthQueryIntervalIsRemovedFromTheConnectionString()
        {
            var testCases = new[]
            {
                ConnectionString + ";QueueLengthQueryDelayInterval=5000",
                ConnectionString + ";;QueueLengthQueryDelayInterval=50",
                ConnectionString + ";;queueLengthQueryDelayInterval=50",
                "QueueLengthQueryDelayInterval=5000;" + ConnectionString,
                "Endpoint=sb://unit-test.servicebus.windows.net/" + ";QueueLengthQueryDelayInterval=5000" + ";SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=N3d5f3bqQzZBKx"
            };

            foreach (var testCase in testCases)
            {
                var connectionStringWithoutQlPart = ConnectionStringPartRemover.Remove(testCase, "QueueLengthQueryDelayInterval");

                Assert.AreEqual(ConnectionString, connectionStringWithoutQlPart);
            }
        }
    }
}
