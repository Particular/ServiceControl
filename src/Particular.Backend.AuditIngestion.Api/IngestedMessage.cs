namespace Particular.Operations.Ingestion.Api
{
    public class IngestedMessage
    {
        readonly string uniqueId;
        readonly bool recoverable;
        readonly string id;
        readonly byte[] body;
        readonly HeaderCollection headers;
        readonly MessageType messageType;
        readonly EndpointInstance sentFrom;
        readonly EndpointInstance processedAt;

        public IngestedMessage(string id, string uniqueId, bool recoverable, byte[] body, HeaderCollection headers, MessageType messageType, 
            EndpointInstance sentFrom, EndpointInstance processedAt)
        {
            this.id = id;
            this.body = body;
            this.headers = headers;
            this.messageType = messageType;
            this.sentFrom = sentFrom;
            this.processedAt = processedAt;
            this.uniqueId = uniqueId;
            this.recoverable = recoverable;
        }

        public string Id
        {
            get { return id; }
        }

        public EndpointInstance SentFrom
        {
            get { return sentFrom; }
        }

        public EndpointInstance ProcessedAt
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

        public bool Recoverable
        {
            get { return recoverable; }
        }
    }
}