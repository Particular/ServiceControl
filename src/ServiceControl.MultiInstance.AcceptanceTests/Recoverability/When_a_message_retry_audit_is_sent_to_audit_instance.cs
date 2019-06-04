namespace ServiceBus.Management.AcceptanceTests.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    class When_a_message_retry_audit_is_sent_to_audit_instance : AcceptanceTest
    {
        [Test]
        public async Task Should_mark_as_resolved()
        {
            CustomAuditEndpointConfiguration = ConfigureWaitingForMasterToSubscribe;

            FailedMessage failure;

            await Define<MyContext>()
                .WithEndpoint<Failing>(b => b.When(c => c.HasNativePubSubSupport || c.ServiceControlSubscribed,
                    bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
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
                .Run(TimeSpan.FromMinutes(2));
        }

        static void ConfigureWaitingForMasterToSubscribe(EndpointConfiguration config)
        {
            config.OnEndpointSubscribed<MyContext>((s, ctx) =>
            {
                if (s.SubscriberReturnAddress.IndexOf(ServiceControlInstanceName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ctx.ServiceControlSubscribed = true;
                }
            });
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
            public Failing()
            {
                EndpointSetup<DefaultServerWithAudit>(c => { c.NoRetries(); });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Console.Out.WriteLine("Handling message");
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.LocalAddress = Settings.LocalAddress();
                    Context.MessageId = context.MessageId.Replace(@"\", "-");

                    if (!Context.RetryIssued) //simulate that the exception will be resolved with the retry
                    {
                        Console.Out.WriteLine("Throwing exception for MyMessage");
                        throw new Exception("Simulated exception");
                    }

                    return Task.FromResult(0);
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
            public bool ServiceControlSubscribed { get; set; }
        }
    }
}