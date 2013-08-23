namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;

    public class When_a_message_has_failed : AcceptanceTest
    {
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
                EndpointSetup<DefaultServer>(c => Configure.Features.Disable<SecondLevelRetries>())
                    .AuditTo(Address.Parse("audit"));
            }


            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.EndpointNameOfReceivingEndpoint = Configure.EndpointName;
                    Context.MessageId = Bus.CurrentMessageContext.Id;
                    throw new Exception("Simulated exception");
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
            public Message Message { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }

        bool AuditDataAvailable(MyContext context, MyContext c)
        {
            lock (context)
            {
                if (c.Message != null)
                {
                    return true;
                }

                if (c.MessageId == null)
                {
                    return false;
                }

                var message =
                    Get<Message>("/api/messages/" + context.MessageId + "-" + context.EndpointNameOfReceivingEndpoint);

                if (message == null)
                {
                    return false;
                }

                c.Message = message;

                return true;
            }
        }

        [Test]
        public void Should_be_imported_and_accessible_via_the_rest_api()
        {
            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>()
                .WithEndpoint<Sender>(b => b.Given(bus => bus.Send(new MyMessage())))
                .WithEndpoint<Receiver>()
                .Done(c => AuditDataAvailable(context, c))
                .Run();

            Assert.AreEqual(context.MessageId, context.Message.MessageId,
                "The returned message should match the processed one");
            Assert.AreEqual(MessageStatus.Failed, context.Message.Status, "Status should be set to failed");
            Assert.AreEqual(1, context.Message.FailureDetails.NumberOfTimesFailed, "Failed count should be 1");
            Assert.AreEqual("Simulated exception", context.Message.FailureDetails.Exception.Message,
                "Exception message should be captured");
        }
    }
}