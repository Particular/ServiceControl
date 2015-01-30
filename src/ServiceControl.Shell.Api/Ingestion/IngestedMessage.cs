namespace ServiceControl.Shell.Api.Ingestion
{
    using System.Collections.Generic;

    public class IngestedMessage
    {
        readonly byte[] body;
        readonly HeaderCollection headers;

        public IngestedMessage(Dictionary<string, string> headers, byte[] body)
        {
            this.body = body;
            this.headers = new HeaderCollection(headers);
        }

        public bool HasBody
        {
            get { return body != null; }
        }

        public byte[] Body
        {
            get { return body; }
        }

        public HeaderCollection Headers
        {
            get { return headers; }
        }
    }
}
