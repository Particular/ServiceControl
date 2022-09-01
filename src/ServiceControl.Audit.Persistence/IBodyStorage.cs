namespace ServiceControl.Audit.Auditing.BodyStorage
{
    using System.IO;
    using System.Threading.Tasks;

    public interface IBodyStorage
    {
        Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream);
        Task<StreamResult> TryFetch(string bodyId);
    }

    public class StreamResult
    {
        public bool HasResult;
        public Stream Stream;
        public string ContentType;
        public int BodySize;
        public string Etag;
    }
}