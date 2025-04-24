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

    class When_ingesting_failed_message_with_missing_headers : AcceptanceTest
    {
        [Test]
        public async Task TimeSent_should_not_be_casted()
        {
            FailedMessageView failure = null;

            var sentTime = DateTime.Parse("2014-11-11T02:26:58.000462Z");

            await Define<MyContext>(c =>
                {
                    c.AddMinimalRequiredHeaders();
                    c.Headers.Add("NServiceBus.TimeSent", DateTimeOffsetHelper.ToWireFormattedString(sentTime));
                })
                .WithEndpoint<FailingEndpoint>()
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
        public async Task Should_be_ingested_when_minimal_required_headers_is_present()
        {
            FailedMessageView failure = null;

            var testStartTime = DateTime.UtcNow;

            await Define<MyContext>(c =>
                {
                    c.AddMinimalRequiredHeaders();
                })
                .WithEndpoint<FailingEndpoint>()
                .Done(async c =>
                {
                    var result = await this.TryGet<FailedMessageView>($"/api/errors/last/{c.UniqueMessageId}");
                    failure = result;
                    return (c.UniqueMessageId != null) & result;
                })
                .Run();

            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.TimeSent, Is.Null);

            //No failure time will result in utc now being used
            Assert.That(failure.TimeOfFailure, Is.GreaterThan(testStartTime));

            // Both host and endpoint name is currently needed so this will be null since no host can be detected from the failed q header5
            Assert.That(failure.ReceivingEndpoint, Is.Null);
        }

        [Test]
        public async Task Should_include_headers_required_by_ServicePulse()
        {
            FailedMessageView failure = null;

            var context = await Define<MyContext>(c =>
                {
                    c.AddMinimalRequiredHeaders();

                    // This is needed for ServiceControl to be able to detect both endpoint (via failed q header) and host via the processing machine header
                    // Missing endpoint or host will case a null ref in ServicePulse
                    c.Headers[Headers.ProcessingMachine] = "MyMachine";

                    c.Headers["NServiceBus.ExceptionInfo.ExceptionType"] = "SomeExceptionType";
                    c.Headers["NServiceBus.ExceptionInfo.Message"] = "Some message";
                })
                .WithEndpoint<FailingEndpoint>()
                .Done(async c =>
                {
                    var result = await this.TryGet<FailedMessageView>($"/api/errors/last/{c.UniqueMessageId}");
                    failure = result;
                    return (c.UniqueMessageId != null) & result;
                })
                .Run();

            Assert.That(failure, Is.Not.Null);

            // ServicePulse assumes that the receiving endpoint name is present
            Assert.That(failure.ReceivingEndpoint, Is.Not.Null);
            Assert.That(failure.ReceivingEndpoint.Name, Is.EqualTo(context.EndpointNameOfReceivingEndpoint));
            Assert.That(failure.ReceivingEndpoint.Host, Is.EqualTo("MyMachine"));

            // ServicePulse needs both an exception type and description to render the UI in a resonable way
            Assert.That(failure.Exception.ExceptionType, Is.EqualTo("SomeExceptionType"));
            Assert.That(failure.Exception.Message, Is.EqualTo("Some message"));
        }

        class MyContext : ScenarioContext
        {
            public string MessageId { get; } = Guid.NewGuid().ToString();

            public string EndpointNameOfReceivingEndpoint => "MyEndpoint";

            public string UniqueMessageId => DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();

            public Dictionary<string, string> Headers { get; } = [];

            public void AddMinimalRequiredHeaders()
            {
                Headers["NServiceBus.FailedQ"] = EndpointNameOfReceivingEndpoint;
                Headers[NServiceBus.Headers.MessageId] = MessageId;
            }
        }

        class FailingEndpoint : EndpointConfigurationBuilder
        {
            public FailingEndpoint() => EndpointSetup<DefaultServerWithoutAudit>(c => c.Recoverability().Delayed(x => x.NumberOfRetries(0)));

            class SendFailedMessage : DispatchRawMessages<MyContext>
            {
                protected override TransportOperations CreateMessage(MyContext context)
                {
                    var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, Array.Empty<byte>());

                    return new TransportOperations(new TransportOperation(outgoingMessage, new UnicastAddressTag("error")));
                }
            }
        }
    }
}