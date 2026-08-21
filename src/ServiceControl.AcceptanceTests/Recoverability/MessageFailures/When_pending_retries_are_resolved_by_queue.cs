namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_pending_retries_are_resolved_by_queue : AcceptanceTest
    {
        [Test]
        public async Task Should_resolve_only_the_queue_it_was_given()
        {
            FailedMessage billingAfterResolve = null;
            FailedMessage shippingAfterResolve = null;

            await Define<Context>()
                .WithEndpoint<Billing>(b => b.When(bus => bus.SendLocal(new BillingCommand())).DoNotFailOnErrorMessages())
                .WithEndpoint<Shipping>(b => b.When(bus => bus.SendLocal(new ShippingCommand())).DoNotFailOnErrorMessages())
                .Do("Wait for both queues to have a failure", async ctx =>
                {
                    var failures = await this.TryGetMany<FailedMessageView>("/api/errors?per_page=50");

                    if (failures.Items.Count != 2)
                    {
                        return false;
                    }

                    var billing = failures.Items.Single(failure => failure.ReceivingEndpoint.Name == BillingEndpoint);
                    var shipping = failures.Items.Single(failure => failure.ReceivingEndpoint.Name == ShippingEndpoint);

                    ctx.BillingQueue = billing.QueueAddress;
                    ctx.ShippingQueue = shipping.QueueAddress;
                    ctx.BillingId = billing.Id;
                    ctx.ShippingId = shipping.Id;

                    // Set only once both have failed, so neither original failure is skipped.
                    ctx.FixDeployed = true;

                    return true;
                })
                .Do("Retry everything on both queues", async ctx =>
                {
                    await this.Post<object>($"/api/errors/queues/{ctx.BillingQueue}/retry");
                    await this.Post<object>($"/api/errors/queues/{ctx.ShippingQueue}/retry");
                })
                .Do("Wait until both retries are left pending", async ctx =>
                {
                    // The endpoints suppress the notification that would tell ServiceControl the retry
                    // was handled, which is what leaves a retry pending in production too.
                    var billing = await this.TryGet<FailedMessage>($"/api/errors/{ctx.BillingId}",
                        message => message.Status == FailedMessageStatus.RetryIssued);

                    var shipping = await this.TryGet<FailedMessage>($"/api/errors/{ctx.ShippingId}",
                        message => message.Status == FailedMessageStatus.RetryIssued);

                    return billing.HasResult && shipping.HasResult;
                })
                .Do("Resolve the pending retries on one queue only", async ctx =>
                {
                    await this.Patch("/api/pendingretries/queues/resolve", new
                    {
                        queueaddress = ctx.BillingQueue,
                        from = DateTime.UtcNow.AddHours(-1).ToString("o"),
                        to = DateTime.UtcNow.AddHours(1).ToString("o")
                    });

                    var billing = await this.TryGet<FailedMessage>($"/api/errors/{ctx.BillingId}",
                        message => message.Status == FailedMessageStatus.Resolved);

                    billingAfterResolve = billing.Item;

                    return billing.HasResult;
                })
                .Do("Look at the queue that was not named", async ctx =>
                {
                    shippingAfterResolve = await this.TryGet<FailedMessage>($"/api/errors/{ctx.ShippingId}");
                })
                .Done(_ => true)
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(billingAfterResolve.Status, Is.EqualTo(FailedMessageStatus.Resolved));

                Assert.That(shippingAfterResolve.Status, Is.EqualTo(FailedMessageStatus.RetryIssued),
                    "Resolving by queue has to leave another queue's pending retry alone, which is the only thing separating this route from the timeframe one");
            }
        }

        static string BillingEndpoint => Conventions.EndpointNamingConvention(typeof(Billing));
        static string ShippingEndpoint => Conventions.EndpointNamingConvention(typeof(Shipping));

        public class Context : ScenarioContext, ISequenceContext
        {
            public int Step { get; set; }

            public bool FixDeployed { get; set; }

            public string BillingQueue { get; set; }

            public string ShippingQueue { get; set; }

            public string BillingId { get; set; }

            public string ShippingId { get; set; }
        }

        public class Billing : EndpointConfigurationBuilder
        {
            public Billing() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.DisableFeature<PlatformRetryNotifications>();
                    c.NoRetries();
                    c.NoOutbox();
                });

            [Handler]
            public class BillingCommandHandler(Context scenarioContext) : IHandleMessages<BillingCommand>
            {
                public Task Handle(BillingCommand message, IMessageHandlerContext context)
                {
                    if (!scenarioContext.FixDeployed)
                    {
                        throw new Exception("Simulated exception");
                    }

                    return Task.CompletedTask;
                }
            }
        }

        public class Shipping : EndpointConfigurationBuilder
        {
            public Shipping() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.DisableFeature<PlatformRetryNotifications>();
                    c.NoRetries();
                    c.NoOutbox();
                });

            [Handler]
            public class ShippingCommandHandler(Context scenarioContext) : IHandleMessages<ShippingCommand>
            {
                public Task Handle(ShippingCommand message, IMessageHandlerContext context)
                {
                    if (!scenarioContext.FixDeployed)
                    {
                        throw new Exception("Simulated exception");
                    }

                    return Task.CompletedTask;
                }
            }
        }

        public class BillingCommand : ICommand;

        public class ShippingCommand : ICommand;
    }
}
