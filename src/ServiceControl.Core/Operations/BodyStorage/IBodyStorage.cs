namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;

    public interface IBodyStorage
    {
        string Store(string bodyId, string contentType, int bodySize, Stream bodyStream);
        Stream Fetch(string bodyUrl);
    }
}