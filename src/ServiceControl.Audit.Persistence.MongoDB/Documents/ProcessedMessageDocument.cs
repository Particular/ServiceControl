namespace ServiceControl.Audit.Persistence.MongoDB.Documents
{
    using System;
    using System.Collections.Generic;
    using global::MongoDB.Bson;
    using global::MongoDB.Bson.Serialization.Attributes;

    class ProcessedMessageDocument
    {
        [BsonId]
        public string Id { get; set; }

        [BsonElement("uniqueMessageId")]
        public string UniqueMessageId { get; set; }

        [BsonElement("messageMetadata")]
        public BsonDocument MessageMetadata { get; set; }

        [BsonElement("headers")]
        public Dictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Flattened header values for text index search. Required because NServiceBus header
        /// keys contain dots and $ characters which MongoDB's text index cannot traverse.
        /// </summary>
        [BsonElement("headerSearchTokens")]
        [BsonIgnoreIfNull]
        public string HeaderSearchTokens { get; set; }

        [BsonElement("processedAt")]
        public DateTime ProcessedAt { get; set; }

        [BsonElement("expiresAt")]
        public DateTime ExpiresAt { get; set; }

        [BsonElement("bodyContentType")]
        [BsonIgnoreIfNull]
        public string BodyContentType { get; set; }

        [BsonElement("bodySize")]
        [BsonIgnoreIfDefault]
        public int BodySize { get; set; }

        [BsonElement("textBody")]
        [BsonIgnoreIfNull]
        public string TextBody { get; set; }

        [BsonElement("binaryBody")]
        [BsonIgnoreIfNull]
        public byte[] BinaryBody { get; set; }
    }
}
