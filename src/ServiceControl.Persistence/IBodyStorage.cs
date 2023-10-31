namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;
    using System.Threading.Tasks;

    public interface IBodyStorage
    {
        Task<MessageBodyStreamResult> TryFetch(string bodyId);
    }

    public class MessageBodyStreamResult
    {
        public bool HasResult;
        public Stream Stream; // Intentional, other streams could require a context
        public string ContentType;
        public int BodySize;
        public string Etag;
    }
}