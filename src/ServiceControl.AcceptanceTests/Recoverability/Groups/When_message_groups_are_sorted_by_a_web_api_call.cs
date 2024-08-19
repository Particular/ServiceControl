namespace ServiceControl.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_message_groups_are_sorted_by_a_web_api_call : AcceptanceTest
    {
        [Test]
        public async Task All_messages_in_group_should_be_sorted_by_time_sent()
        {
            var errors = await SortTest("time_sent");

            Assert.That(errors[0].MessageId, Does.StartWith("1"));
            Assert.That(errors[1].MessageId, Does.StartWith("2"));
            Assert.That(errors[2].MessageId, Does.StartWith("3"));
        }

        [Test]
        public async Task All_messages_in_group_should_be_sorted_by_message_type()
        {
            var errors = await SortTest("message_type");

            Assert.That(errors[0].MessageId, Does.StartWith("1"));
            Assert.That(errors[1].MessageId, Does.StartWith("2"));
            Assert.That(errors[2].MessageId, Does.StartWith("3"));
        }

        async Task<List<FailedMessageView>> SortTest(string sortProperty)
        {
            List<FailedMessageView> localErrors = null;

            await Define<MyContext>()
                .WithEndpoint<Receiver>()
                .Done(async c =>
                {
                    var result = await this.TryGetMany<FailedMessage.FailureGroup>("/api/recoverability/groups");
                    List<FailedMessage.FailureGroup> groups = result;
                    if (!result)
                    {
                        return false;
                    }

                    if (groups.Count != 1)
                    {
                        return false;
                    }

                    var errorResult = await this.TryGetMany<FailedMessageView>($"/api/recoverability/groups/{groups[0].Id}/errors?page=1&direction=asc&sort={sortProperty}");
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
            public Receiver() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.NoRetries();
                    c.LimitMessageProcessingConcurrencyTo(1);
                });

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
                        {"NServiceBus.FailedQ", Conventions.EndpointNamingConvention(typeof(Receiver))},
                        {"NServiceBus.TimeOfFailure", "2014-11-11 02:26:58:000462 Z"},
                        {Headers.TimeSent, DateTimeOffsetHelper.ToWireFormattedString(date)},
                        {Headers.EnclosedMessageTypes, $"MessageThatWillFail{i}"}
                    }, Array.Empty<byte>());
                    return msg;
                }
            }
        }

        public class MyContext : ScenarioContext
        {
        }
    }
}