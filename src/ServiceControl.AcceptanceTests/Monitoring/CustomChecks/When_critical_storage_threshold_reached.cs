namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using MessageFailures.Api;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Operations;
    using ServiceBus.Management.Infrastructure.Settings;

    [TestFixture]
    class When_critical_storage_threshold_reached : AcceptanceTest
    {
        [SetUp]
        public void SetupIngestion()
        {
            SetSettings = s =>
            {
                s.TimeToRestartErrorIngestionAfterFailure = TimeSpan.FromSeconds(1);
                s.DisableHealthChecks = false;
            };
        }

        RavenPersisterSettings PersisterSettings => (RavenPersisterSettings)Settings.PersisterSpecificSettings;

        [Test]
        public async Task Should_stop_ingestion()
        {
            await Define<ScenarioContext>()
                .WithEndpoint<Sender>(b => b
                    .When(context =>
                    {
                        return context.Logs.ToArray().Any(i => i.Message.StartsWith(ErrorIngestion.LogMessages.StoppedInfrastructure));
                    }, (_, __) =>
                    {
                        PersisterSettings.MinimumStorageLeftRequiredForIngestion = 100;
                        PersisterSettings.DatabasePath = TestContext.CurrentContext.TestDirectory;
                        return Task.CompletedTask;
                    })
                    .When(context =>
                    {
                        return context.Logs.ToArray().Any(i =>
                            i.Message.StartsWith(ErrorIngestion.LogMessages.StoppedInfrastructure));
                    }, (bus, c) => bus.SendLocal(new MyMessage())
                    )
                    .DoNotFailOnErrorMessages())
                .Done(async c => await this.TryGetSingle<FailedMessageView>("/api/errors") == false)
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
                        return context.Logs.ToArray().Any(i =>
                            i.Message.StartsWith(ErrorIngestion.LogMessages.StartedInfrastructure));
                    }, (session, context) =>
                    {
                        PersisterSettings.MinimumStorageLeftRequiredForIngestion = 100;
                        PersisterSettings.DatabasePath = TestContext.CurrentContext.TestDirectory;
                        return Task.CompletedTask;
                    })
                    .When(context =>
                    {
                        ingestionShutdown = context.Logs.ToArray().Any(i =>
                            i.Message.StartsWith(ErrorIngestion.LogMessages.StoppedInfrastructure));

                        return ingestionShutdown;
                    },
                        (bus, c) =>
                        {
                            return bus.SendLocal(new MyMessage());
                        })
                    .When(c => ingestionShutdown, (session, context) =>
                    {
                        PersisterSettings.MinimumStorageLeftRequiredForIngestion = 0;
                        return Task.CompletedTask;
                    })
                    .DoNotFailOnErrorMessages())
                .Done(async c => await this.TryGetSingle<FailedMessageView>("/api/errors"))
                .Run();


        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.ReportCustomChecksTo(Settings.DEFAULT_INSTANCE_NAME, TimeSpan.FromSeconds(1));
                    c.NoRetries();
                });

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    throw new ApplicationException("Big Error!");
                }
            }
        }

        public class MyMessage : ICommand
        {
        }
    }
}