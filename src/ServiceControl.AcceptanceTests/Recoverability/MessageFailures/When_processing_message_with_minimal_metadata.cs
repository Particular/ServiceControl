namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures.Api;

    class When_processing_message_with_minimal_metadata : AcceptanceTest
    {
        [Test]
        public async Task Null_TimeSent_should_not_be_cast_to_DateTimeMin()
        {
            FailedMessageView failure = null;

            await Define<MyContext>()
                .WithEndpoint<Failing>()
                .Done(async c =>
                {
                    failure = await TryGetFailureFromApi(c);
                    return failure != null;
                })
                .Run();

            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.TimeSent, Is.Null);
        }

        [Test]
        public async Task TimeSent_should_not_be_casted()
        {
            FailedMessageView failure = null;

            var sentTime = DateTime.Parse("2014-11-11T02:26:58.000462Z").ToUniversalTime();

            await Define<MyContext>(ctx => ctx.TimeSent = sentTime)
                .WithEndpoint<Failing>()
                .Done(async c =>
                {
                    failure = await TryGetFailureFromApi(c);
                    return failure != null;
                })
                .Run();

            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.TimeSent, Is.EqualTo(sentTime));
        }

        [Test]
        public async Task Should_be_able_ingest_the_failed_message()
        {
            FailedMessageView failure = null;

            var testStartTime = DateTime.UtcNow;

            var context = await Define<MyContext>()
                .WithEndpoint<Failing>()
                .Done(async c =>
                {
                    failure = await TryGetFailureFromApi(c);
                    return failure != null;
                })
                .Run();

            Assert.That(failure, Is.Not.Null);

            //No failure time will result in utc now being used
            Assert.That(failure.TimeOfFailure, Is.GreaterThan(testStartTime));

            // ServicePulse assumes that the receiving endpoint name is present
            Assert.That(failure.ReceivingEndpoint, Is.Not.Null);
            Assert.That(failure.ReceivingEndpoint.Name, Is.EqualTo(context.FailedQueueAddress));
        }

        class Failing : EndpointConfigurationBuilder
        {
            public Failing() => EndpointSetup<DefaultServerWithoutAudit>(c => c.Recoverability().Delayed(x => x.NumberOfRetries(0)));

            class SendFailedMessage : DispatchRawMessages<MyContext>
            {
                protected override TransportOperations CreateMessage(MyContext context)
                {
                    var headers = new Dictionary<string, string>
                    {
                        ["NServiceBus.FailedQ"] = context.FailedQueueAddress,
                        [Headers.ProcessingMachine] = "unknown", // This is needed for endpoint detection to work since "host" is required, endpoint name is detected from the FailedQ header
                    };

                    if (context.TimeSent.HasValue)
                    {
                        headers["NServiceBus.TimeSent"] = DateTimeOffsetHelper.ToWireFormattedString(context.TimeSent.Value);
                    }

                    var outgoingMessage = new OutgoingMessage(Guid.NewGuid().ToString(), headers, Array.Empty<byte>());

                    return new TransportOperations(new TransportOperation(outgoingMessage, new UnicastAddressTag("error")));
                }
            }
        }

        async Task<FailedMessageView> TryGetFailureFromApi(MyContext context)
        {
            // Since we are running without a known message id and the learning transport doesn't allow the native message id to be controlled
            // we use a unique failed queue address to find it instead
            var result = await this.TryGetMany<FailedMessageView>($"/api/errors/");

            if (!result.HasResult)
            {
                return null;
            }

            return result.Items.SingleOrDefault(f => f.QueueAddress == context.FailedQueueAddress);
        }

        class MyContext : ScenarioContext
        {
            public DateTime? TimeSent { get; set; }

            public string FailedQueueAddress { get; set; } = $"MyFailingEndpoint{Guid.NewGuid()}";
        }
    }
}