namespace ServiceBus.Management.AcceptanceTests.Audit
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.MessageMutator;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;

    class When_an_audit_message_has_ttbr_set : AcceptanceTest
    {
        [Test]
        public async Task Ttbr_is_stripped_before_being_forwarded_to_audit_queue()
        {
            SetSettings = settings =>
            {
                settings.ForwardAuditMessages = true;
                settings.AuditLogQueue = "Audit.LogPeekEndpoint";
            };

            var context = await Define<MyContext>()
                .WithEndpoint<SourceEndpoint>(
                    c => c.When(bus => bus.SendLocal(new MessageWithTtbr()))
                )
                .WithEndpoint<LogPeekEndpoint>()
                .Done(c => c.Done)
                .Run();

            Assert.IsTrue(context.Done, "Audited message never made it to Audit Log");
            Assert.IsTrue(context.TtbrStripped, "TTBR still set");
        }

        public class SourceEndpoint : EndpointConfigurationBuilder
        {
            public SourceEndpoint()
            {
                // Must disable transactions to be able to forward TTBR to Audit Log
                EndpointSetup<DefaultServerWithAudit>(config => config.ConfigureTransport().Transactions(TransportTransactionMode.None));
            }

            public class MessageHandler : IHandleMessages<MessageWithTtbr>
            {
                public Task Handle(MessageWithTtbr message, IMessageHandlerContext context)
                {
                    Console.WriteLine("Message processed successfully");
                    // Process successfully and forward to Audit Log
                    return Task.FromResult(0);
                }
            }
        }

        public class LogPeekEndpoint : EndpointConfigurationBuilder
        {
            public LogPeekEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.RegisterComponents(components => components.ConfigureComponent<MutateIncomingTransportMessages>(DependencyLifecycle.InstancePerCall));
                }).CustomEndpointName("Audit.LogPeekEndpoint");
            }

            public class MutateIncomingTransportMessages : IMutateIncomingTransportMessages
            {
                readonly MyContext testContext;

                public MutateIncomingTransportMessages(MyContext testContext)
                {
                    this.testContext = testContext;
                }

                public Task MutateIncoming(MutateIncomingTransportMessageContext context)
                {
                    // MSMQ gives incoming messages a magic value so we can't compare against MaxValue
                    // Ensure that the TTBR given is greater than the 10:00:00 configured
                    testContext.TtbrStripped = TimeSpan.Parse(context.Headers[Headers.TimeToBeReceived]) > TimeSpan.Parse("00:10:00");

                    testContext.Done = true;
                    return Task.FromResult(0);
                }
            }
        }

        [TimeToBeReceived("00:10:00")]
        public class MessageWithTtbr : ICommand
        {

        }

        public class MyContext : ScenarioContext
        {
            public bool Done { get; set; }
            public bool TtbrStripped { get; set; }
        }
    }
}