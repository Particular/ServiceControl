namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using MessageFailures.Api;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;
    using TestSupport.EndpointTemplates;

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

        class PersisterSettingsRetriever : IHostedService
        {
            static PersistenceSettings _persistenceSettings;

            public PersisterSettingsRetriever(PersistenceSettings persistenceSettings, int value)
            {
                _persistenceSettings = persistenceSettings;
                SetMinimumStorageLeftForIngestion(value);
            }

            public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public static void SetMinimumStorageLeftForIngestion(int value) => _persistenceSettings.PersisterSpecificSettings["MinimumStorageLeftRequiredForIngestion"] = value.ToString();
        }

        [Test]
        public async Task Should_stop_ingestion()
        {
            CustomizeHostBuilder = hostBuilder => hostBuilder.ConfigureServices(services => services.AddHostedService(b => new PersisterSettingsRetriever(b.GetRequiredService<PersistenceSettings>(), 0)));

            await Define<ScenarioContext>()
                .WithEndpoint<Sender>(b => b
                    .When(context =>
                    {
                        return context.Logs.ToArray().Any(i => i.Message.StartsWith("Ensure started. Infrastructure started"));
                    }, (_, __) =>
                    {
                        PersisterSettingsRetriever.SetMinimumStorageLeftForIngestion(100);
                        return Task.CompletedTask;
                    })
                    .When(context =>
                    {
                        return context.Logs.ToArray().Any(i =>
                            i.Message.StartsWith(
                                "Shutting down due to failed persistence health check. Infrastructure shut down completed"));
                    }, (bus, c) => bus.SendLocal(new MyMessage())
                    )
                    .DoNotFailOnErrorMessages())
                //.Done(async c => await this.TryGetSingle<FailedMessageView>("/api/errors") == false)
                .Run();
        }

        [Test]
        public async Task Should_stop_ingestion_and_resume_when_more_space_is_available()
        {
            CustomizeHostBuilder = hostBuilder => hostBuilder.ConfigureServices(services => services.AddHostedService(b => new PersisterSettingsRetriever(b.GetRequiredService<PersistenceSettings>(), 0)));
            var ingestionShutdown = false;

            await Define<ScenarioContext>()
               .WithEndpoint<Sender>(b => b
                    .When(context =>
                    {
                        return context.Logs.ToArray().Any(i =>
                            i.Message.StartsWith(
                                "Ensure started. Infrastructure started"));
                    }, (session, context) =>
                    {
                        PersisterSettingsRetriever.SetMinimumStorageLeftForIngestion(100);
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
                        PersisterSettingsRetriever.SetMinimumStorageLeftForIngestion(0);
                        return Task.CompletedTask;
                    })
                    .DoNotFailOnErrorMessages())
                .Done(async c => await this.TryGetSingle<FailedMessageView>("/api/errors"))
                .Run();


        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ReportCustomChecksTo(Settings.DEFAULT_SERVICE_NAME, TimeSpan.FromSeconds(1));
                    c.NoRetries();
                });
            }

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