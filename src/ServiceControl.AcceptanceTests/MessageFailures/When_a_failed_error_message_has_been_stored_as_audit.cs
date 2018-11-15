namespace ServiceBus.Management.AcceptanceTests.Audit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.Faults;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;
    using ServiceControl.Operations;

    /// <summary>
    /// This is a specific test for https://github.com/Particular/ServiceControl/issues/1490 - an error message that ends up in failed audits collection
    /// To simulate this we used audit message that contains an artificial FailedQ header
    /// </summary>
    class When_a_failed_error_message_has_been_stored_as_audit : AcceptanceTest
    {
        [Test]
        public async Task It_can_be_reimported()
        {
            //Make sure the audit import attempt fails
            CustomConfiguration = config => { config.RegisterComponents(c => c.ConfigureComponent<FailOnceEnricher>(DependencyLifecycle.SingleInstance)); };

            await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) =>
                {
                    var ops = new SendOptions();
                    ops.SetHeader(FaultsHeaderKeys.FailedQ, "SomeFailedQ");
                    return bus.Send(new MyMessage(), ops);
                }))
                .WithEndpoint<Receiver>()
                .Done(async c =>
                {
                    if (c.UniqueMessageId == null)
                    {
                        return false;
                    }

                    if (!c.WasImportedAgain)
                    {
                        var countResult = await this.TryGet<FailedAuditsCountReponse>("/api/failedaudits/count");
                        FailedAuditsCountReponse failedImportsCountResponse = countResult;
                        if (!countResult)
                        {
                            return false;
                        }

                        if (failedImportsCountResponse.Count <= 0)
                        {
                            return false;
                        }

                        c.FailedImport = true;
                        await this.Post<object>("/api/failedaudits/import", null);
                        c.WasImportedAgain = true;
                        return true;
                    }

                    return await this.TryGet<FailedMessage>($"/api/errors/{c.UniqueMessageId}");
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
                EndpointSetup<DefaultServerWithAudit>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, Settings.EndpointName()).ToString();
                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public bool FailedImport { get; set; }
            public bool WasImportedAgain { get; set; }
            public string UniqueMessageId { get; set; }
        }
    }
}