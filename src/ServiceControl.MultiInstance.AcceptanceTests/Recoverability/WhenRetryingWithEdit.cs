namespace ServiceControl.MultiInstance.AcceptanceTests.Recoverability;

using System;
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

class WhenRetryingWithEdit : WhenRetrying
{
    [Test]
    public async Task ShouldCreateNewMessageAndResolveEditedMessage()
    {
        CustomServiceControlPrimarySettings = s => { s.AllowMessageEditing = true; };

        await Define<MyContext>()
            .WithEndpoint<FailureEndpoint>(b =>
                b.When(bus => bus.SendLocal(new MyMessage { Password = "Bad password!" })).DoNotFailOnErrorMessages())
            .Done(async c =>
            {
                if (!c.ErrorRetried)
                {
                    var failedMessage = await GetFailedMessage(c.UniqueMessageId, ServiceControlInstanceName,
                        FailedMessageStatus.Unresolved);
                    if (!failedMessage.HasResult)
                    {
                        return false; // No failed message yet
                    }

                    await this.Post<object>($"/api/edit/{failedMessage.Item.UniqueMessageId}",
                        new
                        {
                            MessageBody = "{ \"Password\": \"VerySecretPassword\" }",
                            MessageHeaders = failedMessage.Item.ProcessingAttempts[^1].Headers
                        }, null,
                        ServiceControlInstanceName);
                    c.ErrorRetried = true;

                    return false;
                }

                var failedResolvedMessage = await GetFailedMessage(c.UniqueMessageId, ServiceControlInstanceName, FailedMessageStatus.Resolved);

                return failedResolvedMessage.HasResult; // If there is a result it means the message was resolved
            })
            .Run(TimeSpan.FromSeconds(30));
    }

    class FailureEndpoint : EndpointConfigurationBuilder
    {
        public FailureEndpoint() => EndpointSetup<DefaultServerWithoutAudit>(c => { c.NoRetries(); });

        public class MyMessageHandler(MyContext testContext, IReadOnlySettings settings) : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                if (message.Password == "VerySecretPassword")
                {
                    Console.Out.WriteLine("Handling message");
                    return Task.CompletedTask;
                }

                testContext.MessageId = context.MessageId.Replace(@"\", "-");
                testContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();

                Console.Out.WriteLine("Throwing exception");
                throw new Exception("Simulated exception");
            }
        }
    }

    class MyMessage : ICommand
    {
        public string Password { get; set; }
    }

    class MyContext : ScenarioContext
    {
        public string MessageId { get; set; }

        public string EndpointNameOfReceivingEndpoint { get; set; }

        public string UniqueMessageId => DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();
        public bool ErrorRetried { get; set; }
    }
}