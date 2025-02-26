namespace ServiceControl.MultiInstance.AcceptanceTests.Recoverability
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using MessageFailures;
    using MessageFailures.Api;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;
    using TestSupport;

    class WhenRetryingSameMessageMultipleTimes : AcceptanceTest
    {
        [Test]
        public async Task WithNoEdit()
        {
            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    if (c.RetryCount < 3)
                    {
                        var result = await GetFailedMessage(c, ServiceControlInstanceName, FailedMessageStatus.Unresolved);
                        if (result.HasResult)
                        {
                            if (result.Item.ProcessingAttempts.Count == c.RetryCount + 1)
                            {
                                await this.Post<object>($"/api/errors/{result.Item.UniqueMessageId}/retry", null, null,
                                    ServiceControlInstanceName);
                                c.RetryCount++;
                            }
                        }

                        return false;
                    }

                    return await GetFailedMessage(c, ServiceControlInstanceName, FailedMessageStatus.Resolved);
                })
                .Run(TimeSpan.FromMinutes(2));
        }

        [Test]
        public async Task WithEdit()
        {
            CustomServiceControlPrimarySettings = s => { s.AllowMessageEditing = true; };

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(b =>
                    b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    if (c.RetryCount < 3)
                    {
                        var results = await GetAllFailedMessage(ServiceControlInstanceName, FailedMessageStatus.Unresolved);
                        if (!results.HasResult)
                        {
                            return false;
                        }

                        var result = results.Items.Single();

                        c.MessageId = result.MessageId;

                        var failedMessage = await GetFailedMessage(c, ServiceControlInstanceName, FailedMessageStatus.Unresolved);
                        if (!failedMessage.HasResult)
                        {
                            return false;
                        }

                        await this.Post<object>($"/api/edit/{failedMessage.Item.UniqueMessageId}",
                            new
                            {
                                MessageBody = $"{{ \"Name\": \"John{c.RetryCount}\" }}",
                                MessageHeaders = failedMessage.Item.ProcessingAttempts[^1].Headers
                            }, null,
                            ServiceControlInstanceName);
                        c.RetryCount++;

                        return false;
                    }

                    return !(await GetAllFailedMessage(ServiceControlInstanceName, FailedMessageStatus.Unresolved)).HasResult;
                })
                .Run(TimeSpan.FromMinutes(2));
        }

        Task<SingleResult<FailedMessage>> GetFailedMessage(MyContext c, string instance, FailedMessageStatus expectedStatus)
        {
            if (c.MessageId == null)
            {
                return Task.FromResult(SingleResult<FailedMessage>.Empty);
            }

            return this.TryGet<FailedMessage>("/api/errors/" + c.UniqueMessageId, f => f.Status == expectedStatus, instance);
        }

        Task<ManyResult<FailedMessageView>> GetAllFailedMessage(string instance, FailedMessageStatus expectedStatus)
        {
            return this.TryGetMany<FailedMessageView>("/api/errors", f => f.Status == expectedStatus, instance);
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint() => EndpointSetup<DefaultServerWithoutAudit>(c => { c.NoRetries(); });

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly MyContext testContext;
                readonly IReadOnlySettings settings;

                public MyMessageHandler(MyContext testContext, IReadOnlySettings settings)
                {
                    this.testContext = testContext;
                    this.settings = settings;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.MessageId = context.MessageId.Replace(@"\", "-");
                    testContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();
                    Console.Out.WriteLine("Handling message");

                    if (testContext.RetryCount < 3)
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
            public string Name { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string UniqueMessageId => DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();
            public int RetryCount { get; set; }
        }
    }
}