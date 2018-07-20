namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using EndpointTemplates;
    using Infrastructure.Settings;
    using ServiceControl.CompositeViews.Messages;

    public class When_a_message_that_is_handled_by_a_saga : AcceptanceTest
    {
        [Test]
        public async Task Message_should_be_enriched_with_saga_state_changes()
        {
            var messages = new List<MessagesView>();

            var context = await Define<MyContext>()
                .WithEndpoint<EndpointThatIsHostingSagas>(b => b.When((bus, c) => bus.SendLocal(new InitiateSaga { Saga1Id = Guid.NewGuid(), Saga2Id = Guid.NewGuid() })))
                .Done(async c =>
                {
                    if (c.Saga1Complete && c.Saga2Complete)
                    {
                        var result = await this.TryGetMany<MessagesView>("/api/messages");
                        messages = result;
                        if (result)
                        {
                            return messages.Count == 5;
                        }
                    }

                    return false;
                })
                .Run(TimeSpan.FromSeconds(40));

            Assert.AreEqual(5, messages.Count);

            AssertStateChange<UpdateSaga1>(messages, context.Saga1Id, "Updated");
            AssertStateChange<UpdateSaga2>(messages, context.Saga2Id, "Updated");

            AssertStateChange<CompleteSaga1>(messages, context.Saga1Id, "Completed");
            AssertStateChange<CompleteSaga2>(messages, context.Saga2Id, "Completed");

            AssertInitiatedHas2Sagas(messages, context);
        }

        // ReSharper disable once UnusedParameter.Local
        static void AssertInitiatedHas2Sagas(IEnumerable<MessagesView> messages, MyContext context)
        {
            var m = messages.First(message => message.MessageType == typeof(InitiateSaga).FullName);
            var value = (string)m.Headers.First(kv => kv.Key == "ServiceControl.SagaStateChange").Value;
            var strings = value.Split(';');

            Assert.IsTrue(strings.Any(s => s == context.Saga1Id + ":New"));
            Assert.IsTrue(strings.Any(s => s == context.Saga2Id + ":New"));
        }

        void AssertStateChange<T>(IEnumerable<MessagesView> messages, Guid sagaId, string stateChange)
        {
            var m = messages.First(message => message.MessageType == typeof(T).FullName);
            Assert.AreEqual($"{sagaId}:{stateChange}", m.Headers.First(kv => kv.Key == "ServiceControl.SagaStateChange").Value);
        }

        public class EndpointThatIsHostingSagas : EndpointConfigurationBuilder
        {
            public EndpointThatIsHostingSagas()
            {
                EndpointSetup<DefaultServerWithAudit>(c => c.AuditSagaStateChanges(Settings.DEFAULT_SERVICE_NAME));
            }

            public class Saga1 : Saga<Saga1.Saga1Data>, IAmStartedByMessages<InitiateSaga>, IHandleMessages<UpdateSaga1>, IHandleMessages<CompleteSaga1>
            {
                public MyContext Context { get; set; }

                //static Guid myId = Guid.NewGuid();

                public Task Handle(InitiateSaga message, IMessageHandlerContext context)
                {
                    //Data.MyId = myId;
                    return context.SendLocal(new UpdateSaga1 { MyId = message.Saga1Id });
                }

                public Task Handle(UpdateSaga1 message, IMessageHandlerContext context)
                {
                    return context.SendLocal(new CompleteSaga1 { MyId = message.MyId });
                }

                public Task Handle(CompleteSaga1 message, IMessageHandlerContext context)
                {
                    MarkAsComplete();
                    Context.Saga1Id = Data.Id;
                    Context.Saga1Complete = true;
                    return Task.FromResult(0);
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<Saga1Data> mapper)
                {
                    mapper.ConfigureMapping<InitiateSaga>(d => d.Saga1Id).ToSaga(s => s.MyId);
                    mapper.ConfigureMapping<UpdateSaga1>(d => d.MyId).ToSaga(s => s.MyId);
                    mapper.ConfigureMapping<CompleteSaga1>(d => d.MyId).ToSaga(s => s.MyId);
                }

                public class Saga1Data : ContainSagaData
                {
                    public Guid MyId { get; set; }
                }
            }

            public class Saga2 : Saga<Saga2.Saga2Data>, IAmStartedByMessages<InitiateSaga>, IHandleMessages<UpdateSaga2>, IHandleMessages<CompleteSaga2>
            {
                public MyContext Context { get; set; }

                //static Guid myId = Guid.NewGuid();

                public Task Handle(InitiateSaga message, IMessageHandlerContext context)
                {
                    //Data.MyId = myId;
                    return context.SendLocal(new UpdateSaga2 { MyId = message.Saga2Id });
                }

                public Task Handle(UpdateSaga2 message, IMessageHandlerContext context)
                {
                    return context.SendLocal(new CompleteSaga2 { MyId = message.MyId });
                }

                public Task Handle(CompleteSaga2 message, IMessageHandlerContext context)
                {
                    MarkAsComplete();
                    Context.Saga2Id = Data.Id;
                    Context.Saga2Complete = true;
                    return Task.FromResult(0);
                }

                public class Saga2Data : ContainSagaData
                {
                    public Guid MyId { get; set; }
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<Saga2Data> mapper)
                {
                    mapper.ConfigureMapping<InitiateSaga>(d => d.Saga2Id).ToSaga(s => s.MyId);
                    mapper.ConfigureMapping<UpdateSaga2>(d => d.MyId).ToSaga(s => s.MyId);
                    mapper.ConfigureMapping<CompleteSaga2>(d => d.MyId).ToSaga(s => s.MyId);
                }
            }
        }

        
        public class InitiateSaga : ICommand
        {
            public Guid Saga1Id { get; set; }
            public Guid Saga2Id { get; set; }
        }

        
        public class UpdateSaga1 : ICommand
        {
            public Guid MyId { get; set; }
        }

        
        public class CompleteSaga1 : ICommand
        {
            public Guid MyId { get; set; }
        }

        
        public class UpdateSaga2 : ICommand
        {
            public Guid MyId { get; set; }
        }

        
        public class CompleteSaga2 : ICommand
        {
            public Guid MyId { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public bool Saga1Complete { get; set; }
            public bool Saga2Complete { get; set; }
            public Guid Saga1Id { get; set; }
            public Guid Saga2Id { get; set; }
        }
    }
}