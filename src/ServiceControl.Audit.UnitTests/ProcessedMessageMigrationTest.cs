using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NServiceBus;
using NUnit.Framework;
using Particular.Approvals;
using Raven.Client.Documents.Commands;
using Raven.TestDriver;
using ServiceControl.Audit.Auditing;
using ServiceControl.Audit.Infrastructure;
using Sparrow.Json;

namespace ServiceControl.Audit.UnitTests
{
    [TestFixture]
    public class ProcessedMessageMigrationTest : RavenTestDriver
    {
        [Test]
        public async Task Should_load_the_document()
        {
            const string oldDocumentJson = @"{
    ""UniqueMessageId"": ""518e1dde-d16c-4a0f-d699-ec178288af07"",
    ""MessageMetadata"": {
        ""SearchableMessageType"": ""ServiceControl LoadTests Messages AuditMessage"",
        ""IsRetried"": false,
        ""MessageIntent"": 1,
        ""IsSystemMessage"": false,
        ""SendingEndpoint"": {
            ""$type"": ""ServiceControl.Audit.Monitoring.EndpointDetails, ServiceControl.Audit"",
            ""Name"": ""AuditGenerator"",
            ""HostId"": ""6b581b38-1844-0da1-6624-7ee65e301afe"",
            ""Host"": ""SURFACEBOOK2""
        },
        ""BodyNotStored"": true,
        ""ConversationId"": ""01d4e139-0855-429e-bbf0-ac5600cae2c4"",
        ""ReceivingEndpoint"": {
            ""$type"": ""ServiceControl.Audit.Monitoring.EndpointDetails, ServiceControl.Audit"",
            ""Name"": ""LoadGenerator"",
            ""HostId"": ""8bf9147e-c232-40d9-8f93-1bcbc7fec718"",
            ""Host"": ""Load Generator""
        },
        ""DeliveryTime"": ""00:00:00"",
        ""ContentType"": ""text/xml"",
        ""MessageType"": ""ServiceControl.LoadTests.Messages.AuditMessage"",
        ""Body"": ""The Body"",
        ""ContentLength"": 1559,
        ""CriticalTime"": ""00:00:00"",
        ""MessageId"": ""081a72cf-6e77-478f-9ad9-ac5600cae2c4"",
        ""ProcessingTime"": ""00:00:00"",
        ""TimeSent"": ""2020-10-16T12:18:41.0760780Z"",
        ""BodyUrl"": ""/messages/081a72cf-6e77-478f-9ad9-ac5600cae2c4/body""
    },
    ""Headers"": {
        ""$.diagnostics.hostid"": ""8bf9147e-c232-40d9-8f93-1bcbc7fec718"",
        ""$.diagnostics.hostdisplayname"": ""Load Generator"",
        ""NServiceBus.ProcessingMachine"": ""SURFACEBOOK2"",
        ""NServiceBus.ProcessingEndpoint"": ""LoadGenerator"",
        ""NServiceBus.ProcessingStarted"": ""2020-10-16 12:18:41:076078 Z"",
        ""NServiceBus.ProcessingEnded"": ""2020-10-16 12:18:41:076078 Z"",
        ""NServiceBus.MessageId"": ""081a72cf-6e77-478f-9ad9-ac5600cae2c4"",
        ""NServiceBus.MessageIntent"": ""Send"",
        ""NServiceBus.ConversationId"": ""01d4e139-0855-429e-bbf0-ac5600cae2c4"",
        ""NServiceBus.CorrelationId"": ""081a72cf-6e77-478f-9ad9-ac5600cae2c4"",
        ""NServiceBus.OriginatingMachine"": ""SURFACEBOOK2"",
        ""NServiceBus.OriginatingEndpoint"": ""AuditGenerator"",
        ""$.diagnostics.originating.hostid"": ""6b581b3818440da166247ee65e301afe"",
        ""NServiceBus.ContentType"": ""text/xml"",
        ""NServiceBus.EnclosedMessageTypes"": ""ServiceControl.LoadTests.Messages.AuditMessage, ServiceControl.LoadTests.Messages, Version=4.0.0.0, Culture=neutral, PublicKeyToken=null"",
        ""NServiceBus.Version"": ""7.4.3"",
        ""NServiceBus.TimeSent"": ""2020-10-16 12:18:41:076078 Z"",
        ""CorrId"": ""081a72cf-6e77-478f-9ad9-ac5600cae2c4\\0""
    },
    ""ProcessedAt"": ""2020-10-16T12:18:41.0760780Z""
}";

            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession())
                {
                    await session.Advanced.RequestExecutor.ExecuteAsync(
                        new PutDocumentCommand("SomeId", null, ParseJson(session.Advanced.Context, oldDocumentJson)),
                        session.Advanced.Context);

                    session.SaveChanges();
                }

                using (var session = store.OpenSession())
                {
                    var loadedDocument = session.Load<ProcessedMessage>("SomeId");
                    Approver.Verify(loadedDocument);
                }
            }
        }

        public BlittableJsonReaderObject ParseJson(JsonOperationContext context, string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return context.ReadForMemory(stream, "SomeId");
            }
        }

        public class OldProcessedMessage
        {
            public OldProcessedMessage()
            {
                MessageMetadata = new Dictionary<string, object>();
                Headers = new Dictionary<string, string>();
            }

            public OldProcessedMessage(Dictionary<string, string> headers, Dictionary<string, object> metadata)
            {
                UniqueMessageId = headers.UniqueId();
                MessageMetadata = metadata;
                Headers = headers;

                if (Headers.TryGetValue(NServiceBus.Headers.ProcessingEnded, out var processedAt))
                {
                    ProcessedAt = DateTimeExtensions.ToUtcDateTime(processedAt);
                }
                else
                {
                    ProcessedAt = DateTime.UtcNow; // best guess
                }
            }

            public string Id { get; set; }

            public string UniqueMessageId { get; set; }

            public Dictionary<string, object> MessageMetadata { get; set; }

            public Dictionary<string, string> Headers { get; set; }

            public DateTime ProcessedAt { get; set; }
        }
    }
}