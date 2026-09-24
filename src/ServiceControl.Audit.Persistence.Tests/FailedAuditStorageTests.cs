namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.Audit.Auditing;

    [TestFixture]
    class FailedAuditStorageTests : PersistenceTestFixture
    {
        [Test]
        public async Task Should_store_failures()
        {
            await FailedAuditStorage.SaveFailedAuditImport(new FailedAuditImport());

            await configuration.CompleteDBOperation();

            var numFailures = await FailedAuditStorage.GetFailedAuditsCount();

            Assert.That(numFailures, Is.EqualTo(1));
        }

        [Test]
        public async Task Repeated_failure_of_the_same_message_stores_one_document()
        {
            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, "message-1" },
                { Headers.ProcessingEndpoint, "Sales" }
            };
            await FailedAuditStorage.SaveFailedAuditImport(new FailedAuditImport { Id = FailedAuditImport.DeriveKey(headers, "native-1").ToString() });
            await FailedAuditStorage.SaveFailedAuditImport(new FailedAuditImport { Id = FailedAuditImport.DeriveKey(headers, "native-1").ToString() });
            await configuration.CompleteDBOperation();

            Assert.That(await FailedAuditStorage.GetFailedAuditsCount(), Is.EqualTo(1));
        }

        [Test]
        public void DeriveKey_uses_retry_id_or_falls_back_for_malformed_headers()
        {
            var retryId = Guid.NewGuid();
            var headers = new Dictionary<string, string> { ["ServiceControl.Retry.UniqueMessageId"] = retryId.ToString() };

            var fallback = FailedAuditImport.DeriveKey(new Dictionary<string, string>(), "native-1");
            Assert.That(FailedAuditImport.DeriveKey(headers, "native-1"), Is.EqualTo(retryId));
            Assert.That(FailedAuditImport.DeriveKey(new Dictionary<string, string>(), "native-1"), Is.EqualTo(fallback));
            Assert.That(FailedAuditImport.DeriveKey(new Dictionary<string, string>(), "native-2"), Is.Not.EqualTo(fallback));
            headers["ServiceControl.Retry.UniqueMessageId"] = "not-a-guid";
            Assert.That(FailedAuditImport.DeriveKey(headers, "native-1"), Is.EqualTo(fallback));
        }

        [Test]
        public async Task Should_be_able_to_process_failures()
        {
            await FailedAuditStorage.SaveFailedAuditImport(new FailedAuditImport());
            await FailedAuditStorage.SaveFailedAuditImport(new FailedAuditImport());

            await configuration.CompleteDBOperation();

            var succeeded = 0;
            await FailedAuditStorage.ProcessFailedMessages(async (transportMessage, markComplete, token) =>
            {
                await markComplete(token);
                succeeded++;
            }, CancellationToken.None);

            await configuration.CompleteDBOperation();

            var numFailures = await FailedAuditStorage.GetFailedAuditsCount();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(succeeded, Is.EqualTo(2));
                Assert.That(numFailures, Is.EqualTo(0));
            }
        }

        [Test]
        public async Task Processing_throws_when_the_token_is_already_cancelled()
        {
            await FailedAuditStorage.SaveFailedAuditImport(new FailedAuditImport());

            await configuration.CompleteDBOperation();

            var processed = 0;

            Assert.That(
                async () => await FailedAuditStorage.ProcessFailedMessages(
                    async (transportMessage, markComplete, token) =>
                    {
                        processed++;
                        await markComplete(token);
                    },
                    new CancellationToken(true)),
                Throws.InstanceOf<OperationCanceledException>());

            await configuration.CompleteDBOperation();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(processed, Is.EqualTo(0));
                Assert.That(await FailedAuditStorage.GetFailedAuditsCount(), Is.EqualTo(1), "the pending import must survive");
            }
        }

        [Test]
        public async Task Processing_stops_at_the_message_that_cancels()
        {
            await FailedAuditStorage.SaveFailedAuditImport(new FailedAuditImport());
            await FailedAuditStorage.SaveFailedAuditImport(new FailedAuditImport());
            await FailedAuditStorage.SaveFailedAuditImport(new FailedAuditImport());

            await configuration.CompleteDBOperation();

            using var source = new CancellationTokenSource();
            var processed = 0;

            Assert.That(
                async () => await FailedAuditStorage.ProcessFailedMessages(
                    async (transportMessage, markComplete, token) =>
                    {
                        processed++;
                        await markComplete(token);
                        await source.CancelAsync();
                    },
                    source.Token),
                Throws.InstanceOf<OperationCanceledException>(),
                "an interrupted replay must not return as though it had completed");

            await configuration.CompleteDBOperation();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(processed, Is.EqualTo(1));
                Assert.That(await FailedAuditStorage.GetFailedAuditsCount(), Is.EqualTo(3), "an interrupted replay must not commit its deletes");
            }
        }
    }
}