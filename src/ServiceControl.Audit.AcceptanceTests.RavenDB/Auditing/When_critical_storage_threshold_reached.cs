namespace ServiceControl.Audit.AcceptanceTests.RavenDB.Auditing
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Persistence.RavenDB;
    using ServiceControl.AcceptanceTesting;
    using ServiceControl.Audit.AcceptanceTests.TestSupport.EndpointTemplates;
    using ServiceControl.Audit.Auditing.MessagesView;
    using ServiceControl.Audit.Persistence;

    [RunOnAllTransports]
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
                d.Add(RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey, "0");
            };

            await Define<ScenarioContext>()
                .WithEndpoint<Sender>(b => b
                    .When(context =>
                    {
                        return context.Logs.ToArray().Any(i =>
                            i.Message.StartsWith(
                                "Ensure started. Infrastructure started"));
                    }, (_, __) =>
                    {
                        var persistenceSettings = ServiceProvider.GetRequiredService<PersistenceSettings>();
                        persistenceSettings.PersisterSpecificSettings[RavenBootstrapper.DatabasePathKey] = TestContext.CurrentContext.TestDirectory;
                        persistenceSettings.PersisterSpecificSettings[RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey] = "100";
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

        [Test]
        public async Task Should_stop_ingestion_and_resume_when_more_space_is_available()
        {
            SetStorageConfiguration = d =>
            {
                d.Add(RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey, "0");
            };

            var ingestionShutdown = false;
            ScenarioContext result = null;

            await Define<ScenarioContext>()
                .WithEndpoint<Sender>(b => b
                    .When(context =>
                    {
                        result = context;
                        return context.Logs.ToArray().Any(i =>
                            i.Message.StartsWith(
                                "Ensure started. Infrastructure started"));
                    }, (session, context) =>
                    {
                        var persistenceSettings = ServiceProvider.GetRequiredService<PersistenceSettings>();
                        persistenceSettings.PersisterSpecificSettings[RavenBootstrapper.DatabasePathKey] = TestContext.CurrentContext.TestDirectory;
                        persistenceSettings.PersisterSpecificSettings[RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey] = "100";
                        return Task.CompletedTask;
                    })
                    .When(context =>
                        {
                            ingestionShutdown = context.Logs.ToArray().Any(i =>
                                i.Message.StartsWith(
                                    "Shutting down due to failed persistence health check. Infrastructure shut down completed"));

                            return ingestionShutdown;
                        },
                        (bus, c) =>
                        {
                            return bus.SendLocal(new MyMessage());
                        })
                    .When(c => ingestionShutdown, (session, context) =>
                    {
                        var persistenceSettings = ServiceProvider.GetService<PersistenceSettings>();
                        persistenceSettings.PersisterSpecificSettings[RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey] = "0";
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