namespace ServiceBus.Management.AcceptanceTests.Audit
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.MessageMutator;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceBus.Management.Infrastructure.Settings;

    class When_an_audit_message_has_ttbr_set : AcceptanceTest
    {
        [Test]
        public void Ttbr_is_stripped_before_being_forwarded_to_audit_queue()
        {
            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(
                    c => c.AppConfig(PathToAppConfig)
                          .CustomConfig(config =>
                            {
                                Settings.ForwardAuditMessages = true;
                                Settings.AuditLogQueue = Address.Parse("Audit.LogPeekEndpoint");
                            })
                )
                .WithEndpoint<SourceEndpoint>(
                    // Must disable transactions to be able to forward TTBR to Audit Log
                    c => c.CustomConfig(config => config.Transactions().Disable())
                          .Given(bus => bus.SendLocal(new MessageWithTtbr()))
                )
                .WithEndpoint<LogPeekEndpoint>(
                    c => c.CustomConfig(config => config.RegisterComponents(components => components.ConfigureComponent<LogPeekEndpoint.MutateIncomingTransportMessages>(DependencyLifecycle.InstancePerCall)))
                )
                .Done(c => c.Done)
                .Run();

            Assert.IsTrue(context.Done, "Audited message never made it to Audit Log");
            Assert.IsTrue(context.TtbrStipped, "TTBR still set");
        }

        public class SourceEndpoint : EndpointConfigurationBuilder
        {
            public SourceEndpoint()
            {
                EndpointSetup<DefaultServer>()
                    .AuditTo(Address.Parse("Audit"));
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
                EndpointSetup<DefaultServerWithoutAudit>();
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
                    context.TtbrStipped = true;
                    // MSMQ gives incoming messages a magic value so we can't compare against MaxValue
                    // Ensure that the TTBR given is greater than the 10:00:00 configured
                    context.TtbrStipped = transportMessage.TimeToBeReceived > TimeSpan.Parse("10:00:00");

                    context.Done = true;
                }
            }
        }

        [TimeToBeReceived("10:00:00")]
        public class MessageWithTtbr : ICommand
        {
            
        }

        [Serializable]
        public class MyContext : ScenarioContext
        {
            public bool Done { get; set; }
            public bool TtbrStipped { get; set; }
        }
    }
}
