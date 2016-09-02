namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;

    struct ByteArrayMessageBody : IMessageBody
    {
        private byte[] body;

        public ByteArrayMessageBody(MessageBodyMetadata metadata, byte[] body)
        {
            this.body = body;
            Metadata = metadata;
        }

        public MessageBodyMetadata Metadata { get; }

        public Stream GetBody()
        {
            return new MemoryStream(body);
        }
    }
}