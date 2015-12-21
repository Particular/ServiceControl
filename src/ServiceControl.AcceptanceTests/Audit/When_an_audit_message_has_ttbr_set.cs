namespace ServiceBus.Management.AcceptanceTests.Audit
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
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
                .WithEndpoint<LogPeekEndpoint>()
                .Done(c => c.Done)
                .Run();

            Assert.IsTrue(context.Done, "Audited message never made it to Audit Log");
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

            public class MessageHandler : IHandleMessages<MessageWithTtbr>
            {
                readonly MyContext context;
                IBus Bus { get; set; }

                public MessageHandler(MyContext context)
                {
                    this.context = context;
                }

                public void Handle(MessageWithTtbr message)
                {
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
        }
    }
}
