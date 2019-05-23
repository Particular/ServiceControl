﻿namespace ServiceControl.Audit.AcceptanceTests.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using Audit.Auditing.MessagesView;
    using Audit.Monitoring;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;

    class When_importing_a_message_resolved_by_a_retry : AcceptanceTest
    {
        [Test]
        public async Task Should_set_status_to_resolved()
        {
            MessagesView auditedMessage = null;

            var messageId = Guid.NewGuid().ToString();
            await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(s =>
                {
                    var options = new SendOptions();

                    options.SetHeader("ServiceControl.Retry.UniqueMessageId", Guid.NewGuid().ToString());
                    options.RouteToThisEndpoint();
                    options.SetMessageId(messageId);
                    return s.Send(new MyMessage(), options);
                }))
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<MessagesView>("/api/messages", m => m.MessageId == messageId);

                    auditedMessage = result;

                    return result;
                })
                .Run();

            Assert.AreEqual(MessageStatus.ResolvedSuccessfully, auditedMessage.Status);
        }

        public class DomainEventSpy : IDomainHandler<MessageFailureResolvedByRetry>
        {
            public MyContext TestContext { get; set; }

            public Task Handle(MessageFailureResolvedByRetry domainEvent)
            {
                TestContext.EventRaised = domainEvent;
                return Task.CompletedTask;
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
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public MessageFailureResolvedByRetry EventRaised { get; internal set; }
        }
    }
}