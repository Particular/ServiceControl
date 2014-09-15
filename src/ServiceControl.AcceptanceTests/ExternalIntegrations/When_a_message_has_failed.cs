namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;

    public class When_an_event_is_enabled_for_external_integrations : AcceptanceTest
    {
        [Test]
        public void Should_be_published_on_the_bus()
        {
            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<FailingReceiver>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .WithEndpoint<ExternalProcessor>()
                .Done(c => c.MessageDelivered)
                .Run(TimeSpan.FromSeconds(20));

            Assert.IsTrue(context.MessageDelivered);
        }


        public class FailingReceiver : EndpointConfigurationBuilder
        {
            public FailingReceiver()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c=>Configure.Features.Disable<SecondLevelRetries>())
                    .AuditTo(Address.Parse("audit"));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.EndpointNameOfReceivingEndpoint = Configure.EndpointName;
                    Context.MessageId = Bus.CurrentMessageContext.Id.Replace(@"\", "-");
                    throw new Exception("Simulated exception");
                }
            }
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => Configure.Features.Disable<SecondLevelRetries>())
                    .AddMapping<ServiceControl.Contracts.Failures.MessageFailed>(typeof(ManagementEndpoint));
            }

            public class FailureHandler : IHandleMessages<ServiceControl.Contracts.Failures.MessageFailed>
            {
                public MyContext Context { get; set; }

                public void Handle(ServiceControl.Contracts.Failures.MessageFailed message)
                {
                    Context.MessageDelivered = true;
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public bool MessageDelivered { get; set; }
            public string MessageId { get; set; }
            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string UniqueMessageId
            {
                get
                {
                    return DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();
                }
            }
        }
    }
}