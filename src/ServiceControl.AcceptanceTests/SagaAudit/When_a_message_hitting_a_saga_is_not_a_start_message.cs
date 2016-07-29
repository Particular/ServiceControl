namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Saga;
    using NUnit.Framework;
  
    public class When_a_message_hitting_a_saga_is_not_a_start_message : AcceptanceTest
    {
        [Test]
        public void Saga_info_should_not_be_available_through_the_http_api()
        {
            var context = new MyContext();
           
            Define(context)
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.Given((bus, c) => bus.SendLocal(new MyMessage{OrderId = 1})))
                .Done(c => c.SagaNotFound)
                .Run(TimeSpan.FromSeconds(40));

            Assert.IsTrue(context.SagaNotFound);
        }

        public class EndpointThatIsHostingTheSaga : EndpointConfigurationBuilder
        {
            public EndpointThatIsHostingTheSaga()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }

            public class MySaga : Saga<MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>,
                IHandleMessages<MyMessage>
            {
                public void Handle(MessageInitiatingSaga message)
                {
                }

                public void Handle(MyMessage message)
                {
                    MarkAsComplete();
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
                {
                    mapper.ConfigureMapping<MessageInitiatingSaga>(s => s.OrderId).ToSaga(d => d.OrderId);
                    mapper.ConfigureMapping<MyMessage>(s => s.OrderId).ToSaga(d => d.OrderId);
                }
            }

            public class MySagaData : ContainSagaData
            {
                [Unique]
                public int OrderId { get; set; }
            }

            public class SagaNotFound : IHandleSagaNotFound
            {
                public MyContext Context { get; set; }

                public void Handle(object message)
                {
                    Context.SagaNotFound = true;
                }
            }
        }

        [Serializable]
        public class MessageInitiatingSaga : ICommand
        {
            public int OrderId { get; set; }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
            public int OrderId { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public bool SagaNotFound { get; set; }
        }
    }
}