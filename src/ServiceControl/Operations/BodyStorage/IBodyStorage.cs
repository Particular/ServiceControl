namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;

    public interface IBodyStorage
    {
        string Store(string bodyId, string contentType, int bodySize, Stream bodyStream);
        bool TryFetch(string bodyId, out Stream stream);
    }
}