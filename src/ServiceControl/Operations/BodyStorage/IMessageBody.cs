namespace ServiceControl.Operations.BodyStorage
{
    using System.IO;

    public interface IMessageBody
    {
        MessageBodyMetadata Metadata { get; }
        Stream GetBody();
    }
}