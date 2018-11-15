namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Operations;

    class When_a_message_fails_to_import : AcceptanceTest
    {
        [Test]
        public async Task It_is_stored_in_the_failed_errors_collection()
        {
            //Make sure the error import attempt fails
            CustomConfiguration = config => { config.RegisterComponents(c => c.ConfigureComponent<FailOnceEnricher>(DependencyLifecycle.SingleInstance)); };

            FailedErrorsCountReponse countReponse;

            await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage())))
                .WithEndpoint<Receiver>(b => b.DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    if (c.UniqueMessageId == null)
                    {
                        return false;
                    }
                    var result = await this.TryGet<FailedErrorsCountReponse>("/api/failederrors/count");
                    countReponse = result;
                    if (result && countReponse.Count > 0)
                    {
                        return true;
                    }
                    return false;
                })
                .Run();
        }

        class FailOnceEnricher : ImportEnricher
        {
            public MyContext Context { get; set; }

            public override Task Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                if (!Context.FailedImport)
                {
                    TestContext.WriteLine("Simulating message processing failure");
                    throw new MessageDeserializationException("ID", null);
                }

                TestContext.WriteLine("Message processed correctly");
                return Task.FromResult(0);
            }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    var recoverability = c.Recoverability();
                    recoverability.Immediate(x => x.NumberOfRetries(0));
                    recoverability.Delayed(x => x.NumberOfRetries(0));
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.UniqueMessageId = ServiceControl.Infrastructure.DeterministicGuid.MakeId(context.MessageId, Settings.EndpointName()).ToString();
                    throw new Exception("Simulated");
                }
            }
        }

        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public bool FailedImport { get; set; }
            public string UniqueMessageId { get; set; }
        }
    }
}