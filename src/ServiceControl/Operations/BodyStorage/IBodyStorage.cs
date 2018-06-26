namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;
    using System.Threading.Tasks;

    public interface IBodyStorage
    {
        Task<string> Store(string bodyId, string contentType, int bodySize, Stream bodyStream);
        Task<StreamResult> TryFetch(string bodyId);
    }

    public struct StreamResult
    {
        public bool HasResult;
        public Stream Stream;
    }
}