namespace Particular.AuditIngestion.Api
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using ServiceControl.Shell.Api.Ingestion;

    public class IngestedAuditMessage
    {
        readonly byte[] body;
        readonly IReadOnlyDictionary<string, string> headers;

        public IngestedAuditMessage(IngestedMessage rawMessage)
        {
            body = rawMessage.Body;
            headers = rawMessage.Headers;
        }

        public string Id
        {
            get { return null; }
        }

        public EndpointInstance SentFrom
        {
            get { return null; }
        }

        public EndpointInstance ProcessedAt
        {
            get { return null; }
        }

        public bool HasBody
        {
            get { return body != null; }
        }

        public Stream ReadBody()
        {
            if (!HasBody)
            {
                throw new InvalidOperationException("The message does not contain any body.");
            }
            return new MemoryStream(body);
        }

        public IReadOnlyDictionary<string, string> Headers
        {
            get { return headers; }
        }
    }
}