namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Operations;

    public class When_a_message_fails_to_import : AcceptanceTest
    {
        class FailOnceEnricher : ImportEnricher
        {
            public MyContext Context { get; set; }

            public override void Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                if (!Context.FailedImport)
                {
                    Console.WriteLine("Simulating message processing failure");
                    throw new Exception("Boom!");
                }
                Console.WriteLine("Message processed correctly");
            }
        }

        [Test]
        public void It_can_be_reimported()
        {
            //Make sure the audit import attempt fails
            CustomConfiguration = config =>
            {
                config.RegisterComponents(c => c.ConfigureComponent<FailOnceEnricher>(DependencyLifecycle.SingleInstance));
            };

            var context = new MyContext();
            List<MessagesView> response;
            FailedAuditsCountReponse failedAuditsCountReponse;

            Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<Receiver>()
                .Done(c =>
                {
                    if (c.MessageId == null)
                    {
                        return false;
                    }
                    if (!c.WasReimported)
                    {
                        if (TryGet("/api/failedaudits/count", out failedAuditsCountReponse))
                        {
                            if (failedAuditsCountReponse.Count > 0)
                            {
                                c.FailedImport = true;
                                Post<object>("/api/failedaudits/import", null, code =>
                                {
                                    if (code == HttpStatusCode.OK)
                                    {
                                        c.WasReimported = true;
                                        return false;
                                    }
                                    return true;
                                });
                            }
                        }
                        return false;
                    }
                    return TryGetMany("/api/messages/search/" + c.MessageId, out response);
                })
                .Run(TimeSpan.FromSeconds(40));
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithoutAudit>()
                    .AddMapping<MyMessage>(typeof(Receiver));
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

                public IBus Bus { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.MessageId = Bus.CurrentMessageContext.Id;

                    Thread.Sleep(200);
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
            public string PropertyToSearchFor { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public bool FailedImport { get; set; }
            public bool WasReimported { get; set; }
            public string MessageId { get; set; }
        }
    }
}