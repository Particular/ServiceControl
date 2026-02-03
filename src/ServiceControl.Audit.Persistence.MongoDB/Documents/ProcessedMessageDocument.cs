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
        /// Text body content stored as UTF-8 string. Used for text-based messages (JSON, XML, plain text).
        /// This field is included in the text search index for full-text search.
        /// </summary>
        [BsonElement("body")]
        [BsonIgnoreIfNull]
        public string Body { get; set; }

        /// <summary>
        /// Binary body content stored as BSON BinData. Used for non-text messages.
        /// This field is NOT included in text search (binary content can't be meaningfully searched).
        /// </summary>
        [BsonElement("binaryBody")]
        [BsonIgnoreIfNull]
        public byte[] BinaryBody { get; set; }

        [BsonElement("processedAt")]
        public DateTime ProcessedAt { get; set; }

        [BsonElement("expiresAt")]
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Computed field for full-text search containing concatenated header values.
        /// Headers are stored as a dictionary and can't be directly text-indexed,
        /// so we flatten the values into a single searchable string.
        /// </summary>
        [BsonElement("headerSearchText")]
        [BsonIgnoreIfNull]
        public string HeaderSearchText { get; set; }
    }
}
