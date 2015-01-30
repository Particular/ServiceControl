namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;

    public interface IBodyStorage
    {
        void Store(string bodyId, string contentType, int bodySize, Stream bodyStream);
        Stream Fetch(string bodyId);
    }
}