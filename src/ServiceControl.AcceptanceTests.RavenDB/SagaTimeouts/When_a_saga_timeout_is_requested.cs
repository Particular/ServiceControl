namespace ServiceControl.AcceptanceTests.RavenDB.SagaTimeouts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Persistence.SagaTimeouts;
    using ServiceBus.Management.Infrastructure.Settings;

    class When_a_saga_timeout_is_requested : AcceptanceTest
    {
        [Test]
        public async Task Should_list_it_as_pending()
        {
            SagaTimeoutView listed = null;
            SagaTimeoutDetails details = null;

            await Define<Context>()
                .WithEndpoint<Sales>()
                .Do("Wait for the timeout to be listed", async ctx =>
                {
                    var timeouts = await this.TryGetMany<SagaTimeoutView>("/api/timeouts?status=pending", timeout => timeout.TimeoutId == ctx.TimeoutId);
                    listed = timeouts.HasResult ? timeouts.Items.Single() : null;
                    return listed != null;
                })
                .Do("Read its details", async ctx =>
                {
                    var result = await this.TryGet<SagaTimeoutDetails>($"/api/timeouts/{ctx.TimeoutId}");
                    details = result.HasResult ? result.Item : null;
                    return details != null;
                })
                .Done(_ => true)
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(listed.Status, Is.EqualTo(SagaTimeoutStatus.Pending));
                Assert.That(listed.Version, Is.EqualTo(1));
                Assert.That(listed.ManagementAddress, Is.EqualTo("Sales.SagaTimeouts"));
                Assert.That(details.Payload, Is.EqualTo("{\"OrderId\":\"order-1\"}"));
                Assert.That(details.History.Single().Kind, Is.EqualTo(SagaTimeoutChangeKind.Requested));
            });
        }

        class Context : ScenarioContext, ISequenceContext
        {
            public int Step { get; set; }
            public string TimeoutId { get; } = Guid.NewGuid().ToString();
        }

        public class Sales : EndpointConfigurationBuilder
        {
            public Sales() => EndpointSetup<DefaultServerWithoutAudit>(c => c.EnableFeature<ReportTimeoutRequest>());

            // Sent as NServiceBus sends it: the enclosed type is the full name without an assembly, and the body is System.Text.Json
            class ReportTimeoutRequest : DispatchRawMessages<Context>
            {
                protected override TransportOperations CreateMessage(Context context)
                {
                    var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"OrderId\":\"order-1\"}"));
                    var body = $$"""
                        {"TriggerId":"trigger-1","DueTime":"{{DateTimeOffset.UtcNow.AddHours(1):O}}","PayloadContentType":"application/json","Payload":"{{payload}}",
                         "TimeoutId":"{{context.TimeoutId}}","TimeoutType":"Sales.PaymentTimeout","Version":1,"EndpointName":"Sales","SagaType":"Sales.OrderSaga",
                         "SagaId":"{{Guid.NewGuid()}}","CorrelationPropertyValue":"order-1","ChangedAt":"{{DateTimeOffset.UtcNow:O}}",
                         "Host":"host","HostId":"host-id","ManagementAddress":"Sales.SagaTimeouts"}
                        """;

                    var headers = new Dictionary<string, string>
                    {
                        [Headers.EnclosedMessageTypes] = "NServiceBus.SagaTimeoutRequested",
                        [Headers.ContentType] = "application/json",
                        [Headers.MessageIntent] = "Send"
                    };

                    var message = new OutgoingMessage(Guid.NewGuid().ToString(), headers, Encoding.UTF8.GetBytes(body));
                    return new TransportOperations(new TransportOperation(message, new UnicastAddressTag(Settings.DEFAULT_INSTANCE_NAME)));
                }
            }
        }
    }
}
