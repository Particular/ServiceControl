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
                .WithEndpoint<Sender>()
                .WithEndpoint<Receiver>()
                .Done(c =>
                    {
                        lock (context)
                        {
                            if (c.ApiData != null)
                                return true;

                            if (c.MessageId == null)
                                return false;

                            c.ApiData = ApiCall<Message>("/messages/" + context.MessageId);


                            return true;
                        }
                    })
                .Run();

            Assert.AreEqual(context.MessageId,context.ApiData.Id,"The returned message should match the processed one");
        }

        public class Sender : EndpointBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>()
                    .AddMapping<MyMessage>(typeof(Receiver))
                    .When(bus => bus.Send(new MyMessage()));
            }
        }

        public class Receiver : EndpointBuilder
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
            public Message ApiData { get; set; }
        }
    }
}