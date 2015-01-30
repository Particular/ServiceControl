namespace Particular.Backend.AuditIngestion.Api
{
    using System;
    using System.IO;
    using ServiceControl.Shell.Api.Ingestion;

    public class IngestedAuditMessage
    {
        readonly string uniqueId;
        readonly string id;
        readonly byte[] body;
        readonly HeaderCollection headers;
        readonly MessageType messageType;
        readonly EndpointInstanceId sentFrom;
        readonly EndpointInstanceId processedAt;

        public IngestedAuditMessage(string id, string uniqueId, byte[] body, HeaderCollection headers, MessageType messageType, EndpointInstanceId sentFrom, EndpointInstanceId processedAt)
        {
            this.id = id;
            this.body = body;
            this.headers = headers;
            this.messageType = messageType;
            this.sentFrom = sentFrom;
            this.processedAt = processedAt;
            this.uniqueId = uniqueId;
        }

        public string Id
        {
            get { return id; }
        }

        public EndpointInstanceId SentFrom
        {
            get { return sentFrom; }
        }

        public EndpointInstanceId ProcessedAt
        {
            get { return processedAt; }
        }

        public bool HasBody
        {
            get { return Body != null; }
        }

        public int BodyLength
        {
            get { return HasBody ? Body.Length : 0; }
        }

        public HeaderCollection Headers
        {
            get { return headers; }
        }

        public MessageType MessageType
        {
            get { return messageType; }
        }

        public string UniqueId
        {
            get { return uniqueId; }
        }

        public byte[] Body
        {
            get { return body; }
        }
    }
}