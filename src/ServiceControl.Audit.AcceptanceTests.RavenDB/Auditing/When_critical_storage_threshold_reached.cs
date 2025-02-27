namespace ServiceControl.Audit.AcceptanceTests.RavenDB.Auditing
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting.EndpointTemplates;
    using Audit.Auditing;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Persistence.RavenDB;
    using ServiceControl.AcceptanceTesting;
    using ServiceControl.Audit.Auditing.MessagesView;


    class When_critical_storage_threshold_reached : AcceptanceTest
    {
        [SetUp]
        public void SetIngestionRestartInterval() =>
            SetSettings = s =>
            {
                s.TimeToRestartAuditIngestionAfterFailure = TimeSpan.FromSeconds(1);
            };

        [Test]
        public async Task Should_stop_ingestion()
        {
            SetStorageConfiguration = d =>
            {
                d.Add(RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey, "0");
            };

            await Define<ScenarioContext>()
                .WithEndpoint<Sender>(b => b
                    .When(context =>
                    {
                        return context.Logs.ToArray().Any(i =>
                            i.Message.StartsWith(AuditIngestion.LogMessages.StartedInfrastructure));
                    }, (_, __) =>
                    {
                        var databaseConfiguration = ServiceProvider.GetRequiredService<DatabaseConfiguration>();
                        databaseConfiguration.ServerConfiguration.DbPath = TestContext.CurrentContext.TestDirectory;
                        databaseConfiguration.MinimumStorageLeftRequiredForIngestion = 100;
                        return Task.CompletedTask;
                    })
                    .When(context =>
                    {
                        return context.Logs.ToArray().Any(i =>
                            i.Message.StartsWith(AuditIngestion.LogMessages.StoppedInfrastructure));
                    }, (bus, c) => bus.SendLocal(new MyMessage()))
                )
                .Done(async c => await this.TryGetSingle<MessagesView>(
                    "/api/messages?include_system_messages=false&sort=id") == false)
                .Run();
        }

        [Test]
        public async Task Should_stop_ingestion_and_resume_when_more_space_is_available()
        {
            SetStorageConfiguration = d =>
            {
                d.Add(RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey, "0");
            };

            var ingestionShutdown = false;

            await Define<ScenarioContext>()
               .WithEndpoint<Sender>(b => b
                   .When(context =>
                   {
                       return context.Logs.ToArray().Any(i =>
                           i.Message.StartsWith(
                                AuditIngestion.LogMessages.StartedInfrastructure));
                   }, (session, context) =>
                   {
                       var databaseConfiguration = ServiceProvider.GetRequiredService<DatabaseConfiguration>();
                       databaseConfiguration.ServerConfiguration.DbPath = TestContext.CurrentContext.TestDirectory;
                       databaseConfiguration.MinimumStorageLeftRequiredForIngestion = 100;
                       return Task.CompletedTask;
                   })
                   .When(context =>
                   {
                       ingestionShutdown = context.Logs.ToArray().Any(i =>
                           i.Message.StartsWith(AuditIngestion.LogMessages.StoppedInfrastructure));

                       return ingestionShutdown;
                   },
                       (bus, c) => bus.SendLocal(new MyMessage()))
                   .When(c => ingestionShutdown, (session, context) =>
                   {
                       var databaseConfiguration = ServiceProvider.GetRequiredService<DatabaseConfiguration>();
                       databaseConfiguration.MinimumStorageLeftRequiredForIngestion = 0;
                       return Task.CompletedTask;
                   })
               )
               .Done(async c => await this.TryGetSingle<MessagesView>("/api/messages?include_system_messages=false&sort=id"))
               .Run(TimeSpan.FromSeconds(120));
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() => EndpointSetup<DefaultServerWithAudit>();

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context) => Task.CompletedTask;
            }
        }

        public class MyMessage : ICommand;
    }
}