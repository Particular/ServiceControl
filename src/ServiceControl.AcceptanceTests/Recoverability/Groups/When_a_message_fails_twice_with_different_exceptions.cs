namespace ServiceBus.Management.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Web;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_message_fails_twice_with_different_exceptions : AcceptanceTest
    {
        [Test]
        public void Only_the_second_groups_should_apply()
        {
            var context = new MeowContext();

            FailedMessage originalMessage = null;
            FailedMessage retriedMessage = null;

            Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<MeowReceiver>(b => b.Given(bus => bus.SendLocal(new Meow())))
                .Done(ctx =>
                {
                    if (String.IsNullOrWhiteSpace(ctx.UniqueMessageId))
                    {
                        return false;
                    }

                    if (!ctx.Retrying)
                    {
                        FailedMessage originalMessageTemp;
                        if (TryGet($"/api/errors/{ctx.UniqueMessageId}", out originalMessageTemp))
                        {
                            originalMessage = originalMessageTemp;
                            ctx.Retrying = true;
                            Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        }
                    }
                    else
                    {
                        return TryGet(
                            $"/api/errors/{ctx.UniqueMessageId}", 
                            out retriedMessage, 
                            err => err.ProcessingAttempts.Count == 2
                            );
                    }

                    return false;
                })
                .Run(TimeSpan.FromMinutes(3));

            Assert.IsNotNull(originalMessage, "Original message was not received");
            Assert.IsNotNull(retriedMessage, "Retried message was not received");

            Assert.IsNotNull(originalMessage.FailureGroups, "The original message has no failure groups");
            Assert.IsNotNull(retriedMessage.FailureGroups, "The retried message has no failure groups");

            var originalFailureGroupIds = originalMessage.FailureGroups.Select(x => x.Id).ToArray();
            var retriedFailureGroupIds = retriedMessage.FailureGroups.Select(x => x.Id).ToArray();

            Assert.True(originalFailureGroupIds.Any(), "The original message was not classified");
            Assert.True(retriedFailureGroupIds.Any(), "The retried message was not classified");

            foreach (var failureId in originalFailureGroupIds)
            {
                Assert.False(retriedFailureGroupIds.Contains(failureId), "Failure Group {0} is still set on retried message", failureId);
            }
        }


        public class MeowReceiver : EndpointConfigurationBuilder
        {
            public MeowReceiver()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => c.DisableFeature<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 1;
                    });
            }

            public class FailingMessageHandler : IHandleMessages<Meow>
            {
                public MeowContext Context { get; set; }
                public IBus Bus { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public void Handle(Meow message)
                {
                    var messageId = Bus.CurrentMessageContext.Id.Replace(@"\", "-");

                    var uniqueMessageId = DeterministicGuid.MakeId(messageId, Settings.EndpointName()).ToString();
                    Context.UniqueMessageId = uniqueMessageId;

                    if (Context.Retrying)
                    {
                        throw new IOException("The disk is full");
                    }
                    else
                    {
                        throw new HttpException("The website is not responding");
                    }
                }
            }
        }


        [Serializable]
        public class Meow : ICommand
        {
            
        }

        public class MeowContext : ScenarioContext
        {
            public bool Retrying { get; set; }
            public string UniqueMessageId { get; set; }
        }
    }
}