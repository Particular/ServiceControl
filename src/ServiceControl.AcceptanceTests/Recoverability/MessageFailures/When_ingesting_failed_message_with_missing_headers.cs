namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.EndpointTemplates;
using Infrastructure;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.Faults;
using NServiceBus.Routing;
using NServiceBus.Transport;
using NUnit.Framework;
using ServiceControl.MessageFailures.Api;

class When_ingesting_failed_message_with_missing_headers : AcceptanceTest
{
    [Test]
    public async Task Should_be_ingested_when_minimal_required_headers_is_present()
    {
        var testStartTime = DateTime.UtcNow;

        var context = await Define<TestContext>(c => c.AddMinimalRequiredHeaders())
            .WithEndpoint<FailingEndpoint>()
            .Done(async c => await TryGetFailureFromApi(c))
            .Run();

        var failure = context.Failure;

        Assert.That(failure, Is.Not.Null);
        Assert.That(failure.TimeSent, Is.Null);

        //No failure time will result in utc now being used
        Assert.That(failure.TimeOfFailure, Is.GreaterThan(testStartTime));

        // Both host and endpoint name is currently needed so this will be null since no host can be detected from the failed q header
        Assert.That(failure.ReceivingEndpoint, Is.Null);
    }

    [Test]
    public async Task Should_include_headers_required_by_ServicePulse()
    {
        var context = await Define<TestContext>(c =>
            {
                c.AddMinimalRequiredHeaders();

                // This is needed for ServiceControl to be able to detect both endpoint (via failed q header) and host via the processing machine header
                // Missing endpoint or host will cause a null ref in ServicePulse
                c.Headers[Headers.ProcessingMachine] = "MyMachine";

                c.Headers[FaultsHeaderKeys.ExceptionType] = "SomeExceptionType";
                c.Headers[FaultsHeaderKeys.Message] = "Some message";
            })
            .WithEndpoint<FailingEndpoint>()
            .Done(async c => await TryGetFailureFromApi(c))
            .Run();

        var failure = context.Failure;

        Assert.That(failure, Is.Not.Null);

        // ServicePulse assumes that the receiving endpoint name is present
        Assert.That(failure.ReceivingEndpoint, Is.Not.Null);
        Assert.That(failure.ReceivingEndpoint.Name, Is.EqualTo(context.EndpointNameOfReceivingEndpoint));
        Assert.That(failure.ReceivingEndpoint.Host, Is.EqualTo("MyMachine"));

        // ServicePulse needs both an exception type and description to render the UI in a resonable way
        Assert.That(failure.Exception.ExceptionType, Is.EqualTo("SomeExceptionType"));
        Assert.That(failure.Exception.Message, Is.EqualTo("Some message"));
    }

    [Test]
    public async Task TimeSent_should_not_be_casted()
    {
        var sentTime = DateTime.Parse("2014-11-11T02:26:58.000462Z");

        var context = await Define<TestContext>(c =>
            {
                c.AddMinimalRequiredHeaders();
                c.Headers.Add(Headers.TimeSent, DateTimeOffsetHelper.ToWireFormattedString(sentTime));
            })
            .WithEndpoint<FailingEndpoint>()
            .Done(async c => await TryGetFailureFromApi(c))
            .Run();

        var failure = context.Failure;

        Assert.That(failure, Is.Not.Null);
        Assert.That(failure.TimeSent, Is.EqualTo(sentTime));
    }

    async Task<bool> TryGetFailureFromApi(TestContext context)
    {
        context.Failure = await this.TryGet<FailedMessageView>($"/api/errors/last/{context.UniqueMessageId}");
        return context.Failure != null;
    }

    class TestContext : ScenarioContext
    {
        public string MessageId { get; } = Guid.NewGuid().ToString();

        public string EndpointNameOfReceivingEndpoint => "MyEndpoint";

        public string UniqueMessageId => DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();

        public Dictionary<string, string> Headers { get; } = [];

        public FailedMessageView Failure { get; set; }

        public void AddMinimalRequiredHeaders()
        {
            Headers[FaultsHeaderKeys.FailedQ] = EndpointNameOfReceivingEndpoint;
            Headers[NServiceBus.Headers.MessageId] = MessageId;
        }
    }

    class FailingEndpoint : EndpointConfigurationBuilder
    {
        public FailingEndpoint() => EndpointSetup<DefaultServerWithoutAudit>(c => c.EnableFeature<SendFailedMessage>());

        class SendFailedMessage : DispatchRawMessages<TestContext>
        {
            protected override TransportOperations CreateMessage(TestContext context)
            {
                var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, Array.Empty<byte>());

                return new TransportOperations(new TransportOperation(outgoingMessage, new UnicastAddressTag("error")));
            }
        }
    }
}