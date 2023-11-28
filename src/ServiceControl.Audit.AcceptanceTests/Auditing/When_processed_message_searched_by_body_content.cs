﻿namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Audit.Auditing.MessagesView;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NUnit.Framework;
    using TestSupport.EndpointTemplates;

    class When_processed_message_searched_by_body_content : AcceptanceTest
    {
        [Test]
        public async Task Should_be_found_when_fulltext_search_enabled()
        {
            // setting it even if it is the default
            SetSettings = settings => settings.EnableFullTextSearchOnBodies = true;

            var searchString = "forty-two";

            var context = await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage
                {
                    Something = "Somewhere in the body is the answer to all of the questions. forty-two"
                })))
                .WithEndpoint<Receiver>()
                .Done(async c =>
                {
                    if (c.MessageId != null && await this.TryGetMany<MessagesView>($"/api/messages/search/{c.MessageId}"))
                    {
                        c.MessageIngested = true;
                    }

                    if (!c.MessageIngested)
                    {
                        return false;
                    }
                    c.MessageFound = await this.TryGetMany<MessagesView>($"/api/messages/search/{searchString}");
                    return true;
                })
                .Run();

            Assert.IsTrue(context.MessageFound);
        }

        [Test]
        public async Task Should_not_be_found_when_fulltext_search_disabled()
        {
            SetSettings = settings => settings.EnableFullTextSearchOnBodies = false;

            var searchString = "forty-two";

            var context = await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage
                {
                    Something = "Somewhere in the body is the answer to all of the questions. forty-two"
                })))
                .WithEndpoint<Receiver>()
                .Done(async c =>
                {
                    if (c.MessageId != null && await this.TryGetMany<MessagesView>($"/api/messages/search/{c.MessageId}"))
                    {
                        c.MessageIngested = true;
                    }

                    if (!c.MessageIngested)
                    {
                        return false;
                    }

                    c.MessageFound = await this.TryGetMany<MessagesView>($"/api/messages/search/{searchString}");
                    return true;
                })
                .Run();

            Assert.IsTrue(context.MessageIngested);
            Assert.IsFalse(context.MessageFound);
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
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
                public MyContext Context { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.MessageId = context.MessageId;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyMessage : ICommand
        {
            public string Something { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public bool MessageIngested { get; set; }

            public bool MessageFound { get; set; }
        }
    }
}