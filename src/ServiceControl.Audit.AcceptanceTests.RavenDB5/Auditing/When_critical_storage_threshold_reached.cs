namespace ServiceControl.Audit.AcceptanceTests.RavenDB5.Auditing
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Persistence.RavenDb;
    using ServiceControl.AcceptanceTesting;
    using ServiceControl.Audit.AcceptanceTests.TestSupport.EndpointTemplates;
    using ServiceControl.Audit.Auditing.MessagesView;

    [RunOnAllTransports]
    class When_critical_storage_threshold_reached : AcceptanceTest
    {
        [Test]
        public async Task Should_stop_ingestion()
        {
            SetSettings = s =>
            {
                s.TimeToRestartAuditIngestionAfterFailure = TimeSpan.FromSeconds(1);
            };

            SetStorageConfiguration = d =>
            {
                d.Add("RavenDB5/MinimumStorageLeftRequiredForIngestionKey", "100");
            };

            await Define<ScenarioContext>()
                .WithEndpoint<Sender>(b => b
                    .When((session, context) =>
                    {
                        var databaseConfiguration = ServiceProvider.GetRequiredService<DatabaseConfiguration>();
                        databaseConfiguration.ServerConfiguration.DbPath = "c:\\";
                        return Task.CompletedTask;
                    })
                    .When(context =>
                    {
                        return context.Logs.ToArray().Any(i =>
                            i.Message.StartsWith(
                                "Shutting down due to failed persistence health check. Infrastructure shut down completed"));
                    }, (bus, c) => bus.SendLocal(new MyMessage()))
                )
                .Done(async c => await this.TryGetSingle<MessagesView>(
                    "/api/messages?include_system_messages=false&sort=id") == false)
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
    }
}