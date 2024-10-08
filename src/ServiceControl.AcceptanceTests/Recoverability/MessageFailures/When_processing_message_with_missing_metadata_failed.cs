﻿namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures.Api;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_processing_message_with_missing_metadata_failed : AcceptanceTest
    {
        [Test]
        public async Task Null_TimeSent_should_not_be_cast_to_DateTimeMin()
        {
            FailedMessageView failure = null;

            await Define<MyContext>()
                .WithEndpoint<Failing>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<FailedMessageView>("/api/errors/", m => m.Id == c.UniqueMessageId);
                    failure = result;
                    return result;
                })
                .Run();

            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.TimeSent, Is.Null);
        }

        [Test]
        public async Task TimeSent_should_not_be_casted()
        {
            FailedMessageView failure = null;

            var sentTime = DateTime.Parse("2014-11-11T02:26:58.000462Z");

            await Define<MyContext>(ctx => { ctx.TimeSent = sentTime; })
                .WithEndpoint<Failing>()
                .Done(async c =>
                {
                    var result = await this.TryGet<FailedMessageView>($"/api/errors/last/{c.UniqueMessageId}");
                    failure = result;
                    return c.UniqueMessageId != null & result;
                })
                .Run();

            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.TimeSent, Is.EqualTo(sentTime));
        }

        [Test]
        public async Task Should_be_able_to_get_the_message_by_id()
        {
            FailedMessageView failure = null;

            await Define<MyContext>()
                .WithEndpoint<Failing>()
                .Done(async c =>
                {
                    var result = await this.TryGet<FailedMessageView>($"/api/errors/last/{c.UniqueMessageId}");
                    failure = result;
                    return c.UniqueMessageId != null & result;
                })
                .Run();

            Assert.That(failure, Is.Not.Null);
        }

        public class Failing : EndpointConfigurationBuilder
        {
            public Failing() => EndpointSetup<DefaultServerWithoutAudit>(c => { c.Recoverability().Delayed(x => x.NumberOfRetries(0)); });

            class SendFailedMessage : DispatchRawMessages<MyContext>
            {
                protected override TransportOperations CreateMessage(MyContext context)
                {
                    context.EndpointNameOfReceivingEndpoint = Conventions.EndpointNamingConvention(typeof(Failing));
                    context.MessageId = Guid.NewGuid().ToString();
                    context.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, context.EndpointNameOfReceivingEndpoint).ToString();

                    var headers = new Dictionary<string, string>
                    {
                        [Headers.MessageId] = context.MessageId,
                        [Headers.ProcessingEndpoint] = context.EndpointNameOfReceivingEndpoint,
                        ["NServiceBus.ExceptionInfo.ExceptionType"] = "2014-11-11 02:26:57:767462 Z",
                        ["NServiceBus.ExceptionInfo.Message"] = "An error occurred while attempting to extract logical messages from transport message NServiceBus.TransportMessage",
                        ["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception",
                        ["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core",
                        ["NServiceBus.ExceptionInfo.StackTrace"] = string.Empty,
                        ["NServiceBus.FailedQ"] = Conventions.EndpointNamingConvention(typeof(Failing)),
                        ["NServiceBus.TimeOfFailure"] = "2014-11-11 02:26:58:000462 Z"
                    };
                    if (context.TimeSent.HasValue)
                    {
                        headers["NServiceBus.TimeSent"] = DateTimeOffsetHelper.ToWireFormattedString(context.TimeSent.Value);
                    }

                    var outgoingMessage = new OutgoingMessage(context.MessageId, headers, new byte[0]);

                    return new TransportOperations(
                        new TransportOperation(outgoingMessage, new UnicastAddressTag("error"))
                    );
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string UniqueMessageId { get; set; }

            public DateTime? TimeSent { get; set; }
        }
    }
}