namespace ServiceBus.Management.AcceptanceTests.Audit
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.MessageMutator;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;

    class When_an_audit_message_has_ttbr_set : AcceptanceTest
    {
        [Test]
        public void Ttbr_is_stripped_before_being_forwarded_to_audit_queue()
        {
            var context = new MyContext();
            SetSettings = settings =>
            {
                settings.ForwardAuditMessages = true;
                settings.AuditLogQueue = Address.Parse("Audit.LogPeekEndpoint");
            };

            Define(context)
                .WithEndpoint<SourceEndpoint>(
                    c => c.Given(bus => bus.SendLocal(new MessageWithTtbr()))
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
                EndpointSetup<DefaultServerWithAudit>(config => config.Transactions().Disable());
            }

            public class MessageHandler : IHandleMessages<MessageWithTtbr>
            {
                public void Handle(MessageWithTtbr message)
                {
                    Console.WriteLine("Message processed successfully");
                    // Process successfully and forward to Audit Log
                }
            }
        }

        public class LogPeekEndpoint : EndpointConfigurationBuilder
        {
            public LogPeekEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.EndpointName("Audit.LogPeekEndpoint");
                    c.RegisterComponents(components => components.ConfigureComponent<MutateIncomingTransportMessages>(DependencyLifecycle.InstancePerCall));
                });
            }

            public class MutateIncomingTransportMessages : IMutateIncomingTransportMessages
            {
                readonly MyContext context;

                public MutateIncomingTransportMessages(MyContext context)
                {
                    this.context = context;
                }

                public void MutateIncoming(TransportMessage transportMessage)
                {
                    // MSMQ gives incoming messages a magic value so we can't compare against MaxValue
                    // Ensure that the TTBR given is greater than the 10:00:00 configured
                    context.TtbrStripped = transportMessage.TimeToBeReceived > TimeSpan.Parse("00:10:00");

                    context.Done = true;
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