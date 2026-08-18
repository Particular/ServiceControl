namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using CompositeViews.Messages;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    class ErrorImportPerformanceTests : AcceptanceTest
    {
        [Test]
        [CancelAfter(180_000)]
        public async Task Should_import_all_messages(CancellationToken cancellationToken = default)
        {
            await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(bus => Task.WhenAll(Enumerable.Repeat(0, ExpectedMessages).Select(i => bus.SendLocal(new MyMessage())))).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    var result = await this.TryGetMany<MessagesView>("/api/messages?per_page=150");
                    c.MessagesImported = result ? ((List<MessagesView>)result).Count : 0;

                    return c.MessagesImported >= ExpectedMessages;
                })
                .Run(cancellationToken);
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServerWithoutAudit>(c => c.Recoverability().Delayed(s => s.NumberOfRetries(0)));

            [Handler]
            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context) => throw new Exception("Simulated exception");
            }
        }


        public class MyMessage : ICommand;

        const int ExpectedMessages = 100;

        public class MyContext : ScenarioContext
        {
            public int MessagesImported { get; set; }
        }
    }
}