namespace ServiceControl.MultiInstance.AcceptanceTests.Infrastructure
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using CompositeViews.Messages;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using TestSupport;
    using TestSupport.EndpointTemplates;

    class When_remote_instance_is_not_reachable : AcceptanceTest
    {
        [Test]
        public async Task Should_not_fail()
        {
            CustomServiceControlPrimarySettings = s =>
            {
                var currentSetting = s.RemoteInstances[0];
                s.RemoteInstances = new[]
                {
                    currentSetting, new RemoteInstanceSetting("http://localhost:12121")
                };
            };

            //search for the message type
            var searchString = nameof(MyMessage);

            await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.SendLocal(new MyMessage())))
                .Done(async c => await this.TryGetMany<MessagesView>("/api/messages/search/" + searchString, instanceName: ServiceControlInstanceName))
                .Run();
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() => EndpointSetup<DefaultServerWithAudit>();

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly MyContext testContext;
                readonly IReadOnlySettings settings;

                public MyMessageHandler(MyContext testContext, IReadOnlySettings settings)
                {
                    this.testContext = testContext;
                    this.settings = settings;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();
                    testContext.MessageId = context.MessageId;
                    return Task.CompletedTask;
                }
            }
        }


        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}