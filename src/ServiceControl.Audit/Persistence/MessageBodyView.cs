namespace ServiceControl.Audit.Persistence
{
    using System.IO;

    class MessageBodyView
    {
        public bool Found { get; private set; }
        public bool HasContent { get; private set; }
        public Stream StreamContent { get; private set; }
        public string StringContent { get; private set; }
        public string ContentType { get; private set; }
        public int ContentLength { get; private set; }
        public string ETag { get; private set; }

        public static MessageBodyView NotFound() => new MessageBodyView { Found = false, HasContent = false };

        public static MessageBodyView NoContent() => new MessageBodyView { Found = true, HasContent = false };

        public static MessageBodyView FromString(string content, string contentType, int contentLength, string etag)
            => new MessageBodyView
            {
                Found = true,
                HasContent = true,
                StringContent = content,
                ContentType = contentType,
                ContentLength = contentLength,
                ETag = etag
            };

        public static MessageBodyView FromStream(Stream content, string contentType, int contentLength, string etag)
            => new MessageBodyView
            {
                Found = true,
                HasContent = true,
                StreamContent = content,
                ContentType = contentType,
                ContentLength = contentLength,
                ETag = etag
            };
    }
}