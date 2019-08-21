﻿namespace ServiceControl.Monitoring.SmokeTests.RabbitMQ.Tests
{
    using System;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    [Category("TransportSmokeTests")]
    public class When_querying_retries_data : ApiIntegrationTest
    {
        static string ReceiverEndpointName => NServiceBus.AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(MonitoringEndpoint));

        [Test]
        public async Task Should_report_via_http()
        {
            JToken retries = null;

            await Scenario.Define<Context>()
                .WithEndpoint<MonitoredEndpoint>(c =>
                {
                    c.DoNotFailOnErrorMessages();
                    c.CustomConfig(ec => ec.Recoverability().Immediate(i => i.NumberOfRetries(5)));
                    c.When(s => s.SendLocal(new SampleMessage()));
                })
                .WithEndpoint<MonitoringEndpoint>()
                .Done(c => MetricReported("retries", out retries, c))
                .Run();

            Assert.IsTrue(retries["average"].Value<double>() > 0);
            Assert.AreEqual(60, retries["points"].Value<JArray>().Count);
        }

        class MonitoredEndpoint : EndpointConfigurationBuilder
        {
            public MonitoredEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.EnableMetrics().SendMetricDataToServiceControl(ReceiverEndpointName, TimeSpan.FromSeconds(5));
                });
            }

            class Handler : IHandleMessages<SampleMessage>
            {
                public Task Handle(SampleMessage message, IMessageHandlerContext context)
                {
                    throw new Exception("Boom!");
                }
            }
        }

        class MonitoringEndpoint : EndpointConfigurationBuilder
        {
            public MonitoringEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    EndpointFactory.MakeMetricsReceiver(c, Settings, DefaultServer.ConnectionString);
                    c.LimitMessageProcessingConcurrencyTo(1);
                });
            }
        }
    }
}