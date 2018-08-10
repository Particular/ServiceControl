namespace ServiceBus.Management.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    public class When_message_groups_are_sorted_by_a_web_api_call : AcceptanceTest
    {
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
                    var result = await this.TryGetMany<FailedMessage.FailureGroup>("/recoverability/groups");
                    List<FailedMessage.FailureGroup> groups = result;
                    if (!result)
                    {
                        return false;
                    }

                    if (groups.Count != 1)
                    {
                        return false;
                    }

                    var errorResult = await this.TryGetMany<FailedMessageView>($"/recoverability/groups/{groups[0].Id}/errors?page=1&direction=asc&sort={sortProperty}");
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

        const string MessageId = "014b048-2b7b-4f94-8eda-d5be0fe50e92";

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

            class SendFailedMessages : DispatchRawMessages<MyContext>
            {
                protected override TransportOperations CreateMessage(MyContext context)
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
                    var messageId = $"{i}{MessageId}";
                    var msg = new OutgoingMessage(messageId, new Dictionary<string, string>
                    {
                        {Headers.MessageId, messageId},
                        {"NServiceBus.ExceptionInfo.ExceptionType", "System.Exception"},
                        {"NServiceBus.ExceptionInfo.Message", "An error occurred"},
                        {"NServiceBus.ExceptionInfo.Source", "NServiceBus.Core"},
                        {"NServiceBus.FailedQ", Conventions.EndpointNamingConvention(typeof(Receiver))}, // TODO: Correct?
                        {"NServiceBus.TimeOfFailure", "2014-11-11 02:26:58:000462 Z"},
                        {Headers.TimeSent, DateTimeExtensions.ToWireFormattedString(date)},
                        {Headers.EnclosedMessageTypes, $"MessageThatWillFail{i}"}
                    }, new byte[0]);
                    return msg;
                }
            }
        }

        public class MyContext : ScenarioContext
        {
        }
    }
}