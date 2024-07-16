namespace ServiceControl.Monitoring.AcceptanceTests.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Http.Diagrams;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;


    class When_querying_queue_length_data : AcceptanceTest
    {
        [Test]
        public async Task Should_report_via_http()
        {
            var endpointName = NServiceBus.AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(SendingEndpoint));
            var instanceId = Guid.NewGuid();
            var metricsInstanceId = Guid.NewGuid();

            MonitoredEndpointDetails monitoredEndpointDetails = null;
            MonitoredEndpointInstance instance1 = null;
            MonitoredEndpointInstance instance2 = null;

            await Define<TestContext>()
                 .WithEndpoint<SendingEndpoint>(c =>
                 {
                     c.CustomConfig(ec =>
                     {
                         ec.MakeInstanceUniquelyAddressable("1");
                         ec.UniquelyIdentifyRunningInstance()
                             .UsingCustomIdentifier(instanceId);
                         ec.EnableMetrics()
                             .SendMetricDataToServiceControl(Settings.DEFAULT_SERVICE_NAME, TimeSpan.FromSeconds(1));
                     });
                     c.DoNotFailOnErrorMessages();
                     c.When(async s =>
                     {
                         for (var i = 0; i < 10; i++)
                         {
                             await s.SendLocal(new SampleMessage());
                         }
                     });
                 })
                 .WithEndpoint<SendingEndpoint>(c =>
                 {
                     c.CustomConfig(ec =>
                     {
                         ec.MakeInstanceUniquelyAddressable("2");
                         ec.EnableMetrics()
                             .SendMetricDataToServiceControl(Settings.DEFAULT_SERVICE_NAME,
                             TimeSpan.FromSeconds(1),
                             metricsInstanceId.ToString("N"));
                     });
                     c.DoNotFailOnErrorMessages();
                     c.When(async s =>
                     {
                         for (var i = 0; i < 10; i++)
                         {
                             await s.SendLocal(new SampleMessage());
                         }
                     });
                 })
                 .Done(async c =>
                 {
                     var result = await this.TryGet<MonitoredEndpointDetails>($"/monitored-endpoints/{endpointName}");

                     if (!result.HasResult)
                     {
                         return false;
                     }

                     monitoredEndpointDetails = result.Item;

                     instance1 = monitoredEndpointDetails.Instances.SingleOrDefault(instance => instance.Id == instanceId.ToString("N"));
                     instance2 = monitoredEndpointDetails.Instances.SingleOrDefault(instance => instance.Id == metricsInstanceId.ToString("N"));

                     if (instance1 == null || instance2 == null)
                     {
                         return false;
                     }

                     if (monitoredEndpointDetails.Digest.Metrics.TryGetValue("queueLength", out var queueLength) && queueLength.Average == 0.0)
                     {
                         return false;
                     }

                     c.TestEnded.SetResult(true);

                     return true;
                 })
                 .Run();
        }

        class SendingEndpoint : EndpointConfigurationBuilder
        {
            public SendingEndpoint() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.LimitMessageProcessingConcurrencyTo(1);
                });

            class Handler(TestContext testContext) : IHandleMessages<SampleMessage>
            {
                public Task Handle(SampleMessage message, IMessageHandlerContext context) =>
                    //Concurrency limit 1 and this should block any processing on input queue
                    Task.WhenAny(
                        Task.Delay(TimeSpan.FromSeconds(30), context.CancellationToken),
                        testContext.TestEnded.Task
                    );
            }
        }

        class SampleMessage : SampleBaseMessage
        {
        }

        class SampleBaseMessage : IMessage
        {
        }

        class TestContext : ScenarioContext
        {
            public TaskCompletionSource<bool> TestEnded = new();
        }
    }
}