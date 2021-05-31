namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Contracts.MessageFailures;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using Operations;
    using ServiceControl.MessageFailures;
    using TestSupport.EndpointTemplates;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    [RunOnAllTransports]
    class When_a_message_fails_to_import : AcceptanceTest
    {
        [Test]
        public async Task It_can_be_reimported()
        {
            CustomConfiguration = config =>
            {
                config.RegisterComponents(c =>
                {
                    //Make sure the error import attempt fails
                    c.ConfigureComponent<FailOnceEnricher>(DependencyLifecycle.SingleInstance);

                    //Register domain event spy
                    c.ConfigureComponent<MessageFailedHandler>(DependencyLifecycle.SingleInstance);
                });
            };

            SetSettings = settings =>
            {
                settings.ForwardErrorMessages = true;
                settings.ErrorLogQueue = Conventions.EndpointNamingConvention(typeof(ErrorLogSpy));
            };

            var runResult = await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage())))
                .WithEndpoint<Receiver>(b => b.DoNotFailOnErrorMessages())
                .WithEndpoint<ErrorLogSpy>()
                .Done(async c =>
                {
                    if (c.UniqueMessageId == null)
                    {
                        return false;
                    }

                    if (!c.WasImportedAgain)
                    {
                        var result = await this.TryGet<FailedErrorsCountReponse>("/api/failederrors/count");
                        FailedErrorsCountReponse failedAuditCountsResponse = result;
                        if (result && failedAuditCountsResponse.Count > 0)
                        {
                            c.FailedImport = true;
                            await this.Post<object>("/api/failederrors/import");
                            c.WasImportedAgain = true;
                        }

                        return false;
                    }

                    return await this.TryGet<FailedMessage>($"/api/errors/{c.UniqueMessageId}") && c.ErrorForwarded;
                })
                .Run();

            Assert.IsTrue(runResult.ErrorForwarded);
            Assert.IsTrue(runResult.MessageFailedEventPublished);
        }

        class MessageFailedHandler : IDomainHandler<MessageFailed>
        {
            public MessageFailedHandler(MyContext scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }
            readonly MyContext scenarioContext;

            public Task Handle(MessageFailed domainEvent)
            {
                scenarioContext.MessageFailedEventPublished = true;
                return Task.CompletedTask;
            }
        }

        class FailOnceEnricher : IEnrichImportedErrorMessages
        {
            readonly MyContext scenarioContext;

            public FailOnceEnricher(MyContext scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public void Enrich(ErrorEnricherContext context)
            {
                if (!scenarioContext.FailedImport)
                {
                    TestContext.WriteLine("Simulating message processing failure");
                    throw new MessageDeserializationException("ID", null);
                }

                TestContext.WriteLine("Message processed correctly");
            }
        }

        public class ErrorLogSpy : EndpointConfigurationBuilder
        {
            public ErrorLogSpy()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly MyContext scenarioContext;

                public MyMessageHandler(MyContext scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    scenarioContext.ErrorForwarded = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
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
                EndpointSetup<DefaultServer>(c =>
                {
                    var recoverability = c.Recoverability();
                    recoverability.Immediate(x => x.NumberOfRetries(0));
                    recoverability.Delayed(x => x.NumberOfRetries(0));
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyMessageHandler(MyContext scenarioContext, ReadOnlySettings settings)
                {
                    this.scenarioContext = scenarioContext;
                    this.settings = settings;
                }
                readonly MyContext scenarioContext;
                readonly ReadOnlySettings settings;

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    scenarioContext.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, settings.EndpointName()).ToString();
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
            public bool WasImportedAgain { get; set; }
            public bool ErrorForwarded { get; set; }
            public bool MessageFailedEventPublished { get; set; }
        }
    }
}