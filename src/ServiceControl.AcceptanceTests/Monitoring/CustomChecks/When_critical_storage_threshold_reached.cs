namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Messages;
    using TestSupport.EndpointTemplates;

    [TestFixture]
    class When_critical_storage_threshold_reached : AcceptanceTest
    {

        [SetUp]
        public void SetupIngestion() =>
            SetSettings = s =>
            {
                s.TimeToRestartErrorIngestionAfterFailure = TimeSpan.FromSeconds(1);
                s.DbPath = TestContext.CurrentContext.TestDirectory;
                s.DisableHealthChecks = false;
                s.MinimumStorageLeftRequiredForIngestion = 100;
            };


        [Test]
        public async Task Should_stop_ingestion()
        {
            await Define<ScenarioContext>()
                .WithEndpoint<Sender>(b => b
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

        [Test]
        public async Task Should_stop_ingestion_and_resume_when_more_space_is_available()
        {
            var ingestionShutdown = false;

            await Define<ScenarioContext>()
                .WithEndpoint<Sender>(b => b
                    .When(context =>
                    {
                        ingestionShutdown = context.Logs.ToArray().Any(i =>
                            i.Message.StartsWith(
                                "Shutting down due to failed persistence health check. Infrastructure shut down completed"));

                        return ingestionShutdown;
                    },
                    (bus, c) => bus.SendLocal(new MyMessage()))
                    .When(c => ingestionShutdown, (session, context) =>
                    {
                        Settings.MinimumStorageLeftRequiredForIngestion = 0;
                        return Task.CompletedTask;
                    })
                )
                .Done(async c => await this.TryGetSingle<MessagesView>("/api/messages?include_system_messages=false&sort=id"))
                .Run();
        }
        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c => { c.ReportCustomChecksTo(Settings.DEFAULT_SERVICE_NAME, TimeSpan.FromSeconds(1)); });
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