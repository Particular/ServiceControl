namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Audit.Auditing;
    using Audit.Auditing.MessagesView;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    [RunOnAllTransports]
    class When_a_message_fails_to_import : AcceptanceTest
    {
        [Test]
        public async Task It_can_be_reimported()
        {
            //Make sure the audit import attempt fails
            CustomConfiguration = config => { config.RegisterComponents(c => c.ConfigureComponent<FailOnceEnricher>(DependencyLifecycle.SingleInstance)); };

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

            Assert.IsTrue(runResult.AuditForwarded);
        }

        class FailOnceEnricher : IEnrichImportedAuditMessages
        {
            public MyContext Context { get; set; }

            public void Enrich(AuditEnricherContext context)
            {
                if (!Context.FailedImport)
                {
                    TestContext.WriteLine("Simulating message processing failure");
                    throw new MessageDeserializationException("ID", null);
                }

                TestContext.WriteLine("Message processed correctly");
            }
        }

        public class AuditLogSpy : EndpointConfigurationBuilder
        {
            public AuditLogSpy()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.AuditForwarded = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.MessageId = context.MessageId;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public bool FailedImport { get; set; }
            public bool WasImportedAgain { get; set; }
            public string MessageId { get; set; }
            public bool AuditForwarded { get; set; }
        }
    }
}