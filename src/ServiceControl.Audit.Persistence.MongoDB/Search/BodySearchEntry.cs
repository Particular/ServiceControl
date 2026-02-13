namespace ServiceControl.Audit.Persistence.MongoDB.Search
{
    using System;

    readonly struct BodyEntry
    {
        public required string Id { get; init; }
        public required string ContentType { get; init; }
        public required int BodySize { get; init; }
        public string TextBody { get; init; }
        public byte[] BinaryBody { get; init; }
        public required DateTime ExpiresAt { get; init; }
    }
}
