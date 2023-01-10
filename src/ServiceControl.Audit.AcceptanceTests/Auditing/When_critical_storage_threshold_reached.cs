namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Audit.Auditing.MessagesView;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using TestSupport.EndpointTemplates;

    [RunOnAllTransports]
    class When_critical_storage_threshold_reached : AcceptanceTest
    {
        [Test]
        public async Task Should_stop_ingestion()
        {
            SetStorageConfiguration = d => { d.Add("RavenDB5/MinimumStorageLeftRequiredForIngestionKey", "100"); };

            await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When(context =>
                {
                    return context.Logs.ToArray().Count(x => x.Message.Equals(
                               "Shutting down due to failed persistence health check. Infrastructure shut down completed")) >
                           0;
                }, (bus, c) => bus.SendLocal(new MyMessage())))
                .Done(async c =>
                {
                    return await this.TryGetSingle<MessagesView>(
                        "/api/messages?include_system_messages=false&sort=id") == false;
                })
                .Run();
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context) => Task.CompletedTask;
            }
        }

        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
        }
    }
}