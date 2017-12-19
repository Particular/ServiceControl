namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;

    public class When_a_message_is_duplicated : AcceptanceTest
    {
        [Test]
        public void Duplicates_should_not_be_returned_in_queries()
        {
            var context = new MyContext();
            List<MessagesView> response = null;
            List<MessagesView> terminators;

            Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.Send(new MyMessage());
                    bus.Send(new TerminatorMessage());
                }))
                .WithEndpoint<Receiver>()
                .Done(c =>
                {
                    if (c.MessageId == null || c.TerminatorId == null)
                    {
                        return false;
                    }
                    if (!TryGetMany("/api/messages?include_system_messages=false&sort=id", out terminators, m => m.MessageId == c.TerminatorId) || terminators.Count == 0)
                    {
                        return false;
                    }
                    if (!TryGetMany("/api/messages?include_system_messages=false&sort=id", out response))
                    {
                        return false;
                    }
                    return true;
                })
                .Run(TimeSpan.FromSeconds(40));

            Assert.AreEqual(1, response.Count(m => m.MessageId == context.MessageId));
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithoutAudit>()
                    .AddMapping<MyMessage>(typeof(Receiver))
                    .AddMapping<TerminatorMessage>(typeof(Receiver));
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.Pipeline.Register<DuplicateAuditBehavior.Registration>();
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.MessageId = Bus.CurrentMessageContext.Id;
                }
            }

            public class TerminatorHandler : IHandleMessages<TerminatorMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public void Handle(TerminatorMessage message)
                {
                    Context.TerminatorId = Bus.CurrentMessageContext.Id;
                }
            }

            class DuplicateAuditBehavior : IBehavior<IncomingContext>
            {
                public IAuditMessages MessageAuditer { get; set; }

                public TimeSpan? TimeToBeReceivedOnForwardedMessages { get; set; }

                public void Invoke(IncomingContext context, Action next)
                {
                    next();

                    var sendOptions = new SendOptions(Address.Parse("audit"))
                    {
                        TimeToBeReceived = TimeToBeReceivedOnForwardedMessages
                    };

                    //set audit related headers
                    context.PhysicalMessage.Headers[Headers.ProcessingStarted] = DateTimeExtensions.ToWireFormattedString(context.Get<DateTime>("IncomingMessage.ProcessingStarted"));
                    context.PhysicalMessage.Headers[Headers.ProcessingEnded] = DateTimeExtensions.ToWireFormattedString(context.Get<DateTime>("IncomingMessage.ProcessingEnded"));

                    MessageAuditer.Audit(sendOptions, context.PhysicalMessage);
                }

                public class Registration : RegisterStep
                {
                    public Registration()
                        : base("DuplicateAuditBehavior", typeof(DuplicateAuditBehavior), "Send a copy of the successfully processed message to the configured audit queue")
                    {
                        InsertBefore(WellKnownStep.ProcessingStatistics);
                    }
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
        }

        [Serializable]
        public class TerminatorMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }
            public string TerminatorId { get; set; }
        }
    }
}