namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Auditing;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NUnit.Framework;
    using Raven.Client;
    using SagaAudit;

    [TestFixture]
    class ApplyRetentionTests : PersistenceTestFixture
    {
        public override Task Setup()
        {
            SetSettings = s => s.AuditRetentionPeriod = IngestionRetentionPeriod;
            return base.Setup();
        }

        [Test]
        public async Task Sets_the_expiry_of_stored_messages_to_processed_time_plus_the_new_retention_period()
        {
            var message = new ProcessedMessage(
                new Dictionary<string, string>
                {
                    { Headers.MessageId, "MyMessageId" },
                    { Headers.ProcessingEndpoint, "SomeEndpoint" },
                    { Headers.ProcessingEnded, DateTimeOffsetHelper.ToWireFormattedString(new DateTimeOffset(ProcessedAt)) }
                },
                new Dictionary<string, object> { { "MessageId", "MyMessageId" } });
            var snapshot = new SagaSnapshot { SagaId = Guid.NewGuid(), ProcessedAt = ProcessedAt };

            await using (var unitOfWork = await StartAuditUnitOfWork(2))
            {
                await unitOfWork.RecordProcessedMessage(message);
                await unitOfWork.RecordSagaSnapshot(snapshot);
                await unitOfWork.Complete();
            }
            await configuration.CompleteDBOperation();

            var expiresBefore = await GetExpires<ProcessedMessage>(message.Id);
            Assert.That(expiresBefore, Is.EqualTo(DateTime.UtcNow + IngestionRetentionPeriod).Within(TimeSpan.FromMinutes(5)), "ingestion sets the expiry from the configured retention period");

            var progress = new List<long>();
            var updated = await ServiceProvider.GetRequiredService<IAuditRetentionApplier>()
                .ApplyRetention(NewRetentionPeriod, new SynchronousProgress(progress), TestTimeoutCancellationToken);

            Assert.That(updated, Is.EqualTo(2));
            Assert.That(await GetExpires<ProcessedMessage>(message.Id), Is.EqualTo(ProcessedAt + NewRetentionPeriod));
            Assert.That(await GetExpires<SagaSnapshot>(snapshot.Id), Is.EqualTo(ProcessedAt + NewRetentionPeriod));
        }

        async Task<DateTime> GetExpires<T>(string id)
        {
            using var session = configuration.DocumentStore.OpenAsyncSession();
            var document = await session.LoadAsync<T>(id);
            Assert.That(document, Is.Not.Null, $"document {id} not found");
            var expires = session.Advanced.GetMetadataFor(document).GetString(Constants.Documents.Metadata.Expires);
            return DateTime.Parse(expires, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
        }

        // Progress<T> posts to the thread pool; this one reports inline so the test can read the list afterwards
        sealed class SynchronousProgress(List<long> reports) : IProgress<long>
        {
            public void Report(long value) => reports.Add(value);
        }

        static readonly DateTime ProcessedAt = new(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        static readonly TimeSpan IngestionRetentionPeriod = TimeSpan.FromDays(30);
        static readonly TimeSpan NewRetentionPeriod = TimeSpan.FromHours(5);
    }
}
