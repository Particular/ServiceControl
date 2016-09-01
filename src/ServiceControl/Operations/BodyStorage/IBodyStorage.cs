namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;

    public interface IBodyStorage
    {
        bool TryFetch(string bodyId, out Stream stream, out string contentType, out long contentLength);
        void Delete(string bodyId);
    }
}