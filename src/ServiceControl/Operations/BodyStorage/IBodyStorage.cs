namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;

    public interface IBodyStorage
    {
        string Store(string bodyId, string contentType, int bodySize, Stream bodyStream);
        StreamResult TryFetch(string bodyId);
    }

    public struct StreamResult
    {
        public bool HasResult;
        public Stream Stream;
        public string ContentType;
        public int BodySize;
        public string Etag;
    }
}