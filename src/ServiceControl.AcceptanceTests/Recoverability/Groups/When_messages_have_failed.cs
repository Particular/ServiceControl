namespace ServiceControl.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.Recoverability;

    class When_messages_have_failed : AcceptanceTest
    {
        [Test]
        public async Task Should_be_grouped()
        {
            List<FailureGroupView> defaultGroups = null;
            List<FailureGroupView> exceptionTypeAndStackTraceGroups = null;
            List<FailureGroupView> messageTypeGroups = null;

            FailedMessage failedMessageA = null;
            FailedMessage failedMessageB = null;

            var context = await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(async bus =>
                {
                    await bus.SendLocal(new MyMessageA());
                    await bus.SendLocal(new MyMessageB());
                }).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    if (c.MessageIdA == null || c.MessageIdB == null)
                    {
                        return false;
                    }

                    var defaultGroupsResult = await this.TryGetMany<FailureGroupView>("/api/recoverability/groups/");
                    defaultGroups = defaultGroupsResult;
                    if (!defaultGroupsResult)
                    {
                        return false;
                    }

                    if (defaultGroups.Count != 2)
                    {
                        return false;
                    }

                    messageTypeGroups = await this.TryGetMany<FailureGroupView>("/api/recoverability/groups/Message%20Type");
                    exceptionTypeAndStackTraceGroups = await this.TryGetMany<FailureGroupView>("/api/recoverability/groups/Exception%20Type%20and%20Stack%20Trace");

                    var failedMessageAResult = await this.TryGet<FailedMessage>($"/api/errors/{c.UniqueMessageIdA}", msg => msg.FailureGroups.Any());
                    failedMessageA = failedMessageAResult;
                    var failedMessageBResult = await this.TryGet<FailedMessage>($"/api/errors/{c.UniqueMessageIdB}", msg => msg.FailureGroups.Any());
                    failedMessageB = failedMessageBResult;
                    if (!failedMessageAResult || !failedMessageBResult)
                    {
                        return false;
                    }

                    return true;
                })
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(exceptionTypeAndStackTraceGroups.Count, Is.EqualTo(2), "There should be 2 Exception Type and Stack Trace Groups");
                Assert.That(messageTypeGroups.Count, Is.EqualTo(2), "There should be 2 Message Type Groups");
            });

            defaultGroups.ForEach(g => Console.WriteLine(JsonSerializer.Serialize(g)));

            Assert.Multiple(() =>
            {
                Assert.That(exceptionTypeAndStackTraceGroups.Select(g => g.Id).Except(defaultGroups.Select(g => g.Id)), Is.Empty, "/api/recoverability/groups did not retrieve Exception Type and Stack Trace Group");

                Assert.That(messageTypeGroups.Select(g => g.Id).ToArray(), Does.Contain(DeterministicGuid.MakeId(MessageTypeFailureClassifier.Id, typeof(MyMessageA).FullName).ToString()));
            });
            Assert.Multiple(() =>
            {
                Assert.That(messageTypeGroups.Select(g => g.Id).ToArray(), Does.Contain(DeterministicGuid.MakeId(MessageTypeFailureClassifier.Id, typeof(MyMessageB).FullName).ToString()));

                Assert.That(failedMessageA.UniqueMessageId, Is.EqualTo(context.UniqueMessageIdA));
                Assert.That(failedMessageB.UniqueMessageId, Is.EqualTo(context.UniqueMessageIdB));

                Assert.That(failedMessageA.FailureGroups, Is.Not.Empty, "MyMessageA should have failure groups");
                Assert.That(failedMessageB.FailureGroups, Is.Not.Empty, "MyMessageB should have failure groups");
            });

            Assert.That(failedMessageA.FailureGroups.Count(g => g.Type == ExceptionTypeAndStackTraceFailureClassifier.Id), Is.EqualTo(1), $"{ExceptionTypeAndStackTraceFailureClassifier.Id} FailureGroup was not created");
            Assert.That(failedMessageA.FailureGroups.Count(g => g.Type == MessageTypeFailureClassifier.Id), Is.EqualTo(1), $"{MessageTypeFailureClassifier.Id} FailureGroup was not created");

            Assert.That(failedMessageB.FailureGroups.Count(g => g.Type == ExceptionTypeAndStackTraceFailureClassifier.Id), Is.EqualTo(1), $"{ExceptionTypeAndStackTraceFailureClassifier.Id} FailureGroup was not created");
            Assert.That(failedMessageB.FailureGroups.Count(g => g.Type == MessageTypeFailureClassifier.Id), Is.EqualTo(1), $"{MessageTypeFailureClassifier.Id} FailureGroup was not created");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServerWithoutAudit>(c => { c.NoRetries(); });

            public class MyMessageHandler(MyContext scenarioContext, IReadOnlySettings settings) :
                IHandleMessages<MyMessageA>,
                IHandleMessages<MyMessageB>
            {
                public Task Handle(MyMessageA message, IMessageHandlerContext context)
                {
                    scenarioContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();
                    scenarioContext.MessageIdA = context.MessageId.Replace(@"\", "-");
                    throw new Exception("Simulated exception");
                }

                public Task Handle(MyMessageB message, IMessageHandlerContext context)
                {
                    scenarioContext.MessageIdB = context.MessageId.Replace(@"\", "-");
                    throw new Exception("Simulated exception");
                }
            }
        }

        public class MyMessageA : ICommand;

        public class MyMessageB : ICommand;

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