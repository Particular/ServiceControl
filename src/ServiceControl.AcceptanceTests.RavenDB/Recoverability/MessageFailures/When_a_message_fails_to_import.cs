namespace ServiceControl.AcceptanceTests.RavenDB.Recoverability.MessageFailures
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Contracts.MessageFailures;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using Operations;
    using ServiceControl.MessageFailures;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_message_fails_to_import : AcceptanceTest
    {
        [Test]
        public async Task It_can_be_reimported()
        {
            CustomizeHostBuilder = hostBuilder =>
            {
                //Make sure the error import attempt fails
                hostBuilder.Services.AddSingleton<IEnrichImportedErrorMessages, FailOnceEnricher>();

                //Register domain event spy
                hostBuilder.Services.AddSingleton<IDomainHandler<MessageFailed>, MessageFailedHandler>();
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

            Assert.Multiple(() =>
            {
                Assert.That(runResult.ErrorForwarded, Is.True);
                Assert.That(runResult.MessageFailedEventPublished, Is.True);
            });
        }

        class MessageFailedHandler(MyContext scenarioContext) : IDomainHandler<MessageFailed>
        {
            public Task Handle(MessageFailed domainEvent, CancellationToken cancellationToken)
            {
                scenarioContext.MessageFailedEventPublished = true;
                return Task.CompletedTask;
            }
        }

        class FailOnceEnricher(MyContext scenarioContext) : IEnrichImportedErrorMessages
        {
            public void Enrich(ErrorEnricherContext context)
            {
                if (!scenarioContext.FailedImport)
                {
                    TestContext.Out.WriteLine("Simulating message processing failure");
                    throw new MessageDeserializationException("ID", null);
                }

                TestContext.Out.WriteLine("Message processed correctly");
            }
        }

        public class ErrorLogSpy : EndpointConfigurationBuilder
        {
            public ErrorLogSpy() => EndpointSetup<DefaultServerWithoutAudit>();

            public class MyMessageHandler(MyContext scenarioContext) : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    scenarioContext.ErrorForwarded = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var recoverability = c.Recoverability();
                    recoverability.Immediate(x => x.NumberOfRetries(0));
                    recoverability.Delayed(x => x.NumberOfRetries(0));
                });

            public class MyMessageHandler(MyContext scenarioContext, IReadOnlySettings settings)
                : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    scenarioContext.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, settings.EndpointName()).ToString();
                    throw new Exception("Simulated");
                }
            }
        }

        public class MyMessage : ICommand;

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