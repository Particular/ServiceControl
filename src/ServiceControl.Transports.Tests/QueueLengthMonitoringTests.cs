namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using NUnit.Framework.Internal;
    using ServiceControl.Transports;

    [TestFixture]
    class QueueLengthMonitoringTests : TransportTestFixture
    {
        [Test]
        public async Task Should_report_queue_length()
        {
            var queueName = GetTestQueueName("queuelenght");

            await CreateTestQueue(queueName);

            var onQueueLengthEntryReceived = CreateTaskCompletionSource<QueueLengthEntry>();

            await StartQueueLengthProvider(queueName, (qle) =>
            {
                if (qle.Value > 0)
                {
                    onQueueLengthEntryReceived.TrySetResult(qle);
                }
            });

            var dispatcher = await CreateDispatcher(queueName);

            await dispatcher.SendTestMessage(queueName, "some content");

            var queueLengthEntry = await onQueueLengthEntryReceived.Task;

            Assert.AreEqual(1, queueLengthEntry.Value);
        }

        [Test]
        public async Task Should_return_newest_queue_properties_if_queue_name_is_the_same()
        {
            var now = DateTimeOffset.UtcNow;
            var future = now.AddHours(1);

            var runtimeNewerQueueProperties = new QueueRuntimeProperties(name: "SameQueueName", createdAt: future, updatedAt: now, accessedAt: now);
            var existingOlderQueueProperties = new QueueRuntimeProperties(name: "SameQueueName", createdAt: now, updatedAt: now, accessedAt: now);

            var compareResult = await CompareQueueDates(existingOlderQueueProperties, runtimeNewerQueueProperties);  // check newer CreatedAt value passed with the right parameter
            Assert.AreEqual(future, compareResult.CreatedAt);
            Assert.AreEqual(now, compareResult.UpdatedAt);
            Assert.AreEqual(now, compareResult.AccessedAt);
            compareResult = await CompareQueueDates(runtimeNewerQueueProperties, existingOlderQueueProperties);  // check newer CreatedAt value passed with the left parameter
            Assert.AreEqual(future, compareResult.CreatedAt);
            Assert.AreEqual(now, compareResult.UpdatedAt);
            Assert.AreEqual(now, compareResult.AccessedAt);

            runtimeNewerQueueProperties = new QueueRuntimeProperties(name: "SameQueueName", createdAt: now, updatedAt: future, accessedAt: now);
            compareResult = await CompareQueueDates(existingOlderQueueProperties, runtimeNewerQueueProperties);  // check newer UpdatedAt value passed with the right parameter
            Assert.AreEqual(now, compareResult.CreatedAt);
            Assert.AreEqual(future, compareResult.UpdatedAt);
            Assert.AreEqual(now, compareResult.AccessedAt);
            compareResult = await CompareQueueDates(runtimeNewerQueueProperties, existingOlderQueueProperties);  // check newer UpdatedAt value passed with the left parameter
            Assert.AreEqual(now, compareResult.CreatedAt);
            Assert.AreEqual(future, compareResult.UpdatedAt);
            Assert.AreEqual(now, compareResult.AccessedAt);

            runtimeNewerQueueProperties = new QueueRuntimeProperties(name: "SameQueueName", createdAt: now, updatedAt: now, accessedAt: future);
            compareResult = await CompareQueueDates(existingOlderQueueProperties, runtimeNewerQueueProperties);  // check newer AccessedAt value passed with the right parameter
            Assert.AreEqual(now, compareResult.CreatedAt);
            Assert.AreEqual(now, compareResult.UpdatedAt);
            Assert.AreEqual(future, compareResult.AccessedAt);
            compareResult = await CompareQueueDates(runtimeNewerQueueProperties, existingOlderQueueProperties);  // check newer AccessedAt value passed with the left parameter
            Assert.AreEqual(now, compareResult.CreatedAt);
            Assert.AreEqual(now, compareResult.UpdatedAt);
            Assert.AreEqual(future, compareResult.AccessedAt);

            runtimeNewerQueueProperties = new QueueRuntimeProperties(name: "SameQueueName", createdAt: now, updatedAt: now, accessedAt: now);
            compareResult = await CompareQueueDates(existingOlderQueueProperties, runtimeNewerQueueProperties);  // check if all dates are equal
            Assert.AreEqual(now, compareResult.CreatedAt);
            Assert.AreEqual(now, compareResult.UpdatedAt);
            Assert.AreEqual(now, compareResult.AccessedAt);
        }

        public class QueueRuntimeProperties
        {
            public string Name { get; set; }
            public long ActiveMessageCount { get; set; }
            public long ScheduledMessageCount { get; set; }
            public long DeadLetterMessageCount { get; set; }
            public long TransferDeadLetterMessageCount { get; set; }
            public long TransferMessageCount { get; set; }
            public long TotalMessageCount { get; set; }
            public long SizeInBytes { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
            public DateTimeOffset UpdatedAt { get; set; }
            public DateTimeOffset AccessedAt { get; set; }

            public QueueRuntimeProperties(
                string name,
                long activeMessageCount = 0,
                long scheduledMessageCount = 0,
                long deadLetterMessageCount = 0,
                long transferDeadLetterMessageCount = 0,
                long transferMessageCount = 0,
                long totalMessageCount = 0,
                long sizeInBytes = 0,
                DateTimeOffset createdAt = default,
                DateTimeOffset updatedAt = default,
                DateTimeOffset accessedAt = default)
            {
                Name = name;
                ActiveMessageCount = activeMessageCount;
                ScheduledMessageCount = scheduledMessageCount;
                DeadLetterMessageCount = deadLetterMessageCount;
                TransferDeadLetterMessageCount = transferDeadLetterMessageCount;
                ActiveMessageCount = transferMessageCount;
                ActiveMessageCount = totalMessageCount;
                ActiveMessageCount = sizeInBytes;
                CreatedAt = createdAt;
                UpdatedAt = updatedAt;
                AccessedAt = accessedAt;
            }
        }

        async Task<QueueRuntimeProperties> CompareQueueDates(QueueRuntimeProperties existingItem, QueueRuntimeProperties queueRuntimeProperty)
        {
            var createdAtCompare = (TimeComparison)DateTimeOffset.Compare(existingItem.CreatedAt, queueRuntimeProperty.CreatedAt);
            var updatedAtCompare = (TimeComparison)DateTimeOffset.Compare(existingItem.UpdatedAt, queueRuntimeProperty.UpdatedAt);
            var accessedAtCompare = (TimeComparison)DateTimeOffset.Compare(existingItem.AccessedAt, queueRuntimeProperty.AccessedAt);

            if (createdAtCompare == TimeComparison.Earlier)
            {
                Console.WriteLine("Queue <{0}> already processed with an older 'createdAt' date: {1}.  Queue is being updated with newer properties that has the 'createdAt' date: {2}", existingItem.Name, existingItem.CreatedAt, queueRuntimeProperty.CreatedAt);
                return await Task.FromResult(queueRuntimeProperty).ConfigureAwait(false);
            }
            else if (createdAtCompare == TimeComparison.Later)
            {
                Console.WriteLine("Queue <{0}> already processed with a newer 'createdAt' date: {1}. The duplicate queue with the 'createdAt' date: {2} is being discarded.", existingItem.Name, existingItem.CreatedAt, queueRuntimeProperty.CreatedAt);
            }
            else if (updatedAtCompare == TimeComparison.Earlier)
            {
                Console.WriteLine("Queue <{0}> already processed with an older 'updatedAt' date: {1}. Queue is being updated with newer properties that has the 'updatedAt' date: {2}", existingItem.Name, existingItem.UpdatedAt, queueRuntimeProperty.UpdatedAt);
                return await Task.FromResult(queueRuntimeProperty).ConfigureAwait(false);
            }
            else if (updatedAtCompare == TimeComparison.Later)
            {
                Console.WriteLine("Queue <{0}> already processed with a newer 'updatedAt' date: {1}. The duplicate queue with the 'updatedAt' date: {2} is being discarded.", existingItem.Name, existingItem.UpdatedAt, queueRuntimeProperty.UpdatedAt);
            }
            else if (accessedAtCompare == TimeComparison.Earlier)
            {
                Console.WriteLine("Queue <{0}> already processed with an older 'accessedAt' date: {1}. Queue is being updated with newer properties that has the 'accessedAt' date: {2}", existingItem.Name, existingItem.AccessedAt, queueRuntimeProperty.AccessedAt);
                return await Task.FromResult(queueRuntimeProperty).ConfigureAwait(false);
            }
            else if (accessedAtCompare == TimeComparison.Later)
            {
                Console.WriteLine("Queue <{0}> already processed with a newer 'accessedAt' date: {1}. The duplicate queue with the 'accessedAt' date: {2} is being discarded.", existingItem.Name, existingItem.AccessedAt, queueRuntimeProperty.AccessedAt);
            }
            else
            {
                Console.WriteLine("Queue <{0}> already processed. The duplicate queue is being discarded.", existingItem.Name);
            }

            return await Task.FromResult(existingItem).ConfigureAwait(false);
        }

        enum TimeComparison
        {
            Earlier = -1,
            Same = 0,
            Later = 1
        }
    }
}