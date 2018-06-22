namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Sagas;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;

    public class When_a_message_hitting_a_saga_is_not_a_start_message : AcceptanceTest
    {
        [Test]
        public async Task Saga_info_should_not_be_available_through_the_http_api()
        {
            var context = await Define<MyContext>()
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.When((bus, c) => bus.SendLocal(new MyMessage{OrderId = 1})))
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
                public Task Handle(MessageInitiatingSaga message, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    MarkAsComplete();
                    return Task.FromResult(0);
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
                {
                    mapper.ConfigureMapping<MessageInitiatingSaga>(s => s.OrderId).ToSaga(d => d.OrderId);
                    mapper.ConfigureMapping<MyMessage>(s => s.OrderId).ToSaga(d => d.OrderId);
                }
            }

            public class MySagaData : ContainSagaData
            {
                public int OrderId { get; set; }
            }

            public class SagaNotFound : IHandleSagaNotFound
            {
                public MyContext Context { get; set; }

                public Task Handle(object message, IMessageProcessingContext context)
                {
                    Context.SagaNotFound = true;
                    return Task.FromResult(0);
                }
            }
        }

        
        public class MessageInitiatingSaga : ICommand
        {
            public int OrderId { get; set; }
        }

        
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