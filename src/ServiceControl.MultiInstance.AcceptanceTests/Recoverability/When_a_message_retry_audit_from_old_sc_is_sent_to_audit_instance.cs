namespace ServiceControl.MultiInstance.AcceptanceTests.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using MessageFailures;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;
    using TestSupport;


    class When_a_message_retry_audit_from_old_sc_is_sent_to_audit_instance : AcceptanceTest
    {
        [Test]
        [CancelAfter(120_000)]
        public async Task Should_mark_as_resolved(CancellationToken cancellationToken)
        {
            FailedMessage failure;

            await Define<MyContext>()
                .WithEndpoint<Failing>(b => b.When(session => session.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    // Give the other parts of the system a chance to work
                    await Task.Delay(500);

                    var result = await GetFailedMessage(c);
                    failure = result;
                    if (!result)
                    {
                        return false;
                    }

                    if (failure.Status == FailedMessageStatus.Unresolved)
                    {
                        await IssueRetry(c, () => this.Post<object>($"/api/errors/{failure.UniqueMessageId}/retry", null, null, ServiceControlInstanceName));
                        return false;
                    }

                    return failure.Status == FailedMessageStatus.Resolved;
                })
                .Run(cancellationToken);
        }

        Task<SingleResult<FailedMessage>> GetFailedMessage(MyContext c)
        {
            if (c.MessageId == null)
            {
                return Task.FromResult(SingleResult<FailedMessage>.Empty);
            }

            return this.TryGet<FailedMessage>("/api/errors/" + c.UniqueMessageId, msg => true, ServiceControlInstanceName);
        }

        async Task IssueRetry(MyContext c, Func<Task> retryAction)
        {
            if (!c.RetryIssued)
            {
                c.RetryIssued = true;
                await retryAction();
            }
        }

        public class Failing : EndpointConfigurationBuilder
        {
            public Failing() =>
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.NoRetries();
                    c.Pipeline.Register(new SimulateOldServiceControlBehavior(), "Simulates old SC behavior");
                });

            public class SimulateOldServiceControlBehavior : Behavior<ITransportReceiveContext>
            {
                public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
                {
                    context.Message.Headers.Remove("ServiceControl.Retry.AcknowledgementQueue");
                    return next();
                }
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly MyContext testContext;
                readonly IReadOnlySettings settings;
                readonly ReceiveAddresses receiveAddresses;

                public MyMessageHandler(MyContext testContext, IReadOnlySettings settings, ReceiveAddresses receiveAddresses)
                {
                    this.testContext = testContext;
                    this.settings = settings;
                    this.receiveAddresses = receiveAddresses;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Console.Out.WriteLine("Handling message");
                    testContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();
                    testContext.LocalAddress = receiveAddresses.MainReceiveAddress;
                    testContext.MessageId = context.MessageId.Replace(@"\", "-");

                    if (!testContext.RetryIssued) //simulate that the exception will be resolved with the retry
                    {
                        Console.Out.WriteLine("Throwing exception for MyMessage");
                        throw new Exception("Simulated exception");
                    }

                    return Task.CompletedTask;
                }
            }
        }


        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string UniqueMessageId => DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();
            public string LocalAddress { get; set; }
            public bool RetryIssued { get; set; }
        }
    }
}