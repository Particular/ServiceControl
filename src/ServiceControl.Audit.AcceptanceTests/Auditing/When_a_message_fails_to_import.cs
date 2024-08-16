namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Audit.Auditing;
    using Audit.Auditing.MessagesView;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NUnit.Framework;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;


    class When_a_message_fails_to_import : AcceptanceTest
    {
        [Test]
        public async Task It_can_be_reimported()
        {
            CustomizeHostBuilder = hostBuilder =>
                //Make sure the audit import attempt fails
                hostBuilder.Services.AddSingleton<IEnrichImportedAuditMessages, FailOnceEnricher>();

            SetSettings = settings =>
            {
                settings.ForwardAuditMessages = true;
                settings.AuditLogQueue = Conventions.EndpointNamingConvention(typeof(AuditLogSpy));
            };

            var runResult = await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage())))
                .WithEndpoint<Receiver>()
                .WithEndpoint<AuditLogSpy>()
                .Done(async c =>
                {
                    if (c.MessageId == null)
                    {
                        return false;
                    }

                    if (!c.WasImportedAgain)
                    {
                        var result = await this.TryGet<FailedAuditsCountReponse>("/api/failedaudits/count");
                        FailedAuditsCountReponse failedAuditCountsResponse = result;
                        if (result && failedAuditCountsResponse.Count > 0)
                        {
                            c.FailedImport = true;
                            await this.Post<object>("/api/failedaudits/import");
                            c.WasImportedAgain = true;
                        }

                        return false;
                    }

                    return await this.TryGetMany<MessagesView>($"/api/messages/search/{c.MessageId}") && c.AuditForwarded;
                })
                .Run();

            Assert.That(runResult.AuditForwarded, Is.True);
        }

        class FailOnceEnricher(MyContext testContext) : IEnrichImportedAuditMessages
        {
            public void Enrich(AuditEnricherContext context)
            {
                if (!testContext.FailedImport)
                {
                    TestContext.WriteLine("Simulating message processing failure");
                    throw new MessageDeserializationException("ID", null);
                }

                TestContext.WriteLine("Message processed correctly");
            }
        }

        public class AuditLogSpy : EndpointConfigurationBuilder
        {
            public AuditLogSpy() => EndpointSetup<DefaultServerWithoutAudit>();

            public class MyMessageHandler(MyContext testContext) : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.AuditForwarded = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServerWithAudit>();

            public class MyMessageHandler(MyContext testContext) : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.MessageId = context.MessageId;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyMessage : ICommand;

        public class MyContext : ScenarioContext
        {
            public bool FailedImport { get; set; }
            public bool WasImportedAgain { get; set; }
            public string MessageId { get; set; }
            public bool AuditForwarded { get; set; }
        }
    }
}