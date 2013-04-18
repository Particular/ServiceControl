namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    [TestFixture]
    public class When_a_message_has_been_successfully_processed : HttpUtil
    {
        [Test]
        public void Should_be_imported_and_accessible_via_the_rest_api()
        {
            var context = new MyContext();

            Scenario.Define(() => context)
                .WithEndpoint<ManagementEndpoint>()
                .WithEndpoint<Sender>(b => b.Given(bus =>bus.Send(new MyMessage())))
                .WithEndpoint<Receiver>()
                .Done(c => AuditDataAvailable(context, c))
                .Run();

            Assert.AreEqual(context.MessageId,context.ReturnedMessage.MessageId,"The returned message should match the processed one");
            Assert.AreEqual(MessageStatus.Successful, context.ReturnedMessage.Status, "Status should be set to success");
        }


        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>()
                    .AddMapping<MyMessage>(typeof(Receiver));
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>()
                    .AuditTo(Address.Parse("audit"));
            }


            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.EndpointName = Configure.EndpointName;
                    Context.MessageId = Bus.CurrentMessageContext.Id;
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public Message ReturnedMessage { get; set; }

            public string EndpointName { get; set; }
        }


        bool AuditDataAvailable(MyContext context, MyContext c)
        {
            lock (context)
            {
                if (c.ReturnedMessage != null)
                    return true;

                if (c.MessageId == null)
                    return false;

                c.ReturnedMessage = ApiCall<Message>("/api/messages/" + context.MessageId + "-" + context.EndpointName);


                return true;
            }
        }

    }
}