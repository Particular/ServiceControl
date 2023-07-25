namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using MessageFailures.Api;
    using Microsoft.Extensions.Configuration;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using TestSupport.EndpointTemplates;

    [TestFixture]
    class When_critical_storage_threshold_reached : AcceptanceTest
    {

        [SetUp]
        public void SetupIngestion() =>
            SetSettings = s =>
            {
                s.TimeToRestartErrorIngestionAfterFailure = TimeSpan.FromSeconds(1);
                s.DisableHealthChecks = false;

                SetMinimumStorageLeftForIngestion(0);
            };

        [TearDown]
        public void TearDown() => RemoveMinimumStorageLeftForIngestion();

        [Test]
        public async Task Should_stop_ingestion()
        {
            await Define<ScenarioContext>()
                .WithEndpoint<Sender>(b => b
                    .When(context =>
                    {
                        return context.Logs.ToArray().Any(i => i.Message.StartsWith("Ensure started. Infrastructure started"));
                    }, (_, __) =>
                    {
                        SetMinimumStorageLeftForIngestion(100);
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
                            i.Message.StartsWith(
                                "Ensure started. Infrastructure started"));
                    }, (session, context) =>
                    {
                        SetMinimumStorageLeftForIngestion(100);
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
                        SetMinimumStorageLeftForIngestion(0);
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

        static void SetMinimumStorageLeftForIngestion(int value) =>
            System.Configuration.ConfigurationManager.AppSettings["ServiceControl/MinimumStorageLeftRequiredForIngestion"] = value.ToString();

        static void RemoveMinimumStorageLeftForIngestion() =>
            System.Configuration.ConfigurationManager.AppSettings.Remove("ServiceControl/MinimumStorageLeftRequiredForIngestion");


        public class MyMessage : ICommand
        {
        }
    }
}