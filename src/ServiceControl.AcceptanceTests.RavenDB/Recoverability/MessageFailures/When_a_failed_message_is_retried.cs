namespace ServiceControl.AcceptanceTests.RavenDB.Recoverability.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.Recoverability;
    using TestSupport;

    class When_a_failed_message_is_retried : AcceptanceTest
    {
        [Test]
        public async Task Should_remove_failedmessageretries_when_retrying_groups()
        {
            FailedMessageRetriesCountReponse failedMessageRetries = null;

            await Define<Context>()
                .WithEndpoint<FailingEndpoint>(b => b.When(async ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return false;
                    }

                    FailedMessage failedMessage = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    if (failedMessage == null)
                    {
                        return false;
                    }

                    ctx.FailureGroupId = failedMessage.FailureGroups.First().Id;

                    return true;
                }, async (bus, ctx) =>
                {
                    ctx.AboutToSendRetry = true;
                    await this.Post<object>($"/api/recoverability/groups/{ctx.FailureGroupId}/errors/retry");
                }).DoNotFailOnErrorMessages())
                .Done(async ctx =>
                {
                    if (ctx.Retried)
                    {
                        failedMessageRetries = await this.TryGet<FailedMessageRetriesCountReponse>("/api/failedmessageretries/count");

                        return failedMessageRetries.Count == 0;
                    }

                    return false;
                })
                .Run();

            Assert.That(failedMessageRetries.Count, Is.EqualTo(0), "FailedMessageRetries not removed");
        }

        [Test]
        public async Task Should_remove_failedmessageretries_when_retrying_individual_messages()
        {
            FailedMessageRetriesCountReponse failedMessageRetries = null;

            await Define<Context>()
                .WithEndpoint<FailingEndpoint>(b => b.When(async ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return false;
                    }

                    FailedMessage failedMessage = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    if (failedMessage == null)
                    {
                        return false;
                    }

                    return true;
                }, async (bus, ctx) =>
                {
                    ctx.AboutToSendRetry = true;
                    await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                }).DoNotFailOnErrorMessages())
                .Done(async ctx =>
                {
                    if (ctx.Retried)
                    {
                        failedMessageRetries = await this.TryGet<FailedMessageRetriesCountReponse>("/api/failedmessageretries/count");

                        return failedMessageRetries.Count == 0;
                    }

                    return false;
                })
                .Run();

            Assert.That(failedMessageRetries.Count, Is.EqualTo(0), "FailedMessageRetries not removed");
        }

        [Test]
        public async Task Should_remove_UnacknowledgedOperation_when_retrying_individual_messages()
        {
            RetryHistory retryHistory = null;

            await Define<Context>()
                .WithEndpoint<FailingEndpoint>(b => b.When(async ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return false;
                    }

                    FailedMessage failedMessage = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    if (failedMessage == null)
                    {
                        return false;
                    }

                    return true;
                }, async (bus, ctx) =>
                {
                    ctx.AboutToSendRetry = true;
                    await this.Post<object>("/api/errors/retry", new List<string> { ctx.UniqueMessageId });
                }).DoNotFailOnErrorMessages())
                .Done(async ctx =>
                {
                    if (ctx.Retried)
                    {
                        retryHistory = await this.TryGet<RetryHistory>("/api/recoverability/history");

                        return !retryHistory.UnacknowledgedOperations.Any() && retryHistory.HistoricOperations.Any();
                    }

                    return false;
                })
                .Run();

            Assert.That(retryHistory.UnacknowledgedOperations, Is.Empty, "Unucknowledged retry operation not removed");
        }

        [Test]
        public async Task Should_remove_failedmessageretries_after_expiration_process_passes()
        {
            FailedMessageRetriesCountReponse failedMessageRetries = null;

            await Define<Context>()
                .WithEndpoint<FailingEndpointWithoutAudit>(b => b.When(async ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return false;
                    }

                    FailedMessage failedMessage = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    if (failedMessage == null)
                    {
                        return false;
                    }
                    ctx.FailureGroupId = failedMessage.FailureGroups.First().Id;

                    return true;
                }, async (bus, ctx) =>
                {
                    ctx.AboutToSendRetry = true;
                    await this.Post<object>($"/api/recoverability/groups/{ctx.FailureGroupId}/errors/retry");
                })
                .DoNotFailOnErrorMessages())
                .Done(async ctx =>
                {
                    if (ctx.Retried)
                    {
                        // Note: In RavenDB 3.5 there was a call to /api/failederrors/forcecleanerrors implemented in test-only FailedErrorsController
                        // that manually ran the Expiration bundle, but RavenDB 5 uses built-in expiration so you can't do that. The test still
                        // appears to pass, however.
                        failedMessageRetries = await this.TryGet<FailedMessageRetriesCountReponse>("/api/failedmessageretries/count");

                        return failedMessageRetries.Count == 0;
                    }

                    return false;
                })
                .Run();

            Assert.That(failedMessageRetries.Count, Is.EqualTo(0), "FailedMessageRetries not removed");
        }

        public class FailingEndpoint : EndpointConfigurationBuilder
        {
            public FailingEndpoint() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.GetSettings().Get<TransportDefinition>().TransportTransactionMode =
                        TransportTransactionMode.ReceiveOnly;
                    c.EnableFeature<Outbox>();

                    c.RegisterStartupTask(new SendMessageAtStart());

                    c.ReportSuccessfulRetriesToServiceControl();

                    c.NoRetries();
                });

            class SendMessageAtStart : FeatureStartupTask
            {
                protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
                    => session.SendLocal(new MyMessage(), cancellationToken);

                protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
                    => Task.CompletedTask;
            }

            public class MyMessageHandler(Context scenarioContext, IReadOnlySettings settings)
                : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Console.WriteLine("Message Handled");
                    if (scenarioContext.AboutToSendRetry)
                    {
                        scenarioContext.Retried = true;
                    }
                    else
                    {
                        scenarioContext.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, settings.EndpointName()).ToString();
                        throw new Exception("Simulated Exception");
                    }

                    return Task.CompletedTask;
                }
            }
        }

        public class FailingEndpointWithoutAudit : EndpointConfigurationBuilder
        {
            public FailingEndpointWithoutAudit() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.GetSettings().Get<TransportDefinition>().TransportTransactionMode =
                        TransportTransactionMode.ReceiveOnly;
                    c.EnableFeature<Outbox>();

                    c.RegisterStartupTask(new SendMessageAtStart());

                    c.NoRetries();
                });

            class SendMessageAtStart : FeatureStartupTask
            {
                protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default) => session.SendLocal(new MyMessage(), cancellationToken);

                protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;
            }

            public class MyMessageHandler(Context scenarioContext, IReadOnlySettings settings)
                : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Console.WriteLine("Message Handled");
                    if (scenarioContext.AboutToSendRetry)
                    {
                        scenarioContext.Retried = true;
                    }
                    else
                    {
                        scenarioContext.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, settings.EndpointName()).ToString();
                        throw new Exception("Simulated Exception");
                    }

                    return Task.CompletedTask;
                }
            }
        }

        public class Context : ScenarioContext
        {
            public string UniqueMessageId { get; set; }
            public string FailureGroupId { get; set; }
            public bool Retried { get; set; }
            public bool AboutToSendRetry { get; set; }
        }

        public class MyMessage : ICommand;
    }
}