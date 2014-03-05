namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Saga;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;
  
    public class When_a_message_that_is_handled_by_a_saga : AcceptanceTest
    {
        [Test]
        public void Message_should_be_enriched_with_saga_state_changes()
        {
            var context = new MyContext();
            var messages = new List<MessagesView>();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<EndpointThatIsHostingSagas>(b => b.Given((bus, c) => bus.SendLocal(new InitiateSaga())))
                .Done(c =>
                {
                    if (c.Saga1Complete && c.Saga2Complete)
                    {
                        if (TryGetMany("/api/messages", out messages))
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

        static void AssertInitiatedHas2Sagas(IEnumerable<MessagesView> messages, MyContext context)
        {
            var m = messages.First(message => message.MessageType == typeof(InitiateSaga).FullName);
            var value = (string) m.Headers.First(kv => kv.Key == "ServiceControl.SagaStateChange").Value;
            var strings = value.Split(';');

            Assert.IsTrue(strings.Any(s => s == context.Saga1Id + ":New"));
            Assert.IsTrue(strings.Any(s => s == context.Saga2Id + ":New"));
        }

        void AssertStateChange<T>(IEnumerable<MessagesView> messages, Guid sagaId, string stateChange)
        {
            var m = messages.First(message => message.MessageType == typeof(T).FullName);
            Assert.AreEqual(string.Format("{0}:{1}", sagaId, stateChange), m.Headers.First(kv => kv.Key == "ServiceControl.SagaStateChange").Value);
        }

        public class EndpointThatIsHostingSagas : EndpointConfigurationBuilder
        {
            public EndpointThatIsHostingSagas()
            {
                EndpointSetup<DefaultServer>()
                    .AuditTo(Address.Parse("audit"));
            }

            public class Saga1 : Saga<Saga1.Saga1Data>, IAmStartedByMessages<InitiateSaga>, IHandleMessages<UpdateSaga1>, IHandleMessages<CompleteSaga1>
            {
                public MyContext Context { get; set; }

                static Guid myId = Guid.NewGuid();

                public void Handle(InitiateSaga message)
                {
                    Data.MyId = myId;
                    Bus.SendLocal(new UpdateSaga1 { MyId = myId });
                }

                public void Handle(UpdateSaga1 message)
                {
                    Bus.SendLocal(new CompleteSaga1 { MyId = myId });
                }

                public void Handle(CompleteSaga1 message)
                {
                    MarkAsComplete();
                    Context.Saga1Id = Data.Id; 
                    Context.Saga1Complete = true;
                }

                public override void ConfigureHowToFindSaga()
                {
                    ConfigureMapping<UpdateSaga1>(d => d.MyId).ToSaga(s => s.MyId);
                    ConfigureMapping<CompleteSaga1>(d => d.MyId).ToSaga(s => s.MyId);
                }

                public class Saga1Data : ContainSagaData
                {
                    [Unique]
                    public Guid MyId { get; set; }
                }
            }

            public class Saga2 : Saga<Saga2.Saga2Data>, IAmStartedByMessages<InitiateSaga>, IHandleMessages<UpdateSaga2>, IHandleMessages<CompleteSaga2>
            {
                public MyContext Context { get; set; }

                static Guid myId = Guid.NewGuid();

                public void Handle(InitiateSaga message)
                {
                    Data.MyId = myId;
                    Bus.SendLocal(new UpdateSaga2 { MyId = myId });
                }

                public void Handle(UpdateSaga2 message)
                {
                    Bus.SendLocal(new CompleteSaga2 { MyId = myId });
                }

                public void Handle(CompleteSaga2 message)
                {
                    MarkAsComplete();
                    Context.Saga2Id = Data.Id;
                    Context.Saga2Complete = true;
                }

                public override void ConfigureHowToFindSaga()
                {
                    ConfigureMapping<UpdateSaga2>(d => d.MyId).ToSaga(s => s.MyId);
                    ConfigureMapping<CompleteSaga2>(d => d.MyId).ToSaga(s => s.MyId);
                }

                public class Saga2Data : ContainSagaData
                {
                    [Unique]
                    public Guid MyId { get; set; }
                }
            }
        }

        [Serializable]
        public class InitiateSaga : ICommand
        {
        }

        [Serializable]
        public class UpdateSaga1 : ICommand
        {
            public Guid MyId { get; set; }
        }

        [Serializable]
        public class CompleteSaga1 : ICommand
        {
            public Guid MyId { get; set; }
        }

        [Serializable]
        public class UpdateSaga2 : ICommand
        {
            public Guid MyId { get; set; }
        }

        [Serializable]
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