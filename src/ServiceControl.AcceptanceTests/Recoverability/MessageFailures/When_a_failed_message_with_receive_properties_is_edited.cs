namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;

    class When_a_failed_message_with_receive_properties_is_edited : AcceptanceTest
    {
        const string FileCreatedAtHeader = "ServiceControl.ReceiveProperties.LearningTransport.FileCreatedAt";

        [Test]
        public async Task Should_dispatch_edited_receive_properties()
        {
            //HINT: LearningTransport defines one receive property "LearningTransport.FileCreatedAt"
            //      and sets the file creation time from the dispatch property with the same name
            var editedFileCreatedAt = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);

            var context = await Define<Context>()
                .WithEndpoint<FailingEndpoint>(e => e
                    .DoNotFailOnErrorMessages()
                    .When(c => c.SendLocal(new MyMessage())))
                .Done(async ctx =>
                {
                    if (string.IsNullOrWhiteSpace(ctx.UniqueMessageId))
                    {
                        return false;
                    }

                    if (!ctx.EditedMessage)
                    {
                        var failedMessage = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                        if (!failedMessage.HasResult)
                        {
                            return false;
                        }

                        var newHeaders = failedMessage.Item.ProcessingAttempts.Last().Headers.ToDictionary();
                        ctx.StoredFileCreatedAt = newHeaders.GetValueOrDefault(FileCreatedAtHeader);
                        newHeaders[FileCreatedAtHeader] = editedFileCreatedAt.ToString("O");

                        ctx.EditedMessage = true;
                        await this.Post($"/api/edit/{ctx.UniqueMessageId}", new EditMessageModel
                        {
                            MessageBody = JsonSerializer.Serialize(new MyMessage()),
                            MessageHeaders = newHeaders
                        });
                        return false;
                    }

                    return ctx.EditedMessageHeaders != null;
                })
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.StoredFileCreatedAt, Is.EqualTo(context.FirstAttemptFileCreatedAt), "Receive properties are not stored as headers");
                Assert.That(DateTimeOffset.Parse(context.EditedAttemptFileCreatedAt), Is.EqualTo(editedFileCreatedAt), "Edited receive properties are not dispatched");
                Assert.That(context.EditedMessageHeaders.Keys, Has.None.StartsWith("ServiceControl.ReceiveProperties."), "Receive properties are sent as headers");
            }
        }

        public class FailingEndpoint : EndpointConfigurationBuilder
        {
            public FailingEndpoint() => EndpointSetup<DefaultServerWithoutAudit>(c => c.NoRetries());

            [Handler]
            public class MyMessageHandler(Context testContext, IReadOnlySettings settings) : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    var fileCreatedAt = context.Extensions.Get<IncomingMessage>().ReceiveProperties["LearningTransport.FileCreatedAt"];

                    if (!testContext.EditedMessage)
                    {
                        testContext.FirstAttemptFileCreatedAt = fileCreatedAt;
                        testContext.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId.Replace(@"\", "-"), settings.EndpointName()).ToString();
                        throw new SimulatedException("Simulated Exception");
                    }

                    testContext.EditedAttemptFileCreatedAt = fileCreatedAt;
                    testContext.EditedMessageHeaders = context.MessageHeaders.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    return Task.CompletedTask;
                }
            }
        }

        public class Context : ScenarioContext
        {
            public bool EditedMessage { get; set; }
            public string UniqueMessageId { get; set; }
            public string FirstAttemptFileCreatedAt { get; set; }
            public string StoredFileCreatedAt { get; set; }
            public string EditedAttemptFileCreatedAt { get; set; }
            public Dictionary<string, string> EditedMessageHeaders { get; set; }
        }

        public class MyMessage : ICommand;
    }
}
