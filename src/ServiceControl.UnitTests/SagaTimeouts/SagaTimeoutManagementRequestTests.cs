namespace ServiceControl.UnitTests.SagaTimeouts
{
    using System;
    using System.Text;
    using System.Text.Json;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.Persistence.SagaTimeouts;
    using ServiceControl.SagaTimeouts;
    using ManagementHeaders = ServiceControl.SagaTimeouts.RequestSagaTimeoutChangeHandler.ManagementHeaders;

    [TestFixture]
    class SagaTimeoutManagementRequestTests
    {
        [Test]
        public void Should_send_a_trigger_as_a_reschedule_with_an_empty_body()
        {
            var dueTime = new DateTimeOffset(2026, 10, 19, 12, 30, 15, TimeSpan.FromHours(2)).AddTicks(1234560);

            var request = RequestSagaTimeoutChangeHandler.BuildRequest(Change(SagaTimeoutOperation.Trigger, dueTime));

            Assert.Multiple(() =>
            {
                Assert.That(request.Headers[ManagementHeaders.Change], Is.EqualTo("Reschedule"));
                Assert.That(request.Headers[ManagementHeaders.DueTime], Is.EqualTo("2026-10-19 10:30:15:123456 Z"));
                Assert.That(request.Headers[ManagementHeaders.ExpectedVersion], Is.EqualTo("3"));
                Assert.That(request.Headers[ManagementHeaders.SagaType], Is.EqualTo("Sales.OrderSaga"));
                Assert.That(request.Headers[ManagementHeaders.TimeoutId], Is.EqualTo("timeout-1"));
                Assert.That(request.Headers[Headers.MessageId], Is.EqualTo(request.MessageId));
                Assert.That(request.Headers.ContainsKey(Headers.ContentType), Is.False);
                Assert.That(request.Body.IsEmpty, Is.True);
            });
        }

        [Test]
        public void Should_send_a_new_payload_as_the_body_of_a_reschedule()
        {
            var change = Change(SagaTimeoutOperation.Reschedule, DateTimeOffset.UtcNow);
            change.Payload = "{\"Amount\":1}";
            change.PayloadContentType = "application/json";

            var request = RequestSagaTimeoutChangeHandler.BuildRequest(change);

            Assert.Multiple(() =>
            {
                Assert.That(Encoding.UTF8.GetString(request.Body.Span), Is.EqualTo("{\"Amount\":1}"));
                Assert.That(request.Headers[Headers.ContentType], Is.EqualTo("application/json"));
            });
        }

        [Test]
        public void Should_send_a_cancel_without_a_due_time()
        {
            var request = RequestSagaTimeoutChangeHandler.BuildRequest(Change(SagaTimeoutOperation.Cancel, null));

            Assert.Multiple(() =>
            {
                Assert.That(request.Headers[ManagementHeaders.Change], Is.EqualTo("Cancel"));
                Assert.That(request.Headers.ContainsKey(ManagementHeaders.DueTime), Is.False);
                Assert.That(request.Body.IsEmpty, Is.True);
            });
        }

        [Test]
        public void Should_read_a_report_as_the_endpoint_serializes_it()
        {
            const string json = """
                {"TriggerId":"t1","DueTime":"2026-10-19T10:30:00+00:00","PayloadContentType":"application/json","Payload":"eyJBIjoxfQ==",
                 "TimeoutId":"timeout-1","TimeoutType":"Sales.PaymentTimeout","Version":1,"EndpointName":"Sales","SagaType":"Sales.OrderSaga",
                 "SagaId":"5c1b2a3e-1111-4c4c-8888-000000000001","CorrelationPropertyValue":"order-1","ChangedAt":"2026-10-19T10:00:00+00:00",
                 "Host":"host","HostId":"host-id","ManagementAddress":"Sales.SagaTimeouts"}
                """;

            var message = JsonSerializer.Deserialize(json, SagaTimeoutMessagesSerializationContext.Default.SagaTimeoutRequested);

            Assert.Multiple(() =>
            {
                Assert.That(Encoding.UTF8.GetString(message.Payload), Is.EqualTo("{\"A\":1}"));
                Assert.That(message.ManagementAddress, Is.EqualTo("Sales.SagaTimeouts"));
                Assert.That(message.Version, Is.EqualTo(1));
            });
        }

        static RequestSagaTimeoutChange Change(SagaTimeoutOperation operation, DateTimeOffset? dueTime) => new()
        {
            OperationId = Guid.NewGuid(),
            Change = operation,
            ManagementAddress = "Sales.SagaTimeouts",
            SagaType = "Sales.OrderSaga",
            SagaId = Guid.NewGuid(),
            TimeoutId = "timeout-1",
            ExpectedVersion = 3,
            DueTime = dueTime
        };
    }
}
