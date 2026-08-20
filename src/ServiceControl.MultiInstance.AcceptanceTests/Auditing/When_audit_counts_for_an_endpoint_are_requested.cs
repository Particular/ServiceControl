namespace ServiceControl.MultiInstance.AcceptanceTests.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using NServiceBus.AcceptanceTesting.Customization;
    using ServiceControl.Audit.Auditing;
    using TestSupport;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_audit_counts_for_an_endpoint_are_requested : AcceptanceTest
    {
        [Test]
        public async Task Should_come_from_the_audit_instance_only()
        {
            List<AuditCount> counted = null;
            List<AuditCount> unknownEndpoint = null;

            await Define<Context>()
                .WithEndpoint<Sender>(b => b.When((bus, _) => bus.Send(new MyMessage())))
                .WithEndpoint<ReceiverRemote>()
                .Done(async _ =>
                {
                    var forReceiver = await this.TryGetMany<AuditCount>(
                        $"/api/endpoints/{ReceiverEndpoint}/audit-count",
                        count => count.Count > 0,
                        instanceName: ServiceControlInstanceName);

                    if (!forReceiver.HasResult)
                    {
                        return false;
                    }

                    counted = forReceiver.Items;

                    unknownEndpoint = (await this.TryGetMany<AuditCount>(
                        $"/api/endpoints/AnEndpointThatNeverRan/audit-count",
                        instanceName: ServiceControlInstanceName)).Items;

                    return true;
                })
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(counted.Sum(count => count.Count), Is.GreaterThan(0),
                    "The primary instance holds no audit data of its own, so a count here can only have come from the audit instance");

                Assert.That(counted.Select(count => count.UtcDate), Is.Unique,
                    "Counts are merged per day across instances, which is what the endpoint's audit chart plots");

                Assert.That(counted, Has.All.Matches<AuditCount>(count => count.UtcDate.Date == count.UtcDate),
                    "Each point is a whole day");

                Assert.That(unknownEndpoint, Is.Empty,
                    "An endpoint that processed nothing has nothing to plot");
            }
        }

        static string ReceiverEndpoint => Conventions.EndpointNamingConvention(typeof(ReceiverRemote));

        public class Context : ScenarioContext;

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() =>
                EndpointSetup<DefaultServerWithAudit>(c =>
                    c.ConfigureRouting().RouteToEndpoint(typeof(MyMessage), typeof(ReceiverRemote)));
        }

        public class ReceiverRemote : EndpointConfigurationBuilder
        {
            public ReceiverRemote() => EndpointSetup<DefaultServerWithAudit>(c => { });

            [Handler]
            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context) => Task.CompletedTask;
            }
        }

        public class MyMessage : ICommand;
    }
}
