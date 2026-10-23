namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Auditing;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;
    using ServiceControl.Audit.Persistence.EFCore.Implementation.UnitOfWork;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
    using ServiceControl.Persistence.BodyStorage;

    [TestFixture]
    class AuditIngestionUnitOfWorkBodyTests
    {
        static readonly DateTime CreatedOn = new(2026, 10, 20, 13, 0, 0, DateTimeKind.Utc);
        const int Max = 100;

        RecordingBodyStorage storage;
        ThrowingScopeFactory scopes;
        AuditIngestionUnitOfWork unitOfWork;

        [SetUp]
        public void SetUp()
        {
            storage = new RecordingBodyStorage();
            scopes = new ThrowingScopeFactory();
            var settings = new EFPersisterSettings
            {
                ConnectionString = "unused",
                AuditRetentionPeriod = TimeSpan.FromDays(1),
                BodyStorage = new FileSystemBodyStorageSettings { StoragePath = "unused", MaxBodySizeToStore = Max }
            };
            unitOfWork = new AuditIngestionUnitOfWork(scopes, storage, settings, CreatedOn);
        }

        [TearDown]
        public async Task TearDown() => await unitOfWork.DisposeAsync();

        static ProcessedMessage Message(string contentType)
        {
            var headers = new Dictionary<string, string>
            {
                [Headers.MessageId] = Guid.NewGuid().ToString(),
                [Headers.ProcessingEndpoint] = "Receiver",
                [Headers.ContentType] = contentType
            };

            return new ProcessedMessage(headers, []);
        }

        [Test]
        public async Task Small_text_is_not_written_externally()
        {
            await unitOfWork.RecordProcessedMessage(Message("application/json"), Encoding.UTF8.GetBytes("{}"));

            Assert.That(storage.Written, Is.Empty);
        }

        [Test]
        public async Task An_external_body_is_written_under_the_audit_body_id()
        {
            var message = Message("application/octet-stream");
            byte[] body = [1, 2, 3];

            await unitOfWork.RecordProcessedMessage(message, body);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(storage.Written, Has.Count.EqualTo(1));
                Assert.That(storage.Written[0].BodyId, Is.EqualTo(message.Id));
                Assert.That(storage.Written[0].BodyId, Does.StartWith("2026102013-"));
                Assert.That(storage.Written[0].Body, Is.EqualTo(body));
                Assert.That(storage.Written[0].ContentType, Is.EqualTo("application/octet-stream"));
            }
        }

        [Test]
        public async Task An_oversized_text_body_is_written_in_full()
        {
            var body = Encoding.UTF8.GetBytes(new string('a', Max * 3));

            await unitOfWork.RecordProcessedMessage(Message("text/plain"), body);

            Assert.That(storage.Written[0].Body, Is.EqualTo(body));
        }

        [Test]
        public async Task A_failed_body_write_stops_the_batch_before_any_row_is_inserted()
        {
            storage.WriteFailure = new InvalidOperationException("storage is down");
            await unitOfWork.RecordProcessedMessage(Message("application/octet-stream"), new byte[] { 1 });

            var exception = Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.Complete());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception.Message, Is.EqualTo("storage is down"));
                Assert.That(scopes.Used, Is.False, "the database must not be touched once a body write failed");
            }
        }

        [Test]
        public async Task Disposing_without_completing_waits_for_the_started_body_writes()
        {
            storage.PendingWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await unitOfWork.RecordProcessedMessage(Message("application/octet-stream"), new byte[] { 1 });

            var dispose = unitOfWork.DisposeAsync().AsTask();
            Assert.That(dispose.IsCompleted, Is.False);

            storage.PendingWrite.SetResult();
            await dispose;
        }

        [Test]
        public async Task Disposing_does_not_throw_when_a_started_body_write_failed()
        {
            storage.PendingWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await unitOfWork.RecordProcessedMessage(Message("application/octet-stream"), new byte[] { 1 });

            var dispose = unitOfWork.DisposeAsync().AsTask();
            storage.PendingWrite.SetException(new InvalidOperationException("storage is down"));

            Assert.DoesNotThrowAsync(async () => await dispose);
        }

        sealed class ThrowingScopeFactory : IServiceScopeFactory
        {
            public bool Used { get; private set; }

            public IServiceScope CreateScope()
            {
                Used = true;
                throw new InvalidOperationException("The database was used.");
            }
        }
    }
}
