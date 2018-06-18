namespace ServiceBus.Management.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Settings;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;

    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;

    public class When_message_groups_are_sorted_by_a_web_api_call : AcceptanceTest
    {
        const string MessageId = "014b048-2b7b-4f94-8eda-d5be0fe50e92";

        [Test]
        public async Task All_messages_in_group_should_be_sorted_by_time_sent()
        {
            var errors = await SortTest("time_sent");

            Assert.IsTrue(errors[0].MessageId.StartsWith("1"));
            Assert.IsTrue(errors[1].MessageId.StartsWith("2"));
            Assert.IsTrue(errors[2].MessageId.StartsWith("3"));
        }

        [Test]
        public async Task All_messages_in_group_should_be_sorted_by_message_type()
        {
            var errors = await SortTest("message_type");

            Assert.IsTrue(errors[0].MessageId.StartsWith("1"));
            Assert.IsTrue(errors[1].MessageId.StartsWith("2"));
            Assert.IsTrue(errors[2].MessageId.StartsWith("3"));
        }

        async Task<List<FailedMessageView>> SortTest(string sortProperty)
        {
            List<FailedMessageView> localErrors = null;

            await Define<MyContext>()
                .WithEndpoint<Receiver>()
                .Done(async c =>
                {
                    var result = await TryGetMany<FailedMessage.FailureGroup>("/api/recoverability/groups");
                    List<FailedMessage.FailureGroup> groups = result;
                    if (!result)
                    {
                        return false;
                    }

                    if (groups.Count != 1)
                    {
                        return false;
                    }

                    var errorResult = await TryGetMany<FailedMessageView>("/api/recoverability/groups/" + groups[0].Id + "/errors?page=1&direction=asc&sort=" + sortProperty);
                    localErrors = errorResult;
                    if (!errorResult)
                    {
                        return false;
                    }

                    if (localErrors.Count != 3)
                    {
                        return false;
                    }

                    return true;
                })
                .Run();

            return localErrors;
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                    {
                        var recoverability = c.Recoverability();
                        recoverability.Immediate(x => x.NumberOfRetries(0));
                        recoverability.Delayed(x => x.NumberOfRetries(0));
                        c.LimitMessageProcessingConcurrencyTo(1);
                    });
            }

            class SendFailedMessages : DispatchRawMessages
            {
                protected override TransportOperations CreateMessage()
                {
                    var errorAddress = new UnicastAddressTag("error");
                    
                    return new TransportOperations(
                        new TransportOperation(CreateTransportMessage(1), errorAddress),
                        new TransportOperation(CreateTransportMessage(2), errorAddress),
                        new TransportOperation(CreateTransportMessage(3), errorAddress)
                    );
                }

                OutgoingMessage CreateTransportMessage(int i)
                {
                    var date = new DateTime(2015, 9 + i, 20 + i, 0, 0, 0);
                    var msg = new OutgoingMessage(i + MessageId, new Dictionary<string, string>
                    {
                        {"NServiceBus.ExceptionInfo.ExceptionType", "System.Exception"},
                        {"NServiceBus.ExceptionInfo.Message", "An error occurred"},
                        {"NServiceBus.ExceptionInfo.Source", "NServiceBus.Core"},
                        {"NServiceBus.FailedQ", settings.LocalAddress()},
                        {"NServiceBus.TimeOfFailure", "2014-11-11 02:26:58:000462 Z"},
                        {Headers.TimeSent, DateTimeExtensions.ToWireFormattedString(date)},
                        {Headers.EnclosedMessageTypes, "MessageThatWillFail" + i},
                    }, new byte[0]);
                    return msg;
                }

                readonly ReadOnlySettings settings;

                public SendFailedMessages(ReadOnlySettings settings)
                {
                    this.settings = settings;
                }
            }
        }

        public class MyContext : ScenarioContext
        {

        }
    }
}