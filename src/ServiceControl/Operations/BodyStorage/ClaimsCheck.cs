namespace ServiceControl.Operations.BodyStorage
{
    public struct ClaimsCheck
    {
        public ClaimsCheck(bool stored, MessageBodyMetadata metadata)
        {
            Stored = stored;
            Metadata = metadata;
        }

        public bool Stored { get; }
        public MessageBodyMetadata Metadata { get; }
    }
}