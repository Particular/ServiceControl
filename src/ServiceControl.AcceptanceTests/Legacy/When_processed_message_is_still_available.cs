namespace ServiceControl.AcceptanceTests.Legacy
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTests;
    using CompositeViews.Messages;
    using Contracts.Operations;
    using MessageAuditing;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Operations.BodyStorage;
    using TestSupport.EndpointTemplates;

    class When_processed_message_is_still_available : AcceptanceTest
    {
        [Test]
        public async Task Should_be_accessible_via_the_rest_api()
        {
            var messageId = Guid.NewGuid().ToString();

            SetSettings = settings =>
            {
                settings.StoreInitializer = async store =>
                {
                    using (var documentSession = store.OpenAsyncSession())
                    {
                        var processedMessage = new ProcessedMessage
                        {
                            MessageMetadata = new Dictionary<string, object>
                            {
                                {"Body", "Some Content"},
                                {"ContentLength", 11},
                                {"BodyUrl", string.Format(BodyStorageFeature.BodyStorageEnricher.BodyUrlFormatString, messageId)},
                                {"BodyNotStored", false},
                                {"ContentType", "text/plain" },
                                {"MessageIntent", (int)MessageIntentEnum.Send},
                                {"MessageId", messageId},
                            },
                            UniqueMessageId = messageId,
                            ProcessedAt = DateTime.UtcNow
                        };

                        await documentSession.StoreAsync(processedMessage);
                        await documentSession.SaveChangesAsync();
                    }

                    SpinWait.SpinUntil(() => store.DatabaseCommands.GetStatistics().StaleIndexes.Length == 0, TimeSpan.FromSeconds(10));
                };
            };

            MessagesView auditedMessage = null;
            byte[] body = null;

            var context = await Define<MyContext>()
                .WithEndpoint<EndpointToPleaseAcceptanceTestFramework>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<MessagesView>("/api/messages?include_system_messages=false&sort=id", m => m.MessageId == messageId);
                    auditedMessage = result;
                    if (!result)
                    {
                        return false;
                    }

                    body = await this.DownloadData(auditedMessage.BodyUrl);

                    return true;
                })
                .Run();

            Assert.AreEqual(messageId, auditedMessage.MessageId);
            Assert.AreEqual(MessageStatus.Successful, auditedMessage.Status);

            var bodyAsString = Encoding.UTF8.GetString(body);

            Assert.AreEqual("Some Content", bodyAsString);
        }

        public class EndpointToPleaseAcceptanceTestFramework : EndpointConfigurationBuilder
        {
            public EndpointToPleaseAcceptanceTestFramework()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        public class MyContext : ScenarioContext
        {
        }
    }
}