namespace ServiceControl.AcceptanceTests.WebApi
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using CompositeViews.Messages;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NUnit.Framework;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_failed_messages_are_queried : AcceptanceTest
    {
        [Test]
        public async Task Should_filter_by_endpoint_and_by_search_term()
        {
            List<MessagesView> forBilling = null;
            List<MessagesView> matchingTerm = null;
            List<MessagesView> forBillingMatchingTerm = null;
            List<MessagesView> searchRoute = null;
            List<MessagesView> endpointSearchRoute = null;

            await Define<Context>()
                .WithEndpoint<Sender>(b => b.When(async (bus, _) =>
                {
                    await bus.Send(new InvoiceFailed { Description = $"Invoice for the {SearchTerm} run" });
                    await bus.Send(new LabelFailed { Description = $"Label for the {SearchTerm} run" });
                    await bus.Send(new DeliveryFailed { Description = "Delivery booked for the same day" });
                }))
                .WithEndpoint<Billing>(b => b.DoNotFailOnErrorMessages())
                .WithEndpoint<Shipping>(b => b.DoNotFailOnErrorMessages())
                .Do("Wait for all three failures to be ingested", async _ =>
                    (await Query(string.Empty)).Count == 3)
                .Do("Wait for the search index to catch up", async ctx =>
                {
                    matchingTerm = await Query($"q={SearchTerm}");

                    ctx.SearchMatched = string.Join(", ", TypesIn(matchingTerm));

                    return matchingTerm.Count >= 2;
                })
                .Do("Query the list the way ServicePulse filters it", async _ =>
                {
                    forBilling = await Query($"endpoint_name={BillingEndpoint}");
                    forBillingMatchingTerm = await Query($"endpoint_name={BillingEndpoint}&q={SearchTerm}");
                })
                .Do("Query the same two things through the search routes beside it", async _ =>
                {
                    searchRoute = await Paged($"/api/messages/search?q={SearchTerm}");
                    endpointSearchRoute = await Paged($"/api/endpoints/{BillingEndpoint}/messages/search?q={SearchTerm}");
                })
                .Done(_ => true)
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(TypesIn(forBilling), Is.EquivalentTo(new[] { NameOf<InvoiceFailed>() }),
                    "endpoint_name has to drop the other endpoint's failures, not just keep this endpoint's");

                Assert.That(TypesIn(matchingTerm), Is.EquivalentTo(new[] { NameOf<InvoiceFailed>(), NameOf<LabelFailed>() }),
                    $"q has to match the bodies carrying '{SearchTerm}' across endpoints, and drop the one without it");

                Assert.That(TypesIn(searchRoute), Is.EquivalentTo(TypesIn(matchingTerm)),
                    "messages/search and messages2 run the same search, so ServicePulse gets the same answer whichever it asks");

                Assert.That(TypesIn(endpointSearchRoute), Is.EquivalentTo(new[] { NameOf<InvoiceFailed>() }),
                    "Scoping the search route to an endpoint has to drop the other endpoint's match");

                Assert.That(TypesIn(forBillingMatchingTerm), Is.EquivalentTo(new[] { NameOf<InvoiceFailed>() }),
                    "Supplying both narrows to the intersection rather than applying whichever filter is read last");

                Assert.That(ReceiversIn(forBillingMatchingTerm), Is.EquivalentTo(new[] { BillingEndpoint }));
            }
        }

        async Task<List<MessagesView>> Paged(string url) =>
            (await this.TryGetMany<MessagesView>($"{url}&per_page=50")).Items;

        async Task<List<MessagesView>> Query(string filter)
        {
            var result = await this.TryGetMany<MessagesView>($"/api/messages2?page_size=50&{filter}");

            return result.Items;
        }

        static IEnumerable<string> TypesIn(IEnumerable<MessagesView> messages) =>
            messages.Select(message => message.MessageType);

        static IEnumerable<string> ReceiversIn(IEnumerable<MessagesView> messages) =>
            messages.Select(message => message.ReceivingEndpoint.Name).Distinct();

        static string NameOf<T>() => typeof(T).FullName;

        const string SearchTerm = "overnight";

        static string BillingEndpoint => Conventions.EndpointNamingConvention(typeof(Billing));

        class Context : ScenarioContext, ISequenceContext
        {
            public int Step { get; set; }

            public string SearchMatched { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(InvoiceFailed), typeof(Billing));
                    routing.RouteToEndpoint(typeof(LabelFailed), typeof(Shipping));
                    routing.RouteToEndpoint(typeof(DeliveryFailed), typeof(Shipping));
                });
        }

        public class Billing : EndpointConfigurationBuilder
        {
            public Billing() => EndpointSetup<DefaultServerWithoutAudit>(c => c.NoRetries());

            [Handler]
            public class InvoiceFailedHandler : IHandleMessages<InvoiceFailed>
            {
                public Task Handle(InvoiceFailed message, IMessageHandlerContext context) =>
                    throw new Exception("Simulated exception");
            }
        }

        public class Shipping : EndpointConfigurationBuilder
        {
            public Shipping() => EndpointSetup<DefaultServerWithoutAudit>(c => c.NoRetries());

            [Handler]
            public class LabelFailedHandler : IHandleMessages<LabelFailed>
            {
                public Task Handle(LabelFailed message, IMessageHandlerContext context) =>
                    throw new Exception("Simulated exception");
            }

            [Handler]
            public class DeliveryFailedHandler : IHandleMessages<DeliveryFailed>
            {
                public Task Handle(DeliveryFailed message, IMessageHandlerContext context) =>
                    throw new Exception("Simulated exception");
            }
        }

        public class InvoiceFailed : ICommand
        {
            public string Description { get; set; }
        }

        public class LabelFailed : ICommand
        {
            public string Description { get; set; }
        }

        public class DeliveryFailed : ICommand
        {
            public string Description { get; set; }
        }
    }
}
