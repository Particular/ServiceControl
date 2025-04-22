namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
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

            await Define<MyContext>(ctx => ctx.TimeSent = sentTime)
                .WithEndpoint<Failing>()
                .Done(async c =>
                {
                    var result = await this.TryGet<FailedMessageView>($"/api/errors/last/{c.UniqueMessageId}");
                    failure = result;
                    return (c.UniqueMessageId != null) & result;
                })
                .Run();

            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.TimeSent, Is.EqualTo(sentTime));
        }

        [Test]
        public async Task Should_be_able_to_get_the_message_by_id()
        {
            FailedMessageView failure = null;

            var testStartTime = DateTime.UtcNow;

            var context = await Define<MyContext>()
                .WithEndpoint<Failing>()
                .Done(async c =>
                {
                    var result = await this.TryGet<FailedMessageView>($"/api/errors/last/{c.UniqueMessageId}");
                    failure = result;
                    return (c.UniqueMessageId != null) & result;
                })
                .Run();

            Assert.That(failure, Is.Not.Null);

            //No failure time will result in utc now being used
            Assert.That(failure.TimeOfFailure, Is.GreaterThan(testStartTime));

            // ServicePulse assumes that the receiving endpoint name is present
            Assert.That(failure.ReceivingEndpoint, Is.Not.Null);
            Assert.That(failure.ReceivingEndpoint.Name, Is.EqualTo(context.EndpointNameOfReceivingEndpoint));
        }

        class Failing : EndpointConfigurationBuilder
        {
            public Failing() => EndpointSetup<DefaultServerWithoutAudit>(c => c.Recoverability().Delayed(x => x.NumberOfRetries(0)));

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
                        ["NServiceBus.FailedQ"] = Conventions.EndpointNamingConvention(typeof(Failing)),
                        [Headers.ProcessingMachine] = "unknown", // This is needed for endpoint detection to work since "host" is required, endpoint name is detected from the FailedQ header
                    };

                    if (context.TimeSent.HasValue)
                    {
                        headers["NServiceBus.TimeSent"] = DateTimeOffsetHelper.ToWireFormattedString(context.TimeSent.Value);
                    }

                    var outgoingMessage = new OutgoingMessage(context.MessageId, headers, Array.Empty<byte>());

                    return new TransportOperations(new TransportOperation(outgoingMessage, new UnicastAddressTag("error")));
                }
            }
        }

        class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string UniqueMessageId { get; set; }

            public DateTime? TimeSent { get; set; }
        }
    }
}