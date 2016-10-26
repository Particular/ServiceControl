namespace ServiceBus.Management.AcceptanceTests.Error
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    class When_a_multiple_error_message_with_same_uniqueid_are_imported_concurrently : AcceptanceTest
    {
        [Test]
        public void The_import_should_support_it()
        {
            var context = new MyContext();
            SetSettings = settings =>
            {
                settings.MaximumConcurrencyLevel = 10;
            };
            CustomConfiguration = config =>
            {
                config.DefineCriticalErrorAction((s, exception) => context.CriticalErrorExecuted = true);
            };

            FailedMessage failure = null;
            Define(context)
                .WithEndpoint<SourceEndpoint>()
                .Done(c =>
                {
                    if (c.UniqueId == null)
                    {
                        return false;
                    }

                    return c.CriticalErrorExecuted || TryGet($"/api/errors/{c.UniqueId}", out failure, m => m.ProcessingAttempts.Count == 10);
                })
                .Run();

            Assert.IsFalse(context.CriticalErrorExecuted);
            Assert.NotNull(failure);
            Assert.AreEqual(10, failure.ProcessingAttempts.Count);
        }

        public class SourceEndpoint : EndpointConfigurationBuilder
        {
            public SourceEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }

            public class SendMultipleFailedMessagesWithSameUniqueId : IWantToRunWhenBusStartsAndStops
            {
                ISendMessages sendMessages;
                MyContext context;

                public SendMultipleFailedMessagesWithSameUniqueId(ISendMessages sendMessages, MyContext context)
                {
                    this.sendMessages = sendMessages;
                    this.context = context;
                }

                public void Start()
                {
                    context.UniqueId = DeterministicGuid.MakeId("1", "Error.SourceEndpoint").ToString();

                    Parallel.For(0, 10, i =>
                    {
                        var headers = new Dictionary<string, string>
                        {
                            [Headers.ProcessingEndpoint] = "Error.SourceEndpoint",
                            ["NServiceBus.ExceptionInfo.ExceptionType"] = typeof(Exception).FullName,
                            ["NServiceBus.ExceptionInfo.Message"] = "Bad thing happened",
                            ["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception",
                            ["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core",
                            ["NServiceBus.ExceptionInfo.StackTrace"] = String.Empty,
                            ["NServiceBus.FailedQ"] = "Error.SourceEndpoint",
                            ["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z"
                        };
                        var message = new TransportMessage("1", headers);
                        sendMessages.Send(message, new SendOptions("error"));
                    });
                }

                public void Stop()
                {
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public string UniqueId { get; set; }
            public bool CriticalErrorExecuted { get; set; }
        }
    }
}
