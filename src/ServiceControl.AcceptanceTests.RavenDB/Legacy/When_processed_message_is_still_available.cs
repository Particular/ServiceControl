namespace ServiceControl.AcceptanceTests.RavenDB.Legacy
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTests;
    using CompositeViews.Messages;
    using MessageAuditing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Operations.BodyStorage;
    using Raven.Client;
    using ServiceControl.Persistence;
    using TestSupport.EndpointTemplates;

    //TODO: this is ravendb 3.5 specific test and should be moved to raven specific assembly
    class When_processed_message_is_still_available : AcceptanceTest
    {
        [Test]
        public async Task Should_be_accessible_via_the_rest_api()
        {
            var messageId = Guid.NewGuid().ToString();

            CustomizeHostBuilder = hostBuilder
                => hostBuilder.ConfigureServices((hostBuilderContext, services)
                    => services.AddHostedService(sp => new CreateMessageDataMigration(messageId, sp.GetRequiredService<IDocumentStore>())));

            MessagesView auditedMessage = null;
            byte[] body = null;

            var context = await Define<MyContext>()
                .WithEndpoint<Endpoint>()
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

        class CreateMessageDataMigration : IHostedService
        {
            readonly string messageId;
            readonly IDocumentStore store;

            public CreateMessageDataMigration(string messageId, IDocumentStore store)
            {
                this.messageId = messageId;
                this.store = store;
            }

            public async Task StartAsync(CancellationToken cancellationToken)
            {
                using (var documentSession = store.OpenAsyncSession())
                {
                    var processedMessage = new ProcessedMessage
                    {
                        MessageMetadata = new Dictionary<string, object>
                        {
                            {"Body", "Some Content"},
                            {"ContentLength", 11},
                            {"BodyUrl", string.Format(BodyStorageEnricher.BodyUrlFormatString, messageId)},
                            {"BodyNotStored", false},
                            {"ContentType", "text/plain" },
                            {"MessageIntent", (int)MessageIntentEnum.Send},
                            {"MessageId", messageId},
                        },
                        UniqueMessageId = messageId,
                        ProcessedAt = DateTime.UtcNow
                    };

                    await documentSession.StoreAsync(processedMessage, cancellationToken);
                    await documentSession.SaveChangesAsync(cancellationToken);
                }

                SpinWait.SpinUntil(() => store.DatabaseCommands.GetStatistics().StaleIndexes.Length == 0, TimeSpan.FromSeconds(10));
            }

            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        public class MyContext : ScenarioContext
        {
        }
    }
}