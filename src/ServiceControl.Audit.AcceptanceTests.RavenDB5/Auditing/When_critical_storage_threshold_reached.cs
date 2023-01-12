namespace ServiceControl.Audit.AcceptanceTests.RavenDB5.Auditing
{
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Persistence.RavenDb5.CustomChecks;
    using ServiceControl.AcceptanceTesting;
    using ServiceControl.Audit.AcceptanceTests.TestSupport.EndpointTemplates;
    using ServiceControl.Audit.Auditing.MessagesView;

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
                    var checkState = ServiceProvider.GetRequiredService<AuditStorageCustomCheck.State>();

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