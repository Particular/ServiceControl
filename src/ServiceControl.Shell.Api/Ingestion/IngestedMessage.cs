namespace ServiceControl.Shell.Api.Ingestion
{
    using System.Collections.Generic;

    public class IngestedMessage
    {
        readonly byte[] body;
        readonly Dictionary<string, string> headers;

        public IngestedMessage(Dictionary<string, string> headers, byte[] body)
        {
            this.body = body;
            this.headers = headers;
        }


        public IReadOnlyDictionary<string, string> Headers
        {
            get { return headers; }
        }

        public byte[] Body
        {
            get { return body; }
        }
    }
}
