namespace ServiceBus.Management.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;

    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;

    public class When_message_groups_are_sorted_by_a_web_api_call : AcceptanceTest
    {
        const string MessageId = "014b048-2b7b-4f94-8eda-d5be0fe50e92";

        [Test]
        public void All_messages_in_group_should_be_sorted_by_time_sent()
        {
            List<FailedMessageView> errors;

            SortTest("time_sent", out errors);

            Assert.IsTrue(errors[0].MessageId.StartsWith("1"));
            Assert.IsTrue(errors[1].MessageId.StartsWith("2"));
            Assert.IsTrue(errors[2].MessageId.StartsWith("3"));
        }

        [Test]
        public void All_messages_in_group_should_be_sorted_by_message_type()
        {
            List<FailedMessageView> errors;

            SortTest("message_type", out errors);

            Assert.IsTrue(errors[0].MessageId.StartsWith("1"));
            Assert.IsTrue(errors[1].MessageId.StartsWith("2"));
            Assert.IsTrue(errors[2].MessageId.StartsWith("3"));
        }

        void SortTest(string sortProperty, out List<FailedMessageView> errors)
        {
            var context = new MyContext();

            List<FailedMessageView> localErrors = null;

            Define(context)
                .WithEndpoint<Receiver>()
                .Done(c =>
                {
                    List<FailedMessage.FailureGroup> groups;

                    if (!TryGetMany("/api/recoverability/groups", out groups))
                    {
                        return false;
                    }

                    if (groups.Count != 1)
                    {
                        Thread.Sleep(1000);
                        return false;
                    }

                    if (!TryGetMany("/api/recoverability/groups/" + groups[0].Id + "/errors?page=1&direction=asc&sort=" + sortProperty, out localErrors))
                    {
                        return false;
                    }

                    if (localErrors.Count != 3)
                    {
                        Thread.Sleep(1000);
                        return false;
                    }

                    return true;
                })
                .Run();

            errors = localErrors;
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithAudit>(c => c.DisableFeature<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 1;
                        c.MaximumConcurrencyLevel = 1; // this should mean they get processed one at a time
                    });
            }

            public class SendFailedMessage : IWantToRunWhenBusStartsAndStops
            {
                readonly ISendMessages sendMessages;
                readonly ReadOnlySettings settings;

                public SendFailedMessage(ISendMessages sendMessages, ReadOnlySettings settings)
                {
                    this.sendMessages = sendMessages;
                    this.settings = settings;
                }

                public void Start()
                {
                    var msg = CreateTransportMessage(2);

                    sendMessages.Send(msg, new SendOptions(Address.Parse("error")));

                    msg = CreateTransportMessage(1);

                    sendMessages.Send(msg, new SendOptions(Address.Parse("error")));

                    msg = CreateTransportMessage(3);

                    sendMessages.Send(msg, new SendOptions(Address.Parse("error")));
                }

                TransportMessage CreateTransportMessage(int i)
                {
                    var date = new DateTime(2015, 9 + i, 20 + i, 0, 0, 0);
                    var msg = new TransportMessage(i + MessageId, new Dictionary<string, string>
                    {
                        {"NServiceBus.ExceptionInfo.ExceptionType", "System.Exception"},
                        {"NServiceBus.ExceptionInfo.Message", "An error occurred"},
                        {"NServiceBus.ExceptionInfo.Source", "NServiceBus.Core"},
                        {"NServiceBus.FailedQ", settings.LocalAddress().ToString()},
                        {"NServiceBus.TimeOfFailure", "2014-11-11 02:26:58:000462 Z"},
                        {Headers.TimeSent, DateTimeExtensions.ToWireFormattedString(date)},
                        {Headers.EnclosedMessageTypes, "MessageThatWillFail" + i},
                    });
                    return msg;
                }

                public void Stop()
                {

                }
            }
        }

        public class MyContext : ScenarioContext
        {
            
        }
    }
}