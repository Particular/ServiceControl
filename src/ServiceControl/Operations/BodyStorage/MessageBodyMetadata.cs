namespace ServiceControl.Operations.BodyStorage
{
    public struct MessageBodyMetadata
    {
        public string MessageId { get; }
        public string ContentType { get; }
        public long Size { get; }

        public MessageBodyMetadata(string messageId, string contentType, long size)
        {
            MessageId = messageId;
            ContentType = contentType;
            Size = size;
        }
    }
}