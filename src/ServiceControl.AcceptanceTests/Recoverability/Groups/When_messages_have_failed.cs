namespace ServiceBus.Management.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.Recoverability;

    public class When_messages_have_failed : AcceptanceTest
    {
        [Test]
        public void Should_be_grouped()
        {
            var context = new MyContext();

            List<FailureGroupView> defaultGroups = null;
            List<FailureGroupView> exceptionTypeAndStackTraceGroups = null;
            List<FailureGroupView> messageTypeGroups = null;

            FailedMessage failedMessageA = null;
            FailedMessage failedMessageB = null;

            List<FailedMessage> exceptionTypeAndStackTraceFailedMessages;
            List<FailedMessage> messageTypeFailedMessages;

            Define(context)
                .WithEndpoint<Receiver>(b => b.Given(bus =>
                {
                    bus.SendLocal(new MyMessageA());
                    bus.SendLocal(new MyMessageB());
                }))
                .Done(c =>
                {
                    if (c.MessageIdA == null || c.MessageIdB == null)
                    {
                        return false;
                    }

                    if (!TryGetMany("/api/recoverability/groups/", out defaultGroups))
                    {
                        return false;
                    }

                    if (defaultGroups.Count != 2)
                    {
                        Thread.Sleep(1000);
                        return false;
                    }

                    TryGetMany("/api/recoverability/groups/Message%20Type", out messageTypeGroups);
                    TryGetMany("/api/recoverability/groups/Exception%20Type%20and%20Stack%20Trace", out exceptionTypeAndStackTraceGroups);

                    if (!TryGet("/api/errors/" + c.UniqueMessageIdA, out failedMessageA, msg => msg.FailureGroups.Any()) || !TryGet("/api/errors/" + c.UniqueMessageIdB, out failedMessageB, msg => msg.FailureGroups.Any()))
                    {
                        return false;
                    }

                    return true;
                })
                .Run();

            Assert.AreEqual(2, exceptionTypeAndStackTraceGroups.Count, "There should be 2 Exception Type and Stack Trace Groups");
            Assert.AreEqual(2, messageTypeGroups.Count, "There should be 2 Message Type Groups");

            defaultGroups.ForEach(g => Console.WriteLine(JsonConvert.SerializeObject(g)));

            Assert.IsEmpty(exceptionTypeAndStackTraceGroups.Select(g => g.Id).Except(defaultGroups.Select(g => g.Id)), "/api/recoverability/groups did not retrieve Exception Type and Stack Trace Group");

            Assert.Contains(DeterministicGuid.MakeId(MessageTypeFailureClassifier.Id, typeof(MyMessageA).FullName).ToString(), messageTypeGroups.Select(g => g.Id).ToArray());
            Assert.Contains(DeterministicGuid.MakeId(MessageTypeFailureClassifier.Id, typeof(MyMessageB).FullName).ToString(), messageTypeGroups.Select(g => g.Id).ToArray());

            Assert.AreEqual(context.UniqueMessageIdA, failedMessageA.UniqueMessageId);
            Assert.AreEqual(context.UniqueMessageIdB, failedMessageB.UniqueMessageId);

            Assert.IsNotEmpty(failedMessageA.FailureGroups, "MyMessageA should have failure groups");
            Assert.IsNotEmpty(failedMessageB.FailureGroups, "MyMessageB should have failure groups");

            Assert.AreEqual(1, failedMessageA.FailureGroups.Count(g => g.Type == ExceptionTypeAndStackTraceFailureClassifier.Id), $"{ExceptionTypeAndStackTraceFailureClassifier.Id} FailureGroup was not created");
            Assert.AreEqual(1, failedMessageA.FailureGroups.Count(g => g.Type == MessageTypeFailureClassifier.Id), $"{MessageTypeFailureClassifier.Id} FailureGroup was not created");

            Assert.AreEqual(1, failedMessageB.FailureGroups.Count(g => g.Type == ExceptionTypeAndStackTraceFailureClassifier.Id), $"{ExceptionTypeAndStackTraceFailureClassifier.Id} FailureGroup was not created");
            Assert.AreEqual(1, failedMessageB.FailureGroups.Count(g => g.Type == MessageTypeFailureClassifier.Id), $"{MessageTypeFailureClassifier.Id} FailureGroup was not created");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => c.DisableFeature<SecondLevelRetries>());
            }

            public class MyMessageHandler :
                IHandleMessages<MyMessageA>,
                IHandleMessages<MyMessageB>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public void Handle(MyMessageA message)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.MessageIdA = Bus.CurrentMessageContext.Id.Replace(@"\", "-");
                    throw new Exception("Simulated exception");
                }

                public void Handle(MyMessageB message)
                {
                    Context.MessageIdB = Bus.CurrentMessageContext.Id.Replace(@"\", "-");
                    throw new Exception("Simulated exception");
                }
            }
        }

        [Serializable]
        public class MyMessageA : ICommand
        {
        }

        [Serializable]
        public class MyMessageB : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string MessageIdA { get; set; }
            public string MessageIdB { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string UniqueMessageIdA => DeterministicGuid.MakeId(MessageIdA, EndpointNameOfReceivingEndpoint).ToString();
            public string UniqueMessageIdB => DeterministicGuid.MakeId(MessageIdB, EndpointNameOfReceivingEndpoint).ToString();
        }
    }
}