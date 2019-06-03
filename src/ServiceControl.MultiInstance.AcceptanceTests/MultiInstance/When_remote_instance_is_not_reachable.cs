namespace ServiceBus.Management.AcceptanceTests.MultiInstance
{
    using System.Threading.Tasks;
    using EndpointTemplates;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;

    class When_remote_instance_is_not_reachable : AcceptanceTest
    {
        [Test]
        public async Task Should_not_fail()
        {
            CustomServiceControlSettings = s =>
            {
                var currentSetting = s.RemoteInstances[0];
                s.RemoteInstances = new[]
                {
                    currentSetting,
                    new RemoteInstanceSetting
                    {
                        ApiUri = "http://localhost:12121",
                        QueueAddress = ServiceControlAuditInstanceName
                    }
                };
            };
            
            //search for the message type
            var searchString = typeof(MyMessage).Name;

            await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.SendLocal(new MyMessage())))
                .Done(async c => await this.TryGetMany<MessagesView>("/api/messages/search/" + searchString, instanceName: ServiceControlInstanceName))
                .Run();
        }
        
        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.MessageId = context.MessageId;
                    return Task.FromResult(0);
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