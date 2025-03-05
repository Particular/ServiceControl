namespace ServiceControl.MultiInstance.AcceptanceTests.Recoverability
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using MessageFailures;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;
    using TestSupport;

    class WhenRetryingSameMessageMultipleTimes : WhenRetrying
    {
        public enum RetryType
        {
            NoEdit,
            Edit
        }

        [TestCase(new[] { RetryType.NoEdit, RetryType.NoEdit, RetryType.Edit })]
        [TestCase(new[] { RetryType.Edit, RetryType.NoEdit, RetryType.Edit, RetryType.NoEdit })]
        //[TestCase(new[] { RetryType.NoEdit, RetryType.Edit, RetryType.NoEdit, RetryType.Edit })]
        [TestCase(new[] { RetryType.Edit, RetryType.Edit, RetryType.NoEdit })]
        public async Task WithMixOfRetryTypes(RetryType[] retryTypes)
        {
            CustomServiceControlPrimarySettings = s => { s.AllowMessageEditing = true; };

            await Define<MyContext>(context => context.NumberOfRetriesToComplete = retryTypes.Length)
                .WithEndpoint<FailureEndpoint>(b =>
                    b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    if (c.ExceptionsRetried >= c.NumberOfRetriesToComplete) // Are all retries done?
                    {
                        return !(await GetAllFailedMessage(ServiceControlInstanceName, FailedMessageStatus.Unresolved))
                            .HasResult; // Should return true if all failed messages are no longer unresolved
                    }

                    if (c.ExceptionsRetried >= c.ExceptionsThrown)
                    {
                        return false;
                    }

                    var results = await GetAllFailedMessage(ServiceControlInstanceName, FailedMessageStatus.Unresolved);
                    if (!results.HasResult)
                    {
                        return false; // No failed messages yet
                    }

                    var mostRecentFailure = results.Items.Single();

                    var failedMessage = await GetFailedMessage(mostRecentFailure.Id, ServiceControlInstanceName, FailedMessageStatus.Unresolved);
                    if (!failedMessage.HasResult)
                    {
                        return false; // No failed message yet
                    }

                    if (retryTypes[c.ExceptionsThrown - 1] == RetryType.Edit)
                    {
                        await this.Post<object>($"/api/edit/{failedMessage.Item.UniqueMessageId}",
                            new
                            {
                                MessageBody = $"{{ \"Name\": \"Hello {c.ExceptionsThrown}\" }}",
                                MessageHeaders = failedMessage.Item.ProcessingAttempts[^1].Headers
                            }, null,
                            ServiceControlInstanceName);
                    }
                    else
                    {
                        await this.Post<object>($"/api/errors/{failedMessage.Item.UniqueMessageId}/retry", null, null,
                            ServiceControlInstanceName);
                    }
                    c.ExceptionsRetried++;

                    return false;

                })
                .Run(TimeSpan.FromSeconds(15));
        }

        class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint() => EndpointSetup<DefaultServerWithoutAudit>(c => { c.NoRetries(); });

            public class MyMessageHandler(MyContext testContext, IReadOnlySettings settings)
                : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.MessageId = context.MessageId.Replace(@"\", "-");
                    testContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();

                    if (testContext.ExceptionsThrown < testContext.NumberOfRetriesToComplete)
                    {
                        testContext.ExceptionsThrown++;
                        Console.Out.WriteLine("Throwing exception");
                        throw new Exception("Simulated exception");
                    }

                    Console.Out.WriteLine("Handling message");

                    return Task.CompletedTask;
                }
            }
        }

        class MyMessage : ICommand
        {
            public string Name { get; set; }
        }

        class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string UniqueMessageId => DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();
            public int ExceptionsThrown { get; set; }
            public int ExceptionsRetried { get; set; }
            public int NumberOfRetriesToComplete { get; set; }
        }
    }
}