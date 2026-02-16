namespace ServiceControl.Audit.Persistence.MongoDB.BodyStorage
{
    using System;

    readonly struct BodyWriteItem
    {
        public required string Id { get; init; }
        public required string ContentType { get; init; }
        public required int BodySize { get; init; }
        public required byte[] Body { get; init; }
        public string TextBody { get; init; }
        public required DateTime ExpiresAt { get; init; }
    }
}
