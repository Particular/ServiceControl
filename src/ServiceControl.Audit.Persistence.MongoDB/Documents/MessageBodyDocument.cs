namespace ServiceControl.Audit.Persistence.MongoDB.Documents
{
    using System;
    using global::MongoDB.Bson.Serialization.Attributes;

    class MessageBodyDocument
    {
        [BsonId]
        public string Id { get; set; }

        [BsonElement("contentType")]
        public string ContentType { get; set; }

        [BsonElement("bodySize")]
        public int BodySize { get; set; }

        /// <summary>
        /// Text body content for text-based content types (JSON, XML, plain text).
        /// Stored as string for full-text search support.
        /// </summary>
        [BsonElement("textBody")]
        [BsonIgnoreIfNull]
        public string TextBody { get; set; }

        /// <summary>
        /// Binary body content for non-text content types (protobuf, images, etc.).
        /// Stored as byte[] for efficient storage.
        /// </summary>
        [BsonElement("binaryBody")]
        [BsonIgnoreIfNull]
        public byte[] BinaryBody { get; set; }

        [BsonElement("expiresAt")]
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Determines if the content type represents text-based content that can be searched.
        /// </summary>
        public static bool IsTextContentType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
            {
                return false;
            }

            // TODO: Better way to determine text-based content types?
            return contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
                   contentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
                   contentType.Contains("xml", StringComparison.OrdinalIgnoreCase);
        }
    }
}
