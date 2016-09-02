namespace ServiceControl.Operations.BodyStorage
{
    public struct MessageBodyMetadata
    {
        public string MessageId { get; }
        public string ContentType { get; }
        public int Size { get; }

        public MessageBodyMetadata(string messageId, string contentType, int size)
        {
            MessageId = messageId;
            ContentType = contentType;
            Size = size;
        }
    }
}