namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Auditing;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Monitoring;

    [TestFixture]
    class PagedVersionConformanceTests : PersistenceTestFixture
    {
        [Test]
        public async Task Two_pages_of_one_set_do_not_share_a_version()
        {
            await Ingest(MakeMessage(), MakeMessage(), MakeMessage());

            var firstPage = await DataStore.GetMessages(false, new PagingInfo(page: 1, pageSize: 2), Sort);
            var firstPageAgain = await DataStore.GetMessages(false, new PagingInfo(page: 1, pageSize: 2), Sort);
            var secondPage = await DataStore.GetMessages(false, new PagingInfo(page: 2, pageSize: 2), Sort);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(firstPage.Results, Has.Count.EqualTo(2), "two rows on the first page");
                Assert.That(secondPage.Results, Has.Count.EqualTo(1), "and the third on the second, so the bodies differ");
                Assert.That(firstPage.QueryStats.ETag, Is.Not.Empty, "the first page produced no version to compare");
                Assert.That(firstPageAgain.QueryStats.ETag, Is.EqualTo(firstPage.QueryStats.ETag),
                    "the first page's version moved between two reads of unchanged data, so this test cannot judge anything");
                Assert.That(secondPage.QueryStats.ETag, Is.Not.EqualTo(firstPage.QueryStats.ETag),
                    "a caller holding page one would be told page two is unchanged and would render page one twice");
            }
        }

        [Test]
        public async Task Two_searches_do_not_share_a_version()
        {
            var wanted = Guid.NewGuid().ToString();

            await Ingest(MakeMessage(conversationId: wanted), MakeMessage(conversationId: wanted), MakeMessage());

            var matching = await DataStore.QueryMessages(wanted, new PagingInfo(), Sort);
            var matchingAgain = await DataStore.QueryMessages(wanted, new PagingInfo(), Sort);
            var missing = await DataStore.QueryMessages(Guid.NewGuid().ToString(), new PagingInfo(), Sort);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(matching.Results, Is.Not.Empty, "the search that should match found nothing, so the test proves nothing");
                Assert.That(missing.Results, Is.Empty, "and the search for an unused id matched nothing, so the bodies differ");
                Assert.That(matching.QueryStats.ETag, Is.Not.Empty, "the matching search produced no version to compare");
                Assert.That(matchingAgain.QueryStats.ETag, Is.EqualTo(matching.QueryStats.ETag),
                    "the matching search's version moved between two reads of unchanged data");
                Assert.That(missing.QueryStats.ETag, Is.Not.EqualTo(matching.QueryStats.ETag),
                    "a caller holding an empty search result would be told a search carrying messages is unchanged");
            }
        }

        [Test]
        public async Task Two_endpoints_do_not_share_a_version()
        {
            await Ingest(MakeMessage(processingEndpoint: "Shipping"), MakeMessage(processingEndpoint: "Billing"));

            var shipping = await DataStore.QueryMessagesByReceivingEndpoint(false, "Shipping", new PagingInfo(), Sort);
            var shippingAgain = await DataStore.QueryMessagesByReceivingEndpoint(false, "Shipping", new PagingInfo(), Sort);
            var billing = await DataStore.QueryMessagesByReceivingEndpoint(false, "Billing", new PagingInfo(), Sort);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(shipping.Results, Has.Count.EqualTo(1), "one message for Shipping");
                Assert.That(billing.Results, Has.Count.EqualTo(1), "one for Billing");
                Assert.That(billing.Results[0].MessageId, Is.Not.EqualTo(shipping.Results[0].MessageId), "and they are not the same message");
                Assert.That(shipping.QueryStats.ETag, Is.Not.Empty, "Shipping produced no version to compare");
                Assert.That(shippingAgain.QueryStats.ETag, Is.EqualTo(shipping.QueryStats.ETag),
                    "Shipping's version moved between two reads of unchanged data");
                Assert.That(billing.QueryStats.ETag, Is.Not.EqualTo(shipping.QueryStats.ETag),
                    "a caller watching one endpoint's messages would be shown another endpoint's");
            }
        }

        static SortInfo Sort => new("Id", "asc");

        async Task Ingest(params ProcessedMessage[] messages)
        {
            var unitOfWork = await StartAuditUnitOfWork(messages.Length);

            foreach (var message in messages)
            {
                await unitOfWork.RecordProcessedMessage(message);
            }

            await unitOfWork.DisposeAsync();
            await configuration.CompleteDBOperation();
        }

        static ProcessedMessage MakeMessage(string conversationId = null, string processingEndpoint = null)
        {
            var messageId = Guid.NewGuid().ToString();
            conversationId ??= Guid.NewGuid().ToString();
            processingEndpoint ??= "SomeEndpoint";

            var metadata = new Dictionary<string, object>
            {
                { "MessageId", messageId },
                { "MessageIntent", MessageIntent.Send },
                { "CriticalTime", TimeSpan.FromSeconds(5) },
                { "ProcessingTime", TimeSpan.FromSeconds(1) },
                { "DeliveryTime", TimeSpan.FromSeconds(4) },
                { "IsSystemMessage", false },
                { "MessageType", "MyMessageType" },
                { "IsRetried", false },
                { "ConversationId", conversationId },
                { "ReceivingEndpoint", new EndpointDetails { Name = processingEndpoint } }
            };

            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, messageId },
                { Headers.ProcessingEndpoint, processingEndpoint },
                { Headers.MessageIntent, MessageIntent.Send.ToString() },
                { Headers.ConversationId, conversationId },
                { Headers.ProcessingStarted, DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow) },
                { Headers.EnclosedMessageTypes, "MyMessageType" }
            };

            return new ProcessedMessage(headers, metadata);
        }
    }
}
